using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed record ByokAccountBindingIdentity
{
    private const string Prefix = "byok-account-sha256-";

    public ByokAccountBindingIdentity(string value)
    {
        if (value is null || value.Length != Prefix.Length + 64 || !value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.Skip(Prefix.Length).Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("BYOK account binding must be an opaque SHA-256 identity, never a credential or raw account subject.", nameof(value));
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record FinancialJournalHeader(
    string Schema,
    string FinancialJournalIdentity,
    string FinancialRunIdentity,
    CognitionLane Lane,
    string PolicyDigest,
    string PremiumModelRevisionIdentity,
    ByokAccountBindingIdentity? ByokAccountBinding,
    long RunCeilingMicrousd,
    long AccountCeilingMicrousd,
    int MaximumJobs,
    int MaximumOpenReservations,
    string HeaderChecksum)
{
    public const string CurrentSchema = "snow_globe_financial_journal/v1";

    public static FinancialJournalHeader Create(
        string financialJournalIdentity,
        string financialRunIdentity,
        CognitionLane lane,
        string policyDigest,
        string premiumModelRevisionIdentity,
        ByokAccountBindingIdentity? byokAccountBinding,
        long runCeilingMicrousd,
        long accountCeilingMicrousd,
        int maximumJobs,
        int maximumOpenReservations)
    {
        FinancialJournalHeader header = new(CurrentSchema, financialJournalIdentity, financialRunIdentity, lane,
            policyDigest, premiumModelRevisionIdentity, byokAccountBinding, runCeilingMicrousd,
            accountCeilingMicrousd, maximumJobs, maximumOpenReservations, string.Empty);
        header.Validate(includeChecksum: false);
        return header with { HeaderChecksum = FinancialJournalJson.HeaderChecksum(header) };
    }

    public void Validate(bool includeChecksum = true)
    {
        if (!string.Equals(Schema, CurrentSchema, StringComparison.Ordinal))
            throw new InvalidDataException("Financial Journal schema is unsupported.");
        FinancialJournalValidation.Identity(FinancialJournalIdentity, nameof(FinancialJournalIdentity));
        FinancialJournalValidation.Identity(FinancialRunIdentity, nameof(FinancialRunIdentity));
        if (!Enum.IsDefined(Lane)) throw new InvalidDataException("Financial Journal lane is invalid.");
        FinancialJournalValidation.Digest(PolicyDigest, nameof(PolicyDigest));
        FinancialJournalValidation.ContentAddress(PremiumModelRevisionIdentity, nameof(PremiumModelRevisionIdentity));
        if (Lane == CognitionLane.Premium && ByokAccountBinding is null)
            throw new InvalidDataException("Premium Financial Journals require an opaque BYOK account binding.");
        if (Lane == CognitionLane.Local && ByokAccountBinding is not null)
            throw new InvalidDataException("Local Financial Journals must not contain a BYOK account binding.");
        if (RunCeilingMicrousd < 0 || AccountCeilingMicrousd < 0 || MaximumJobs < 0 || MaximumJobs > FinancialJournalBounds.MaximumRecords
            || MaximumOpenReservations < 0 || MaximumOpenReservations > MaximumJobs)
            throw new InvalidDataException("Financial Journal limits are invalid.");
        if (includeChecksum && !string.Equals(HeaderChecksum, FinancialJournalJson.HeaderChecksum(this), StringComparison.Ordinal))
            throw new InvalidDataException("Financial Journal header checksum mismatch.");
    }
}

public enum FinancialJournalApplyStatus
{
    Admitted,
    Pending,
    SubmissionUnknown,
    Replay,
    Conflict,
    JobCapDenied,
    RunCeilingDenied,
    AccountCeilingDenied,
    OpenReservationCapDenied,
    RecordHeadroomDenied,
    RecordCapacityExhausted,
    DispatchMarked,
    Completed,
    Reconciled,
    ReconciliationReplay
}

public enum FinancialReconciliationOutcome { Release, Settle }

public abstract record FinancialJournalCommand(string IdempotencyKey, string JobDigest);
public sealed record AdmitAndReserveFinancialJournalCommand(
    string IdempotencyKey,
    string JobDigest,
    PremiumCognitionJob? PremiumJob,
    long ReservedMicrousd) : FinancialJournalCommand(IdempotencyKey, JobDigest);
public sealed record MarkDispatchUnknownFinancialJournalCommand(
    string IdempotencyKey,
    string JobDigest,
    long ExpectedVersion) : FinancialJournalCommand(IdempotencyKey, JobDigest);
public sealed record CompleteFinancialJournalCommand(
    string IdempotencyKey,
    string JobDigest,
    long ExpectedVersion,
    InferenceReceipt Receipt) : FinancialJournalCommand(IdempotencyKey, JobDigest);
public sealed record ReconcileUnknownFinancialJournalCommand(
    string IdempotencyKey,
    string JobDigest,
    long ExpectedVersion,
    ByokAccountBindingIdentity AccountBinding,
    string EvidenceIdentity,
    string EvidenceDigest,
    FinancialReconciliationOutcome Outcome,
    long SettledMicrousd) : FinancialJournalCommand(IdempotencyKey, JobDigest);

public abstract record FinancialJournalQuery;
public sealed record FinancialJournalPageQuery(int Offset = 0, int Limit = 64) : FinancialJournalQuery;
public sealed record FinancialJournalSnapshotQuery : FinancialJournalQuery;

public sealed record FinancialJournalApplyResult(
    FinancialJournalApplyStatus Status,
    long Version,
    InferenceReceipt? Receipt,
    string OutcomeCode,
    bool AppendRequired = false);

public sealed record FinancialJournalEntrySnapshot(
    string IdempotencyKey,
    string JobDigest,
    long Version,
    long ReservedMicrousd,
    long EffectiveSettledMicrousd,
    SubmissionState SubmissionState,
    ChargeState EffectiveChargeState,
    string OutcomeCode,
    PremiumCognitionJob? PremiumJob,
    InferenceReceipt? Receipt,
    string? ReconciliationEvidenceIdentity,
    string? ReconciliationEvidenceDigest);

public sealed record FinancialJournalReadResult(
    FinancialJournalHeader Header,
    IReadOnlyList<FinancialJournalEntrySnapshot> Entries,
    int RecordCount,
    IReadOnlyList<string> Trace);

/// <summary>A small command/query seam. Implementations never call inference adapters.</summary>
public interface IFinancialJournal : IDisposable
{
    FinancialJournalApplyResult Apply(FinancialJournalCommand command);
    FinancialJournalReadResult Read(FinancialJournalQuery query);
}

internal static class FinancialJournalBounds
{
    internal const int MaximumHeaderBytes = 4 * 1024;
    internal const int MaximumRecordBytes = 8 * 1024;
    internal const int MaximumRecords = 4096;
    internal const long MaximumTotalBytes = 32L * 1024 * 1024;
    internal const int MaximumPageSize = 64;
    internal const int MaximumJsonDepth = 12;
}

internal static class FinancialJournalValidation
{
    private static readonly HashSet<string> ReceiptOutcomeCodes = new(StringComparer.Ordinal)
    { "premium_success", "local_success", "local_fallback", "local_invalid", "local_error", "deterministic_idle", "capacity_denied", "job_cap_denied", "run_ceiling_denied", "account_ceiling_denied", "open_reservation_cap_denied", "record_headroom_denied", "provider_rejected", "provider_timeout", "provider_malformed", "provider_unknown", "policy_rejected" };
    private static readonly HashSet<string> ReconciliationEvidenceIdentities = new(StringComparer.Ordinal)
    { "provider_charge_evidence/v1" };
    internal static void Identity(string value, string name)
    {
        if (!SnowGlobeInferenceIdentity.IsCanonical(value)) throw new InvalidDataException($"{name} is not a canonical identity.");
    }

    internal static void Digest(string value, string name)
    {
        if (value is null || value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException($"{name} is not a lowercase SHA-256 digest.");
    }

    internal static void ContentAddress(string value, string name)
    {
        if (value is null || value.Length != 71 || !value.StartsWith("sha256-", StringComparison.Ordinal))
            throw new InvalidDataException($"{name} is not a content address.");
        Digest(value[7..], name);
    }

    internal static void Command(FinancialJournalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Digest(command.IdempotencyKey, nameof(command.IdempotencyKey));
        Digest(command.JobDigest, nameof(command.JobDigest));
    }

    internal static void Receipt(InferenceReceipt receipt, FinancialJournalHeader header, string key, string digest)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Reservation is null || receipt.Proposal is null) throw new InvalidDataException("Inference receipt contains null nested values.");
        if (!string.Equals(receipt.IdempotencyKey, key, StringComparison.Ordinal)
            || !string.Equals(receipt.JobDigest, digest, StringComparison.Ordinal)
            || !string.Equals(receipt.PolicyDigest, header.PolicyDigest, StringComparison.Ordinal)
            || !string.Equals(receipt.FinancialJournalIdentity, header.FinancialJournalIdentity, StringComparison.Ordinal)
            || receipt.RequestedLane != header.Lane
            || !string.Equals(receipt.PremiumModelRevisionIdentity, header.PremiumModelRevisionIdentity, StringComparison.Ordinal))
            throw new InvalidDataException("Inference receipt identity binding mismatch.");
        if (receipt.Reservation.ReservedMicrousd < 0 || receipt.Reservation.SettledMicrousd < 0
            || receipt.Reservation.SettledMicrousd > receipt.Reservation.ReservedMicrousd)
            throw new InvalidDataException("Inference receipt accounting is invalid.");
        Identity(receipt.ReasonCode, nameof(receipt.ReasonCode));
        Identity(receipt.PrimaryOutcomeCode, nameof(receipt.PrimaryOutcomeCode));
        Identity(receipt.PremiumModelIdentity, nameof(receipt.PremiumModelIdentity));
        ContentAddress(receipt.PremiumModelRevisionIdentity, nameof(receipt.PremiumModelRevisionIdentity));
        if (!ReceiptOutcomeCodes.Contains(receipt.ReasonCode) || !ReceiptOutcomeCodes.Contains(receipt.PrimaryOutcomeCode))
            throw new InvalidDataException("Inference receipt outcome is not allowlisted.");
        Identity(receipt.Proposal.AgentId, nameof(receipt.Proposal.AgentId));
        if (!Enum.IsDefined(receipt.SubmissionState) || !Enum.IsDefined(receipt.ChargeState) || !Enum.IsDefined(receipt.Proposal.Action))
            throw new InvalidDataException("Inference receipt enum is invalid.");
        bool proposalShape = receipt.Proposal.Action switch
        {
            SnowGlobeActionKind.Idle or SnowGlobeActionKind.BuildShelter or SnowGlobeActionKind.BuildStorage => receipt.Proposal.Quantity == 0,
            SnowGlobeActionKind.GatherWood or SnowGlobeActionKind.GatherStone or SnowGlobeActionKind.MaintainShelter => receipt.Proposal.Quantity is > 0 and <= 64,
            _ => false
        };
        if (!proposalShape) throw new InvalidDataException("Inference receipt proposal shape is invalid.");
    }

    internal static void Job(PremiumCognitionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Observation is null) throw new InvalidDataException("Premium job observation is null.");
        Digest(job.IdempotencyKey, nameof(job.IdempotencyKey));
        Digest(job.JobDigest, nameof(job.JobDigest));
        Digest(job.PolicyDigest, nameof(job.PolicyDigest));
        Identity(job.PremiumModelIdentity, nameof(job.PremiumModelIdentity));
        ContentAddress(job.PremiumModelRevisionIdentity, nameof(job.PremiumModelRevisionIdentity));
        Identity(job.Observation.AgentId, nameof(job.Observation.AgentId));
        if (job.Observation.HomeSlot < 0 || job.Observation.Tick < 0 || job.Observation.AvailableWood < 0 || job.Observation.AvailableStone < 0
            || job.Observation.StockpileWood < 0 || job.Observation.StockpileStone < 0 || job.Observation.ShelterCount < 0 || job.Observation.StorageCount < 0)
            throw new InvalidDataException("Premium job observation contains negative values.");
    }

    internal static void ReconciliationEvidence(string identity, string digest)
    {
        Identity(identity, nameof(identity)); Digest(digest, nameof(digest));
        if (!ReconciliationEvidenceIdentities.Contains(identity)) throw new InvalidDataException("Reconciliation evidence identity is not allowlisted.");
    }

    internal static long Add(long left, long right)
    {
        try { return checked(left + right); }
        catch (OverflowException ex) { throw new InvalidDataException("Financial Journal arithmetic overflowed.", ex); }
    }
}

internal enum FinancialEntryPhase { Admitted, DispatchUnknown, Denied, Completed }

internal sealed class FinancialJournalState
{
    private readonly Dictionary<string, FinancialEntry> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _trace = new();
    internal FinancialJournalState(FinancialJournalHeader header) { Header = header; }
    internal FinancialJournalHeader Header { get; }
    internal int RecordCount { get; private set; }
    internal string PreviousChecksum { get; private set; } = new('0', 64);
    internal IReadOnlyDictionary<string, FinancialEntry> Entries => _entries;

    internal FinancialJournalApplyResult Plan(FinancialJournalCommand command, HashSet<string> activeKeys, bool loading = false)
    {
        FinancialJournalValidation.Command(command);
        return command switch
        {
            AdmitAndReserveFinancialJournalCommand admit => PlanAdmit(admit, activeKeys, loading),
            MarkDispatchUnknownFinancialJournalCommand mark => PlanMark(mark),
            CompleteFinancialJournalCommand complete => PlanComplete(complete),
            ReconcileUnknownFinancialJournalCommand reconcile => PlanReconcile(reconcile),
            _ => throw new InvalidDataException("Unknown Financial Journal command.")
        };
    }

    private FinancialJournalApplyResult PlanAdmit(AdmitAndReserveFinancialJournalCommand command, HashSet<string> activeKeys, bool loading)
    {
        ValidateAdmission(command);
        if (_entries.TryGetValue(command.IdempotencyKey, out FinancialEntry? existing))
        {
            if (!string.Equals(existing.JobDigest, command.JobDigest, StringComparison.Ordinal)) return Result(FinancialJournalApplyStatus.Conflict, existing, "idempotency_conflict");
            if (existing.RequestedReservedMicrousd != command.ReservedMicrousd || !Equals(existing.PremiumJob, command.PremiumJob))
                return Result(FinancialJournalApplyStatus.Conflict, existing, "admission_conflict");
            if (existing.Receipt is not null) return new(FinancialJournalApplyStatus.Replay, existing.Version, CloneReceipt(existing.Receipt), "receipt_replay");
            if (existing.Phase == FinancialEntryPhase.DispatchUnknown) return Result(FinancialJournalApplyStatus.SubmissionUnknown, existing, "submission_unknown");
            if (existing.Phase == FinancialEntryPhase.Denied) return Result(StatusForDenial(existing.OutcomeCode), existing, existing.OutcomeCode);
            if (activeKeys.Contains(command.IdempotencyKey)) return Result(FinancialJournalApplyStatus.Pending, existing, "pending");
            if (!loading) activeKeys.Add(command.IdempotencyKey);
            return Result(FinancialJournalApplyStatus.Admitted, existing, "admitted");
        }

        int worstCaseRecords = Header.Lane == CognitionLane.Premium ? 4 : 2;
        FinancialJournalApplyStatus denial = !CanFitFutureRecords(worstCaseRecords)
            ? CanFitFutureRecords(2) ? FinancialJournalApplyStatus.RecordHeadroomDenied : FinancialJournalApplyStatus.RecordCapacityExhausted
            : AdmissionDenial(command);
        if (denial == FinancialJournalApplyStatus.RecordCapacityExhausted)
            return new(denial, 0, null, "record_capacity_exhausted");
        string outcome = OutcomeFor(denial);
        return new(denial == FinancialJournalApplyStatus.Admitted ? FinancialJournalApplyStatus.Admitted : denial, 1, null, outcome, AppendRequired: true);
    }

    private void ValidateAdmission(AdmitAndReserveFinancialJournalCommand command)
    {
        if (command.ReservedMicrousd < 0) throw new InvalidDataException("Reservation cannot be negative.");
        if (Header.Lane == CognitionLane.Premium)
        {
            if (command.PremiumJob is null || command.ReservedMicrousd <= 0) throw new InvalidDataException("Premium admission requires a job and positive full reservation.");
            FinancialJournalValidation.Job(command.PremiumJob);
            if (!string.Equals(command.PremiumJob.IdempotencyKey, command.IdempotencyKey, StringComparison.Ordinal)
                || !string.Equals(command.PremiumJob.JobDigest, command.JobDigest, StringComparison.Ordinal)
                || !string.Equals(command.PremiumJob.PolicyDigest, Header.PolicyDigest, StringComparison.Ordinal)
                || !string.Equals(command.PremiumJob.PremiumModelRevisionIdentity, Header.PremiumModelRevisionIdentity, StringComparison.Ordinal))
                throw new InvalidDataException("Premium job identity binding mismatch.");
        }
        else if (command.PremiumJob is not null || command.ReservedMicrousd != 0)
            throw new InvalidDataException("Local admission cannot reserve premium cost.");
    }

    private FinancialJournalApplyStatus AdmissionDenial(AdmitAndReserveFinancialJournalCommand command)
    {
        int admittedJobs = _entries.Values.Count(e => !e.IsDenied);
        if (admittedJobs >= Header.MaximumJobs) return FinancialJournalApplyStatus.JobCapDenied;
        if (Header.Lane == CognitionLane.Local) return FinancialJournalApplyStatus.Admitted;
        if (_entries.Values.Count(IsOpenReservation) >= Header.MaximumOpenReservations) return FinancialJournalApplyStatus.OpenReservationCapDenied;
        long exposure = Exposure();
        long projected = FinancialJournalValidation.Add(exposure, command.ReservedMicrousd);
        if (projected > Header.RunCeilingMicrousd) return FinancialJournalApplyStatus.RunCeilingDenied;
        if (projected > Header.AccountCeilingMicrousd) return FinancialJournalApplyStatus.AccountCeilingDenied;
        return FinancialJournalApplyStatus.Admitted;
    }

    private FinancialJournalApplyResult PlanMark(MarkDispatchUnknownFinancialJournalCommand command)
    {
        FinancialEntry entry = Existing(command);
        if (entry.Version != command.ExpectedVersion) return Result(FinancialJournalApplyStatus.Conflict, entry, "version_conflict");
        if (entry.Phase != FinancialEntryPhase.Admitted || Header.Lane != CognitionLane.Premium) return Result(FinancialJournalApplyStatus.Conflict, entry, "state_conflict");
        return new(FinancialJournalApplyStatus.DispatchMarked, entry.Version + 1, null, "submission_unknown", AppendRequired: true);
    }

    private FinancialJournalApplyResult PlanComplete(CompleteFinancialJournalCommand command)
    {
        FinancialEntry entry = Existing(command);
        if (entry.Receipt is not null)
        {
            return entry.CompletionExpectedVersion == command.ExpectedVersion
                && string.Equals(FinancialJournalJson.ReceiptDigest(entry.Receipt), FinancialJournalJson.ReceiptDigest(command.Receipt), StringComparison.Ordinal)
                ? new(FinancialJournalApplyStatus.Replay, entry.Version, CloneReceipt(entry.Receipt), "receipt_replay")
                : Result(FinancialJournalApplyStatus.Conflict, entry, "receipt_conflict");
        }
        if (entry.Version != command.ExpectedVersion) return Result(FinancialJournalApplyStatus.Conflict, entry, "version_conflict");
        FinancialJournalValidation.Receipt(command.Receipt, Header, command.IdempotencyKey, command.JobDigest);
        if (command.Receipt.Reservation.ReservedMicrousd != entry.ReservedMicrousd)
            throw new InvalidDataException("Receipt reservation differs from admitted reservation.");
        ValidateCompletionTuple(entry, command.Receipt);
        if (Header.Lane == CognitionLane.Local)
        {
            if (command.Receipt.SubmissionState != SubmissionState.NotApplicable || command.Receipt.ChargeState != ChargeState.NotApplicable
                || command.Receipt.Reservation.ReservedMicrousd != 0 || command.Receipt.Reservation.SettledMicrousd != 0)
                throw new InvalidDataException("Local receipt contains premium submission or accounting state.");
        }
        else if (entry.IsDenied)
        {
            bool matchingDenial = string.Equals(command.Receipt.PrimaryOutcomeCode, entry.OutcomeCode, StringComparison.Ordinal)
                || (string.Equals(entry.OutcomeCode, "open_reservation_cap_denied", StringComparison.Ordinal)
                    && string.Equals(command.Receipt.PrimaryOutcomeCode, "capacity_denied", StringComparison.Ordinal));
            if (!matchingDenial || command.Receipt.SubmissionState != SubmissionState.DefinitelyNotSubmitted
                || command.Receipt.ChargeState != ChargeState.NotApplicable || command.Receipt.Reservation.ReservedMicrousd != 0
                || entry.PremiumJob is null
                || !string.Equals(command.Receipt.PremiumModelIdentity, entry.PremiumJob.PremiumModelIdentity, StringComparison.Ordinal)
                || !string.Equals(command.Receipt.Proposal.AgentId, entry.PremiumJob.Observation.AgentId, StringComparison.Ordinal))
                throw new InvalidDataException("Denied premium receipt is incoherent with its admission result.");
        }
        else
        {
            bool coherent = command.Receipt.ChargeState switch
            {
                ChargeState.Settled => command.Receipt.SubmissionState == SubmissionState.ResponseReceived,
                ChargeState.Released => command.Receipt.SubmissionState == SubmissionState.DefinitelyNotSubmitted && command.Receipt.Reservation.SettledMicrousd == 0,
                ChargeState.Unknown => (command.Receipt.SubmissionState is SubmissionState.SubmissionUnknown or SubmissionState.ResponseReceived) && command.Receipt.Reservation.SettledMicrousd == 0,
                _ => false
            };
            if (!coherent || entry.PremiumJob is null
                || !string.Equals(command.Receipt.PremiumModelIdentity, entry.PremiumJob.PremiumModelIdentity, StringComparison.Ordinal)
                || !string.Equals(command.Receipt.Proposal.AgentId, entry.PremiumJob.Observation.AgentId, StringComparison.Ordinal))
                throw new InvalidDataException("Premium receipt is incoherent with the immutable admitted job.");
        }
        if (entry.Phase == FinancialEntryPhase.Admitted && Header.Lane == CognitionLane.Premium)
            throw new InvalidDataException("Premium work must be dispatch-marked before completion.");
        return new(FinancialJournalApplyStatus.Completed, entry.Version + 1, CloneReceipt(command.Receipt), "completed", AppendRequired: true);
    }

    private void ValidateCompletionTuple(FinancialEntry entry, InferenceReceipt receipt)
    {
        bool deterministicIdle = string.Equals(receipt.ReasonCode, "deterministic_idle", StringComparison.Ordinal);
        if (deterministicIdle && (receipt.Proposal.Action != SnowGlobeActionKind.Idle || receipt.Proposal.Quantity != 0))
            throw new InvalidDataException("Deterministic-idle receipt contains a non-idle proposal.");

        if (Header.Lane == CognitionLane.Local)
        {
            bool localAccounting = receipt.SubmissionState == SubmissionState.NotApplicable && receipt.ChargeState == ChargeState.NotApplicable
                && receipt.Reservation.ReservedMicrousd == 0 && receipt.Reservation.SettledMicrousd == 0;
            bool localOutcome = entry.IsDenied
                ? deterministicIdle && string.Equals(receipt.PrimaryOutcomeCode, entry.OutcomeCode, StringComparison.Ordinal)
                : (string.Equals(receipt.ReasonCode, "local_success", StringComparison.Ordinal) && string.Equals(receipt.PrimaryOutcomeCode, "local_success", StringComparison.Ordinal))
                    || (deterministicIdle && receipt.PrimaryOutcomeCode is "local_invalid" or "local_error");
            if (!localAccounting || !localOutcome) throw new InvalidDataException("Local receipt tuple is source-impossible.");
            return;
        }

        bool fallbackReason = receipt.ReasonCode is "local_fallback" or "deterministic_idle";
        if (entry.IsDenied)
        {
            string expectedPrimary = string.Equals(entry.OutcomeCode, "open_reservation_cap_denied", StringComparison.Ordinal) ? "capacity_denied" : entry.OutcomeCode;
            if (!fallbackReason || !string.Equals(receipt.PrimaryOutcomeCode, expectedPrimary, StringComparison.Ordinal)
                || receipt.SubmissionState != SubmissionState.DefinitelyNotSubmitted || receipt.ChargeState != ChargeState.NotApplicable
                || receipt.Reservation.ReservedMicrousd != 0 || receipt.Reservation.SettledMicrousd != 0)
                throw new InvalidDataException("Denied premium receipt tuple is source-impossible.");
            return;
        }

        bool premiumTuple = receipt.ChargeState switch
        {
            ChargeState.Settled => receipt.SubmissionState == SubmissionState.ResponseReceived
                && string.Equals(receipt.ReasonCode, "premium_success", StringComparison.Ordinal)
                && string.Equals(receipt.PrimaryOutcomeCode, "premium_success", StringComparison.Ordinal),
            ChargeState.Released => receipt.SubmissionState == SubmissionState.DefinitelyNotSubmitted
                && receipt.Reservation.SettledMicrousd == 0 && fallbackReason
                && string.Equals(receipt.PrimaryOutcomeCode, "provider_rejected", StringComparison.Ordinal),
            ChargeState.Unknown => receipt.SubmissionState is SubmissionState.SubmissionUnknown or SubmissionState.ResponseReceived
                && receipt.Reservation.SettledMicrousd == 0 && fallbackReason
                && receipt.PrimaryOutcomeCode is "provider_rejected" or "provider_timeout" or "provider_malformed" or "provider_unknown",
            _ => false
        };
        if (!premiumTuple) throw new InvalidDataException("Premium receipt tuple is source-impossible.");
    }

    private FinancialJournalApplyResult PlanReconcile(ReconcileUnknownFinancialJournalCommand command)
    {
        if (Header.Lane != CognitionLane.Premium || Header.ByokAccountBinding is null
            || !string.Equals(command.AccountBinding.Value, Header.ByokAccountBinding.Value, StringComparison.Ordinal))
            throw new InvalidDataException("Reconciliation account binding mismatch.");
        if (command.AccountBinding is null) throw new InvalidDataException("Reconciliation account binding is null.");
        FinancialJournalValidation.ReconciliationEvidence(command.EvidenceIdentity, command.EvidenceDigest);
        if (!Enum.IsDefined(command.Outcome) || command.SettledMicrousd < 0) throw new InvalidDataException("Reconciliation facts are invalid.");
        FinancialEntry entry = Existing(command);
        if (entry.ReconciliationEvidenceIdentity is not null)
        {
            if (entry.ReconciliationExpectedVersion != command.ExpectedVersion)
                return Result(FinancialJournalApplyStatus.Conflict, entry, "version_conflict");
            bool same = string.Equals(entry.ReconciliationEvidenceIdentity, command.EvidenceIdentity, StringComparison.Ordinal)
                && string.Equals(entry.ReconciliationEvidenceDigest, command.EvidenceDigest, StringComparison.Ordinal)
                && entry.ReconciliationOutcome == command.Outcome && entry.EffectiveSettledMicrousd == command.SettledMicrousd;
            return same ? Result(FinancialJournalApplyStatus.ReconciliationReplay, entry, "reconciliation_replay") : Result(FinancialJournalApplyStatus.Conflict, entry, "reconciliation_conflict");
        }
        if (entry.Version != command.ExpectedVersion) return Result(FinancialJournalApplyStatus.Conflict, entry, "version_conflict");
        if (entry.Receipt is null || entry.Receipt.ChargeState != ChargeState.Unknown || entry.EffectiveChargeState != ChargeState.Unknown)
            return Result(FinancialJournalApplyStatus.Conflict, entry, "state_conflict");
        if (command.Outcome == FinancialReconciliationOutcome.Release && command.SettledMicrousd != 0)
            throw new InvalidDataException("Released reconciliation cannot settle cost.");
        if (command.Outcome == FinancialReconciliationOutcome.Settle && command.SettledMicrousd > entry.ReservedMicrousd)
            throw new InvalidDataException("Reconciliation exceeds the immutable reservation.");
        return new(FinancialJournalApplyStatus.Reconciled, entry.Version + 1, CloneReceipt(entry.Receipt), "reconciled", AppendRequired: true);
    }

    private FinancialEntry Existing(FinancialJournalCommand command)
    {
        if (!_entries.TryGetValue(command.IdempotencyKey, out FinancialEntry? entry)) throw new InvalidDataException("Financial Journal job does not exist.");
        if (!string.Equals(entry.JobDigest, command.JobDigest, StringComparison.Ordinal)) throw new InvalidDataException("Financial Journal job digest mismatch.");
        return entry;
    }

    internal void ApplyRecord(FinancialJournalCommand command, FinancialJournalApplyResult planned, string checksum, HashSet<string> activeKeys)
    {
        switch (command)
        {
            case AdmitAndReserveFinancialJournalCommand admit:
                if (_entries.ContainsKey(admit.IdempotencyKey)) break;
                FinancialEntry entry = new(admit.IdempotencyKey, admit.JobDigest, planned.Version, admit.ReservedMicrousd,
                    planned.Status == FinancialJournalApplyStatus.Admitted ? admit.ReservedMicrousd : 0,
                    planned.Status == FinancialJournalApplyStatus.Admitted ? FinancialEntryPhase.Admitted : FinancialEntryPhase.Denied,
                    Header.Lane == CognitionLane.Premium ? SubmissionState.DefinitelyNotSubmitted : SubmissionState.NotApplicable,
                    Header.Lane == CognitionLane.Premium && planned.Status == FinancialJournalApplyStatus.Admitted ? ChargeState.Reserved : ChargeState.NotApplicable,
                    planned.OutcomeCode, admit.PremiumJob is null ? null : CloneJob(admit.PremiumJob));
                _entries.Add(admit.IdempotencyKey, entry);
                if (planned.Status == FinancialJournalApplyStatus.Admitted) activeKeys.Add(admit.IdempotencyKey);
                _trace.Add(planned.Status == FinancialJournalApplyStatus.Admitted ? (Header.Lane == CognitionLane.Premium ? "reserved" : "local_admitted") : planned.OutcomeCode);
                break;
            case MarkDispatchUnknownFinancialJournalCommand mark:
                FinancialEntry marked = _entries[mark.IdempotencyKey];
                marked.Version = planned.Version; marked.Phase = FinancialEntryPhase.DispatchUnknown;
                marked.SubmissionState = SubmissionState.SubmissionUnknown; marked.EffectiveChargeState = ChargeState.Unknown;
                _trace.Add("submission_unknown");
                break;
            case CompleteFinancialJournalCommand complete:
                FinancialEntry completed = _entries[complete.IdempotencyKey];
                completed.Version = planned.Version; completed.Phase = FinancialEntryPhase.Completed;
                completed.CompletionExpectedVersion = complete.ExpectedVersion;
                completed.Receipt = CloneReceipt(complete.Receipt); completed.SubmissionState = complete.Receipt.SubmissionState;
                completed.EffectiveChargeState = complete.Receipt.ChargeState; completed.EffectiveSettledMicrousd = complete.Receipt.Reservation.SettledMicrousd;
                completed.OutcomeCode = complete.Receipt.PrimaryOutcomeCode; activeKeys.Remove(complete.IdempotencyKey);
                _trace.Add("completed/" + complete.Receipt.ReasonCode);
                break;
            case ReconcileUnknownFinancialJournalCommand reconcile:
                FinancialEntry reconciled = _entries[reconcile.IdempotencyKey];
                reconciled.Version = planned.Version;
                reconciled.EffectiveChargeState = reconcile.Outcome == FinancialReconciliationOutcome.Release ? ChargeState.Released : ChargeState.Settled;
                reconciled.EffectiveSettledMicrousd = reconcile.SettledMicrousd;
                reconciled.ReconciliationEvidenceIdentity = reconcile.EvidenceIdentity;
                reconciled.ReconciliationEvidenceDigest = reconcile.EvidenceDigest;
                reconciled.ReconciliationOutcome = reconcile.Outcome;
                reconciled.ReconciliationExpectedVersion = reconcile.ExpectedVersion;
                _trace.Add("reconciled/" + (reconcile.Outcome == FinancialReconciliationOutcome.Release ? "released" : "settled"));
                break;
        }
        RecordCount++;
        PreviousChecksum = checksum;
    }

    internal FinancialJournalReadResult Read(FinancialJournalPageQuery query)
    {
        if (query.Offset < 0 || query.Limit < 0 || query.Limit > FinancialJournalBounds.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(query));
        FinancialJournalEntrySnapshot[] entries = _entries.Values.OrderBy(e => e.IdempotencyKey, StringComparer.Ordinal)
            .Skip(query.Offset).Take(query.Limit).Select(e => e.Snapshot()).ToArray();
        return new(Header with { ByokAccountBinding = Header.ByokAccountBinding is null ? null : new(Header.ByokAccountBinding.Value) },
            new ReadOnlyCollection<FinancialJournalEntrySnapshot>(entries), RecordCount, new ReadOnlyCollection<string>(_trace.ToArray()));
    }

    internal FinancialJournalReadResult ReadSnapshot()
    {
        FinancialJournalEntrySnapshot[] entries = _entries.Values.OrderBy(e => e.IdempotencyKey, StringComparer.Ordinal).Select(e => e.Snapshot()).ToArray();
        return Detached(entries);
    }

    private FinancialJournalReadResult Detached(FinancialJournalEntrySnapshot[] entries) =>
        new(Header with { ByokAccountBinding = Header.ByokAccountBinding is null ? null : new(Header.ByokAccountBinding.Value) },
            new ReadOnlyCollection<FinancialJournalEntrySnapshot>(entries), RecordCount, new ReadOnlyCollection<string>(_trace.ToArray()));

    private long Exposure()
    {
        long total = 0;
        foreach (FinancialEntry entry in _entries.Values)
        {
            long amount = entry.EffectiveChargeState switch
            {
                ChargeState.Reserved or ChargeState.Unknown => entry.ReservedMicrousd,
                ChargeState.Settled => entry.EffectiveSettledMicrousd,
                _ => 0
            };
            total = FinancialJournalValidation.Add(total, amount);
        }
        return total;
    }

    private bool CanFitFutureRecords(int additionalRecords)
    {
        long obligations = 0;
        foreach (FinancialEntry entry in _entries.Values)
        {
            int entryObligations = entry.Phase switch
            {
                FinancialEntryPhase.Denied when entry.Receipt is null => 1,
                FinancialEntryPhase.Admitted when Header.Lane == CognitionLane.Local => 1,
                FinancialEntryPhase.Admitted => 3,
                FinancialEntryPhase.DispatchUnknown => 2,
                FinancialEntryPhase.Completed when entry.EffectiveChargeState == ChargeState.Unknown
                    && entry.ReconciliationEvidenceIdentity is null => 1,
                _ => 0
            };
            obligations = FinancialJournalValidation.Add(obligations, entryObligations);
        }
        long required = FinancialJournalValidation.Add(FinancialJournalValidation.Add(RecordCount, obligations), additionalRecords);
        return required <= FinancialJournalBounds.MaximumRecords;
    }

    private static bool IsOpenReservation(FinancialEntry entry) => entry.EffectiveChargeState is ChargeState.Reserved or ChargeState.Unknown;
    private static FinancialJournalApplyResult Result(FinancialJournalApplyStatus status, FinancialEntry entry, string outcome) => new(status, entry.Version, entry.Receipt is null ? null : CloneReceipt(entry.Receipt), outcome);
    private static FinancialJournalApplyStatus StatusForDenial(string outcome) => outcome switch
    {
        "job_cap_denied" => FinancialJournalApplyStatus.JobCapDenied,
        "run_ceiling_denied" => FinancialJournalApplyStatus.RunCeilingDenied,
        "account_ceiling_denied" => FinancialJournalApplyStatus.AccountCeilingDenied,
        "open_reservation_cap_denied" => FinancialJournalApplyStatus.OpenReservationCapDenied,
        "record_headroom_denied" => FinancialJournalApplyStatus.RecordHeadroomDenied,
        _ => throw new InvalidDataException("Unknown admission denial outcome.")
    };
    private static string OutcomeFor(FinancialJournalApplyStatus status) => status switch
    {
        FinancialJournalApplyStatus.Admitted => "admitted",
        FinancialJournalApplyStatus.JobCapDenied => "job_cap_denied",
        FinancialJournalApplyStatus.RunCeilingDenied => "run_ceiling_denied",
        FinancialJournalApplyStatus.AccountCeilingDenied => "account_ceiling_denied",
        FinancialJournalApplyStatus.OpenReservationCapDenied => "open_reservation_cap_denied",
        FinancialJournalApplyStatus.RecordHeadroomDenied => "record_headroom_denied",
        _ => throw new InvalidDataException("Unexpected admission status.")
    };
    internal static InferenceReceipt CloneReceipt(InferenceReceipt value) => value with { Reservation = value.Reservation with { }, Proposal = value.Proposal with { } };
    internal static PremiumCognitionJob CloneJob(PremiumCognitionJob value) => value with { Observation = value.Observation with { } };
}

internal sealed class FinancialEntry
{
    internal FinancialEntry(string key, string digest, long version, long requestedReserved, long reserved, FinancialEntryPhase phase, SubmissionState submission, ChargeState charge, string outcome, PremiumCognitionJob? job)
    { IdempotencyKey = key; JobDigest = digest; Version = version; RequestedReservedMicrousd = requestedReserved; ReservedMicrousd = reserved; Phase = phase; IsDenied = phase == FinancialEntryPhase.Denied; SubmissionState = submission; EffectiveChargeState = charge; OutcomeCode = outcome; PremiumJob = job; }
    internal string IdempotencyKey { get; }
    internal string JobDigest { get; }
    internal long Version { get; set; }
    internal long RequestedReservedMicrousd { get; }
    internal long ReservedMicrousd { get; }
    internal long EffectiveSettledMicrousd { get; set; }
    internal FinancialEntryPhase Phase { get; set; }
    internal bool IsDenied { get; }
    internal SubmissionState SubmissionState { get; set; }
    internal ChargeState EffectiveChargeState { get; set; }
    internal string OutcomeCode { get; set; }
    internal PremiumCognitionJob? PremiumJob { get; }
    internal InferenceReceipt? Receipt { get; set; }
    internal string? ReconciliationEvidenceIdentity { get; set; }
    internal string? ReconciliationEvidenceDigest { get; set; }
    internal FinancialReconciliationOutcome? ReconciliationOutcome { get; set; }
    internal long? ReconciliationExpectedVersion { get; set; }
    internal long? CompletionExpectedVersion { get; set; }
    internal FinancialJournalEntrySnapshot Snapshot() => new(IdempotencyKey, JobDigest, Version, ReservedMicrousd,
        EffectiveSettledMicrousd, SubmissionState, EffectiveChargeState, OutcomeCode,
        PremiumJob is null ? null : FinancialJournalState.CloneJob(PremiumJob),
        Receipt is null ? null : FinancialJournalState.CloneReceipt(Receipt), ReconciliationEvidenceIdentity, ReconciliationEvidenceDigest);
}

/// <summary>In-memory Adapter using the exact durable transition reducer.</summary>
public sealed class InMemoryCognitionJobJournal : IFinancialJournal, ICognitionReceiptInspector
{
    private readonly object _gate = new();
    private readonly int _legacyPremiumCapacity;
    private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);
    private FinancialJournalState? _state;
    private bool _disposed;

    public InMemoryCognitionJobJournal(int premiumCapacity = 64)
    {
        if (premiumCapacity < 0 || premiumCapacity > FinancialJournalBounds.MaximumRecords) throw new ArgumentOutOfRangeException(nameof(premiumCapacity));
        _legacyPremiumCapacity = premiumCapacity;
    }

    public InMemoryCognitionJobJournal(FinancialJournalHeader header)
    {
        ArgumentNullException.ThrowIfNull(header); header.Validate();
        _legacyPremiumCapacity = header.MaximumOpenReservations;
        _state = new FinancialJournalState(header);
    }

    internal void BindLegacy(FinancialJournalHeader header)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_state is null) { _state = new FinancialJournalState(header); return; }
            if (!string.Equals(_state.Header.HeaderChecksum, header.HeaderChecksum, StringComparison.Ordinal))
                throw new InvalidOperationException("Financial Journal is already bound to different immutable header facts.");
        }
    }

    internal int LegacyPremiumCapacity => _legacyPremiumCapacity;

    public FinancialJournalApplyResult Apply(FinancialJournalCommand command)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FinancialJournalState state = _state ?? throw new InvalidOperationException("Financial Journal is not bound.");
            FinancialJournalApplyResult planned = state.Plan(command, _activeKeys);
            if (!RequiresRecord(command, planned)) return planned;
            string checksum = CognitionCanonical.Digest($"memory|{state.RecordCount + 1}|{state.PreviousChecksum}|{planned.Status}|{command.IdempotencyKey}|{command.JobDigest}");
            state.ApplyRecord(command, planned, checksum, _activeKeys);
            return planned;
        }
    }

    public FinancialJournalReadResult Read(FinancialJournalQuery query)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FinancialJournalState state = _state ?? throw new InvalidOperationException("Financial Journal is not bound.");
            return query switch { FinancialJournalPageQuery page => state.Read(page), FinancialJournalSnapshotQuery => state.ReadSnapshot(), _ => throw new ArgumentException("Unsupported Financial Journal query.", nameof(query)) };
        }
    }

    public IReadOnlyList<InferenceReceipt> SnapshotReceipts() => Read(new FinancialJournalSnapshotQuery()).Entries.Where(e => e.Receipt is not null).Select(e => FinancialJournalState.CloneReceipt(e.Receipt!)).ToArray();
    public IReadOnlyList<PremiumCognitionJob> SnapshotPremiumJobs() => Read(new FinancialJournalSnapshotQuery()).Entries.Where(e => e.PremiumJob is not null).Select(e => FinancialJournalState.CloneJob(e.PremiumJob!)).ToArray();
    public IReadOnlyList<string> SnapshotTrace() => Read(new FinancialJournalSnapshotQuery()).Trace;
    public void Dispose() { lock (_gate) _disposed = true; }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(InMemoryCognitionJobJournal)); }
    internal static bool RequiresRecord(FinancialJournalCommand command, FinancialJournalApplyResult result) => result.AppendRequired;
}

public interface IFileFinancialJournalAppendFault
{
    void BeforeWrite(int sequence);
    void AfterWriteBeforeFlush(int sequence);
}

/// <summary>Strict, bounded, single-live-writer JSON/JSONL Adapter.</summary>
public sealed class FileFinancialJournal : IFinancialJournal, ICognitionReceiptInspector
{
    public const string HeaderFileName = "header.json";
    public const string RecordsFileName = "records.jsonl";
    public const string WriterLeaseFileName = "writer.lock";
    private readonly object _gate = new();
    private readonly FileStream _lease;
    private readonly FileStream _records;
    private readonly IFileFinancialJournalAppendFault? _fault;
    private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);
    private FinancialJournalState _state;
    private bool _poisoned;
    private bool _disposed;

    private FileFinancialJournal(string directory, FinancialJournalState state, FileStream lease, FileStream records, IFileFinancialJournalAppendFault? fault)
    { DirectoryPath = Path.GetFullPath(directory); _state = state; _lease = lease; _records = records; _fault = fault; }

    public string DirectoryPath { get; }
    public bool IsPoisoned { get { lock (_gate) return _poisoned; } }

    public static FileFinancialJournal CreateNew(string directory, FinancialJournalHeader header, IFileFinancialJournalAppendFault? fault = null)
    {
        ArgumentNullException.ThrowIfNull(directory); ArgumentNullException.ThrowIfNull(header); header.Validate();
        string root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        if (Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("Financial Journal directory must be empty.");
        byte[] headerBytes = FinancialJournalJson.WriteHeader(header);
        if (headerBytes.Length > FinancialJournalBounds.MaximumHeaderBytes) throw new InvalidDataException("Financial Journal header is oversized.");
        FileStream? lease = null; FileStream? records = null;
        try
        {
            lease = new FileStream(Path.Combine(root, WriterLeaseFileName), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough);
            using (FileStream headerFile = new(Path.Combine(root, HeaderFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            { headerFile.Write(headerBytes); headerFile.Flush(flushToDisk: true); }
            using (FileStream empty = new(Path.Combine(root, RecordsFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1, FileOptions.WriteThrough))
                empty.Flush(flushToDisk: true);
            records = new FileStream(Path.Combine(root, RecordsFileName), FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            return new FileFinancialJournal(root, new FinancialJournalState(header), lease, records, fault);
        }
        catch { records?.Dispose(); lease?.Dispose(); throw; }
    }

    public static FileFinancialJournal OpenForAppend(string directory, IFileFinancialJournalAppendFault? fault = null)
    {
        string root = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
        FileStream? lease = null; FileStream? records = null;
        try
        {
            lease = new FileStream(Path.Combine(root, WriterLeaseFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough);
            FinancialJournalState state = FinancialJournalJson.Load(root);
            records = new FileStream(Path.Combine(root, RecordsFileName), FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
            records.Seek(0, SeekOrigin.End);
            return new FileFinancialJournal(root, state, lease, records, fault);
        }
        catch { records?.Dispose(); lease?.Dispose(); throw; }
    }

    public static FinancialJournalReadResult ReadArchive(string directory, FinancialJournalPageQuery? query = null)
    {
        FinancialJournalState state = FinancialJournalJson.Load(Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory))));
        return query is null ? state.ReadSnapshot() : state.Read(query);
    }

    public FinancialJournalApplyResult Apply(FinancialJournalCommand command)
    {
        lock (_gate)
        {
            ThrowIfUnavailable();
            FinancialJournalApplyResult planned = _state.Plan(command, _activeKeys);
            if (!InMemoryCognitionJobJournal.RequiresRecord(command, planned)) return planned;
            int sequence = _state.RecordCount + 1;
            byte[] record = FinancialJournalJson.WriteRecord(sequence, _state.PreviousChecksum, _state.Header.HeaderChecksum, command, planned);
            if (record.Length - 1 > FinancialJournalBounds.MaximumRecordBytes) throw new InvalidDataException("Financial Journal record is oversized.");
            if (sequence > FinancialJournalBounds.MaximumRecords) throw new InvalidDataException("Financial Journal record count limit reached.");
            if (FinancialJournalValidation.Add(_records.Length, record.Length) > FinancialJournalBounds.MaximumTotalBytes) throw new InvalidDataException("Financial Journal total byte limit reached.");
            string checksum = FinancialJournalJson.RecordChecksum(record.AsSpan(0, record.Length - 1));
            try
            {
                _fault?.BeforeWrite(sequence);
                _records.Write(record);
                _fault?.AfterWriteBeforeFlush(sequence);
                _records.Flush(flushToDisk: true);
                _state.ApplyRecord(command, planned, checksum, _activeKeys);
            }
            catch { _poisoned = true; throw; }
            return planned;
        }
    }

    public FinancialJournalReadResult Read(FinancialJournalQuery query)
    {
        lock (_gate)
        {
            ThrowIfUnavailable();
            return query switch { FinancialJournalPageQuery page => _state.Read(page), FinancialJournalSnapshotQuery => _state.ReadSnapshot(), _ => throw new ArgumentException("Unsupported Financial Journal query.", nameof(query)) };
        }
    }

    public IReadOnlyList<InferenceReceipt> SnapshotReceipts() => Read(new FinancialJournalSnapshotQuery()).Entries.Where(e => e.Receipt is not null).Select(e => FinancialJournalState.CloneReceipt(e.Receipt!)).ToArray();
    public IReadOnlyList<PremiumCognitionJob> SnapshotPremiumJobs() => Read(new FinancialJournalSnapshotQuery()).Entries.Where(e => e.PremiumJob is not null).Select(e => FinancialJournalState.CloneJob(e.PremiumJob!)).ToArray();
    public IReadOnlyList<string> SnapshotTrace() => Read(new FinancialJournalSnapshotQuery()).Trace;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try { _records.Dispose(); }
            finally { _lease.Dispose(); }
        }
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileFinancialJournal));
        if (_poisoned) throw new InvalidOperationException("Financial Journal writer is poisoned after uncertain append/flush outcome.");
    }
}

internal static class FinancialJournalJson
{
    private const string RecordSchema = "snow_globe_financial_journal_record/v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static string HeaderChecksum(FinancialJournalHeader header) => CognitionCanonical.Digest(StrictUtf8.GetString(WriteHeaderCore(header, includeChecksum: false)));
    internal static byte[] WriteHeader(FinancialJournalHeader header) => WriteHeaderCore(header, includeChecksum: true);

    private static byte[] WriteHeaderCore(FinancialJournalHeader header, bool includeChecksum)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", header.Schema);
            writer.WriteString("financial_journal_identity", header.FinancialJournalIdentity);
            writer.WriteString("financial_run_identity", header.FinancialRunIdentity);
            writer.WriteString("lane", header.Lane == CognitionLane.Premium ? "premium" : "local");
            writer.WriteString("policy_digest", header.PolicyDigest);
            writer.WriteString("premium_model_revision_identity", header.PremiumModelRevisionIdentity);
            if (header.ByokAccountBinding is null) writer.WriteNull("byok_account_binding_identity"); else writer.WriteString("byok_account_binding_identity", header.ByokAccountBinding.Value);
            writer.WriteNumber("run_ceiling_microusd", header.RunCeilingMicrousd);
            writer.WriteNumber("account_ceiling_microusd", header.AccountCeilingMicrousd);
            writer.WriteNumber("maximum_jobs", header.MaximumJobs);
            writer.WriteNumber("maximum_open_reservations", header.MaximumOpenReservations);
            if (includeChecksum) writer.WriteString("header_checksum", header.HeaderChecksum);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static FinancialJournalState Load(string root)
    {
        string headerPath = Path.Combine(root, FileFinancialJournal.HeaderFileName);
        string recordsPath = Path.Combine(root, FileFinancialJournal.RecordsFileName);
        byte[] headerBytes = ReadBounded(headerPath, FinancialJournalBounds.MaximumHeaderBytes);
        if (HasBom(headerBytes)) throw new InvalidDataException("Financial Journal header must be UTF-8 without BOM.");
        FinancialJournalHeader header = ParseHeader(headerBytes);
        byte[] recordsBytes = ReadBounded(recordsPath, FinancialJournalBounds.MaximumTotalBytes);
        if (HasBom(recordsBytes)) throw new InvalidDataException("Financial Journal records must be UTF-8 without BOM.");
        FinancialJournalState state = new(header);
        HashSet<string> loadingKeys = new(StringComparer.Ordinal);
        if (recordsBytes.Length == 0) return state;
        if (recordsBytes[^1] != (byte)'\n') throw new InvalidDataException("Financial Journal record tail is unterminated.");
        int start = 0;
        while (start < recordsBytes.Length)
        {
            int end = Array.IndexOf(recordsBytes, (byte)'\n', start);
            if (end < 0) throw new InvalidDataException("Financial Journal record tail is unterminated.");
            int length = end - start;
            if (length <= 0 || length > FinancialJournalBounds.MaximumRecordBytes) throw new InvalidDataException("Financial Journal record length is invalid.");
            if (state.RecordCount >= FinancialJournalBounds.MaximumRecords) throw new InvalidDataException("Financial Journal record count is oversized.");
            ReadOnlySpan<byte> line = recordsBytes.AsSpan(start, length);
            ParsedRecord parsed = ParseRecord(line, state.RecordCount + 1, state.PreviousChecksum, header);
            FinancialJournalApplyResult planned = state.Plan(parsed.Command, loadingKeys, loading: true);
            if (planned.Status != parsed.Status || planned.Version != parsed.Version || !InMemoryCognitionJobJournal.RequiresRecord(parsed.Command, planned))
                throw new InvalidDataException("Financial Journal record is incoherent with prior state or caps.");
            state.ApplyRecord(parsed.Command, planned, parsed.Checksum, loadingKeys);
            loadingKeys.Clear();
            start = end + 1;
        }
        return state;
    }

    internal static byte[] WriteRecord(int sequence, string previousChecksum, string headerChecksum, FinancialJournalCommand command, FinancialJournalApplyResult result)
    {
        byte[] withoutChecksum = WriteRecordCore(sequence, previousChecksum, headerChecksum, command, result, null);
        string checksum = CognitionCanonical.Digest(StrictUtf8.GetString(withoutChecksum));
        byte[] final = WriteRecordCore(sequence, previousChecksum, headerChecksum, command, result, checksum);
        byte[] withLf = new byte[final.Length + 1]; final.CopyTo(withLf, 0); withLf[^1] = (byte)'\n'; return withLf;
    }

    internal static string RecordChecksum(ReadOnlySpan<byte> completeRecord)
    {
        using JsonDocument document = ParseDocument(completeRecord);
        JsonElement root = document.RootElement;
        string checksum = RequiredString(root, "record_checksum");
        return checksum;
    }

    private static byte[] WriteRecordCore(int sequence, string previousChecksum, string headerChecksum, FinancialJournalCommand command, FinancialJournalApplyResult result, string? checksum)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject(); writer.WriteString("schema", RecordSchema); writer.WriteNumber("sequence", sequence);
            writer.WriteString("kind", Kind(command)); writer.WriteString("previous_checksum", previousChecksum); writer.WriteString("header_checksum", headerChecksum);
            writer.WriteNumber("version", result.Version); writer.WriteString("status", Status(result.Status));
            writer.WritePropertyName("payload"); WriteCommand(writer, command);
            if (checksum is not null) writer.WriteString("record_checksum", checksum);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void WriteCommand(Utf8JsonWriter writer, FinancialJournalCommand command)
    {
        writer.WriteStartObject(); writer.WriteString("idempotency_key", command.IdempotencyKey); writer.WriteString("job_digest", command.JobDigest);
        switch (command)
        {
            case AdmitAndReserveFinancialJournalCommand admit:
                writer.WriteNumber("reserved_microusd", admit.ReservedMicrousd);
                writer.WritePropertyName("premium_job"); if (admit.PremiumJob is null) writer.WriteNullValue(); else WriteJob(writer, admit.PremiumJob);
                break;
            case MarkDispatchUnknownFinancialJournalCommand mark: writer.WriteNumber("expected_version", mark.ExpectedVersion); break;
            case CompleteFinancialJournalCommand complete:
                writer.WriteNumber("expected_version", complete.ExpectedVersion); writer.WritePropertyName("receipt"); WriteReceipt(writer, complete.Receipt); break;
            case ReconcileUnknownFinancialJournalCommand reconcile:
                writer.WriteNumber("expected_version", reconcile.ExpectedVersion); writer.WriteString("account_binding_identity", reconcile.AccountBinding.Value);
                writer.WriteString("evidence_identity", reconcile.EvidenceIdentity); writer.WriteString("evidence_digest", reconcile.EvidenceDigest);
                writer.WriteString("outcome", reconcile.Outcome == FinancialReconciliationOutcome.Release ? "release" : "settle"); writer.WriteNumber("settled_microusd", reconcile.SettledMicrousd); break;
        }
        writer.WriteEndObject();
    }

    private static void WriteJob(Utf8JsonWriter writer, PremiumCognitionJob job)
    {
        writer.WriteStartObject(); writer.WriteString("idempotency_key", job.IdempotencyKey); writer.WriteString("job_digest", job.JobDigest);
        writer.WriteString("policy_digest", job.PolicyDigest); writer.WriteString("premium_model_identity", job.PremiumModelIdentity);
        writer.WriteString("premium_model_revision_identity", job.PremiumModelRevisionIdentity); writer.WritePropertyName("observation"); WriteObservation(writer, job.Observation); writer.WriteEndObject();
    }

    private static void WriteObservation(Utf8JsonWriter writer, SnowGlobeObservation o)
    {
        writer.WriteStartObject(); writer.WriteString("agent_id", o.AgentId); writer.WriteNumber("home_slot", o.HomeSlot); writer.WriteNumber("tick", o.Tick);
        writer.WriteNumber("available_wood", o.AvailableWood); writer.WriteNumber("available_stone", o.AvailableStone); writer.WriteNumber("stockpile_wood", o.StockpileWood);
        writer.WriteNumber("stockpile_stone", o.StockpileStone); writer.WriteNumber("shelter_count", o.ShelterCount); writer.WriteNumber("storage_count", o.StorageCount); writer.WriteEndObject();
    }

    private static void WriteReceipt(Utf8JsonWriter writer, InferenceReceipt r)
    {
        writer.WriteStartObject(); writer.WriteString("idempotency_key", r.IdempotencyKey); writer.WriteString("job_digest", r.JobDigest); writer.WriteString("policy_digest", r.PolicyDigest);
        writer.WriteString("financial_journal_identity", r.FinancialJournalIdentity); writer.WriteString("requested_lane", r.RequestedLane == CognitionLane.Premium ? "premium" : "local");
        writer.WriteString("premium_model_identity", r.PremiumModelIdentity); writer.WriteString("premium_model_revision_identity", r.PremiumModelRevisionIdentity);
        writer.WriteString("submission_state", r.SubmissionState.ToString()); writer.WriteString("charge_state", r.ChargeState.ToString());
        writer.WriteNumber("reserved_microusd", r.Reservation.ReservedMicrousd); writer.WriteNumber("settled_microusd", r.Reservation.SettledMicrousd);
        writer.WriteString("reason_code", r.ReasonCode); writer.WriteString("primary_outcome_code", r.PrimaryOutcomeCode);
        writer.WritePropertyName("proposal"); writer.WriteStartObject(); writer.WriteString("agent_id", r.Proposal.AgentId); writer.WriteString("action", r.Proposal.Action.ToString()); writer.WriteNumber("quantity", r.Proposal.Quantity); writer.WriteEndObject(); writer.WriteEndObject();
    }

    internal static string ReceiptDigest(InferenceReceipt receipt)
    {
        using MemoryStream stream = new(); using (Utf8JsonWriter writer = new(stream)) WriteReceipt(writer, receipt);
        return CognitionCanonical.Digest(StrictUtf8.GetString(stream.ToArray()));
    }

    private static FinancialJournalHeader ParseHeader(byte[] bytes)
    {
        using JsonDocument document = ParseDocument(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema", "financial_journal_identity", "financial_run_identity", "lane", "policy_digest", "premium_model_revision_identity", "byok_account_binding_identity", "run_ceiling_microusd", "account_ceiling_microusd", "maximum_jobs", "maximum_open_reservations", "header_checksum");
        string? binding = OptionalString(root, "byok_account_binding_identity");
        FinancialJournalHeader header = new(RequiredString(root, "schema"), RequiredString(root, "financial_journal_identity"), RequiredString(root, "financial_run_identity"),
            ParseLane(RequiredString(root, "lane")), RequiredString(root, "policy_digest"), RequiredString(root, "premium_model_revision_identity"), binding is null ? null : new(binding),
            RequiredInt64(root, "run_ceiling_microusd"), RequiredInt64(root, "account_ceiling_microusd"), RequiredInt32(root, "maximum_jobs"), RequiredInt32(root, "maximum_open_reservations"), RequiredString(root, "header_checksum"));
        header.Validate();
        if (!bytes.AsSpan().SequenceEqual(WriteHeader(header))) throw new InvalidDataException("Financial Journal header is not canonical JSON.");
        return header;
    }

    private static ParsedRecord ParseRecord(ReadOnlySpan<byte> bytes, int expectedSequence, string expectedPrevious, FinancialJournalHeader header)
    {
        using JsonDocument document = ParseDocument(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema", "sequence", "kind", "previous_checksum", "header_checksum", "version", "status", "payload", "record_checksum");
        if (!string.Equals(RequiredString(root, "schema"), RecordSchema, StringComparison.Ordinal) || RequiredInt32(root, "sequence") != expectedSequence
            || !string.Equals(RequiredString(root, "previous_checksum"), expectedPrevious, StringComparison.Ordinal)
            || !string.Equals(RequiredString(root, "header_checksum"), header.HeaderChecksum, StringComparison.Ordinal))
            throw new InvalidDataException("Financial Journal record chain identity mismatch.");
        string checksum = RequiredString(root, "record_checksum"); FinancialJournalValidation.Digest(checksum, "record_checksum");
        string kind = RequiredString(root, "kind"); FinancialJournalCommand command = ParseCommand(kind, root.GetProperty("payload"));
        FinancialJournalApplyStatus status = ParseStatus(RequiredString(root, "status")); long version = RequiredInt64(root, "version");
        FinancialJournalApplyResult synthetic = new(status, version, command is CompleteFinancialJournalCommand c ? c.Receipt : null, OutcomeForParsed(status));
        byte[] canonical = WriteRecord(expectedSequence, expectedPrevious, header.HeaderChecksum, command, synthetic);
        ReadOnlySpan<byte> canonicalLine = canonical.AsSpan(0, canonical.Length - 1);
        if (!bytes.SequenceEqual(canonicalLine)) throw new InvalidDataException("Financial Journal record is not canonical or checksum-valid.");
        return new(command, status, version, checksum);
    }

    private static FinancialJournalCommand ParseCommand(string kind, JsonElement payload)
    {
        string key = RequiredString(payload, "idempotency_key"); string digest = RequiredString(payload, "job_digest");
        return kind switch
        {
            "admit" => ParseAdmit(payload, key, digest),
            "dispatch_unknown" => ParseMark(payload, key, digest),
            "complete" => ParseComplete(payload, key, digest),
            "reconcile" => ParseReconcile(payload, key, digest),
            _ => throw new InvalidDataException("Unknown Financial Journal record kind.")
        };
    }

    private static FinancialJournalCommand ParseAdmit(JsonElement p, string key, string digest)
    { Exact(p, "idempotency_key", "job_digest", "reserved_microusd", "premium_job"); return new AdmitAndReserveFinancialJournalCommand(key, digest, p.GetProperty("premium_job").ValueKind == JsonValueKind.Null ? null : ParseJob(p.GetProperty("premium_job")), RequiredInt64(p, "reserved_microusd")); }
    private static FinancialJournalCommand ParseMark(JsonElement p, string key, string digest)
    { Exact(p, "idempotency_key", "job_digest", "expected_version"); return new MarkDispatchUnknownFinancialJournalCommand(key, digest, RequiredInt64(p, "expected_version")); }
    private static FinancialJournalCommand ParseComplete(JsonElement p, string key, string digest)
    { Exact(p, "idempotency_key", "job_digest", "expected_version", "receipt"); return new CompleteFinancialJournalCommand(key, digest, RequiredInt64(p, "expected_version"), ParseReceipt(p.GetProperty("receipt"))); }
    private static FinancialJournalCommand ParseReconcile(JsonElement p, string key, string digest)
    {
        Exact(p, "idempotency_key", "job_digest", "expected_version", "account_binding_identity", "evidence_identity", "evidence_digest", "outcome", "settled_microusd");
        string outcome = RequiredString(p, "outcome"); return new ReconcileUnknownFinancialJournalCommand(key, digest, RequiredInt64(p, "expected_version"), new(RequiredString(p, "account_binding_identity")),
            RequiredString(p, "evidence_identity"), RequiredString(p, "evidence_digest"), outcome == "release" ? FinancialReconciliationOutcome.Release : outcome == "settle" ? FinancialReconciliationOutcome.Settle : throw new InvalidDataException("Unknown reconciliation outcome."), RequiredInt64(p, "settled_microusd"));
    }

    private static PremiumCognitionJob ParseJob(JsonElement p)
    {
        Exact(p, "idempotency_key", "job_digest", "policy_digest", "premium_model_identity", "premium_model_revision_identity", "observation");
        return new(RequiredString(p, "idempotency_key"), RequiredString(p, "job_digest"), RequiredString(p, "policy_digest"), RequiredString(p, "premium_model_identity"), RequiredString(p, "premium_model_revision_identity"), ParseObservation(p.GetProperty("observation")));
    }
    private static SnowGlobeObservation ParseObservation(JsonElement p)
    {
        Exact(p, "agent_id", "home_slot", "tick", "available_wood", "available_stone", "stockpile_wood", "stockpile_stone", "shelter_count", "storage_count");
        return new(RequiredString(p, "agent_id"), RequiredInt32(p, "home_slot"), RequiredInt32(p, "tick"), RequiredInt32(p, "available_wood"), RequiredInt32(p, "available_stone"), RequiredInt32(p, "stockpile_wood"), RequiredInt32(p, "stockpile_stone"), RequiredInt32(p, "shelter_count"), RequiredInt32(p, "storage_count"));
    }
    private static InferenceReceipt ParseReceipt(JsonElement p)
    {
        Exact(p, "idempotency_key", "job_digest", "policy_digest", "financial_journal_identity", "requested_lane", "premium_model_identity", "premium_model_revision_identity", "submission_state", "charge_state", "reserved_microusd", "settled_microusd", "reason_code", "primary_outcome_code", "proposal");
        JsonElement proposal = p.GetProperty("proposal"); Exact(proposal, "agent_id", "action", "quantity");
        if (!Enum.TryParse(RequiredString(p, "submission_state"), false, out SubmissionState submission) || !Enum.IsDefined(submission)
            || !Enum.TryParse(RequiredString(p, "charge_state"), false, out ChargeState charge) || !Enum.IsDefined(charge)
            || !Enum.TryParse(RequiredString(proposal, "action"), false, out SnowGlobeActionKind action) || !Enum.IsDefined(action)) throw new InvalidDataException("Receipt enum is invalid.");
        return new(RequiredString(p, "idempotency_key"), RequiredString(p, "job_digest"), RequiredString(p, "policy_digest"), RequiredString(p, "financial_journal_identity"), ParseLane(RequiredString(p, "requested_lane")), RequiredString(p, "premium_model_identity"), RequiredString(p, "premium_model_revision_identity"), submission, charge,
            new(RequiredInt64(p, "reserved_microusd"), RequiredInt64(p, "settled_microusd")), RequiredString(p, "reason_code"), RequiredString(p, "primary_outcome_code"), new(RequiredString(proposal, "agent_id"), action, RequiredInt32(proposal, "quantity")));
    }

    private static JsonDocument ParseDocument(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = StrictUtf8.GetString(bytes);
            JsonDocument document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = FinancialJournalBounds.MaximumJsonDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object) { document.Dispose(); throw new InvalidDataException("Financial Journal JSON root must be an object."); }
            return document;
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException) { throw new InvalidDataException("Financial Journal JSON is invalid.", ex); }
    }

    private static void Exact(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Financial Journal JSON value must be an object.");
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject()) if (!names.Add(property.Name)) throw new InvalidDataException("Financial Journal JSON contains a duplicate property.");
        if (names.Count != expected.Length || expected.Any(name => !names.Contains(name))) throw new InvalidDataException("Financial Journal JSON has missing or unknown properties.");
    }

    private static string RequiredString(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.String ? e.GetProperty(name).GetString()! : throw new InvalidDataException($"{name} must be a string.");
    private static string? OptionalString(JsonElement e, string name) => e.GetProperty(name).ValueKind switch { JsonValueKind.Null => null, JsonValueKind.String => e.GetProperty(name).GetString(), _ => throw new InvalidDataException($"{name} must be a string or null.") };
    private static long RequiredInt64(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Number && e.GetProperty(name).TryGetInt64(out long value) ? value : throw new InvalidDataException($"{name} must be an integer.");
    private static int RequiredInt32(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Number && e.GetProperty(name).TryGetInt32(out int value) ? value : throw new InvalidDataException($"{name} must be a 32-bit integer.");
    private static CognitionLane ParseLane(string value) => value switch { "premium" => CognitionLane.Premium, "local" => CognitionLane.Local, _ => throw new InvalidDataException("Unknown cognition lane.") };
    private static string Kind(FinancialJournalCommand command) => command switch { AdmitAndReserveFinancialJournalCommand => "admit", MarkDispatchUnknownFinancialJournalCommand => "dispatch_unknown", CompleteFinancialJournalCommand => "complete", ReconcileUnknownFinancialJournalCommand => "reconcile", _ => throw new InvalidDataException("Unknown Financial Journal command.") };
    private static string Status(FinancialJournalApplyStatus status) => status.ToString();
    private static FinancialJournalApplyStatus ParseStatus(string value) => Enum.TryParse(value, false, out FinancialJournalApplyStatus status) && Enum.IsDefined(status) ? status : throw new InvalidDataException("Unknown Financial Journal status.");
    private static string OutcomeForParsed(FinancialJournalApplyStatus status) => status switch
    {
        FinancialJournalApplyStatus.Admitted => "admitted", FinancialJournalApplyStatus.JobCapDenied => "job_cap_denied",
        FinancialJournalApplyStatus.RunCeilingDenied => "run_ceiling_denied", FinancialJournalApplyStatus.AccountCeilingDenied => "account_ceiling_denied",
        FinancialJournalApplyStatus.OpenReservationCapDenied => "open_reservation_cap_denied", FinancialJournalApplyStatus.DispatchMarked => "submission_unknown",
        FinancialJournalApplyStatus.RecordHeadroomDenied => "record_headroom_denied",
        FinancialJournalApplyStatus.Completed => "completed", FinancialJournalApplyStatus.Reconciled => "reconciled", _ => status.ToString().ToLowerInvariant()
    };
    private static byte[] ReadBounded(string path, long maximum)
    {
        if (!File.Exists(path)) throw new InvalidDataException("Financial Journal artifact is missing.");
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = stream.Length;
        if (length > maximum) throw new InvalidDataException("Financial Journal artifact is oversized.");
        byte[] bytes = new byte[checked((int)length)];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new InvalidDataException("Financial Journal artifact changed while being read.");
            offset += read;
        }
        if (stream.Length != length) throw new InvalidDataException("Financial Journal artifact changed while being read.");
        return bytes;
    }
    private static bool HasBom(ReadOnlySpan<byte> bytes) => bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
    private sealed record ParsedRecord(FinancialJournalCommand Command, FinancialJournalApplyStatus Status, long Version, string Checksum);
}
