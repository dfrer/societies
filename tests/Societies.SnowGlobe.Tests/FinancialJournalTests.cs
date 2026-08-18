using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class FinancialJournalTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string EvidenceIdentity = "provider_charge_evidence/v1";

    [Fact]
    public void CreateDisposeReopenEmpty_PreservesExactHeaderAndCredentialFreeDtos()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header();
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
            {
                FinancialJournalReadResult read = journal.Read(new FinancialJournalPageQuery());
                Assert.Equal(header, read.Header); Assert.Empty(read.Entries); Assert.Equal(0, read.RecordCount);
                Assert.Equal(0, new FileInfo(Path.Combine(root, FileFinancialJournal.RecordsFileName)).Length);
            }
            using FileFinancialJournal reopened = FileFinancialJournal.OpenForAppend(root);
            Assert.Equal(header, reopened.Read(new FinancialJournalPageQuery()).Header);

            Type[] dtoTypes = { typeof(ByokAccountBindingIdentity), typeof(FinancialJournalHeader), typeof(FinancialJournalEntrySnapshot), typeof(ReconcileUnknownFinancialJournalCommand) };
            string[] forbidden = { "credential", "secret", "token", "email", "uri", "url", "raw", "metadata", "error" };
            foreach (PropertyInfo property in dtoTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
                Assert.DoesNotContain(forbidden, term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("account-subject-secret-sentinel", File.ReadAllText(Path.Combine(root, FileFinancialJournal.HeaderFileName), Encoding.UTF8), StringComparison.Ordinal);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ByokBinding_RejectsCredentialLikeOrLegacyPlaceholders()
    {
        Assert.Throws<ArgumentException>(() => new ByokAccountBindingIdentity("sk-live-secret-sentinel"));
        Assert.Throws<ArgumentException>(() => new ByokAccountBindingIdentity("byok-binding-v1"));
        Assert.Throws<ArgumentException>(() => new ByokAccountBindingIdentity("account-binding-v1"));

        const string rawSubject = "account-subject-secret-sentinel";
        string root = Temp();
        try
        {
            FinancialJournalHeader opaque = FinancialJournalHeader.Create("journal-v1", "financial-run-v1", CognitionLane.Premium,
                Policy().Digest, Revision, new ByokAccountBindingIdentity("byok-account-sha256-" + Hash(rawSubject)), 100, 100, 64, 4);
            using FileFinancialJournal _ = FileFinancialJournal.CreateNew(root, opaque);
            Assert.DoesNotContain(rawSubject, File.ReadAllText(Path.Combine(root, FileFinancialJournal.HeaderFileName), Encoding.UTF8), StringComparison.Ordinal);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task PremiumSuccess_PersistsOrderedFactsAndRestartReplayDoesNoInferenceWork()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header();
            CountingAdapter firstLocal = new(Proposal()); CountingProvider firstPremium = new(Success());
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
            {
                SnowGlobeTwoTierCognitionModule module = Module(journal, firstLocal, firstPremium);
                Assert.Equal(Proposal(), await module.ProposeAsync(Observation(), default));
                FinancialJournalEntrySnapshot entry = Assert.Single(journal.Read(new FinancialJournalPageQuery()).Entries);
                Assert.Equal(3, entry.Version); Assert.Equal(SubmissionState.ResponseReceived, entry.SubmissionState);
                Assert.Equal(ChargeState.Settled, entry.EffectiveChargeState); Assert.Equal(2, entry.EffectiveSettledMicrousd);
                Assert.Equal(new[] { "reserved", "submission_unknown", "completed/premium_success" }, journal.SnapshotTrace());
            }

            CountingAdapter replayLocal = new(Proposal()); CountingProvider replayPremium = new(Success());
            using FileFinancialJournal reopened = FileFinancialJournal.OpenForAppend(root);
            SnowGlobeTwoTierCognitionModule replayModule = Module(reopened, replayLocal, replayPremium);
            Assert.Equal(Proposal(), await replayModule.ProposeAsync(Observation(), default));
            Assert.Equal(0, replayPremium.Calls); Assert.Equal(0, replayLocal.Calls);
            Assert.Equal(3, Assert.Single(reopened.Read(new FinancialJournalPageQuery()).Entries).Version);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ModuleRequiresExactExpectedJournalHeaderIncludingByokCapsAndChecksum()
    {
        FinancialJournalHeader actual = Header();
        using InMemoryCognitionJobJournal journal = new(actual);
        CountingAdapter local = new(Proposal()); CountingProvider premium = new(Success());
        FinancialJournalHeader wrongCaps = Header(run: actual.RunCeilingMicrousd + 1);
        FinancialJournalHeader wrongByok = FinancialJournalHeader.Create(actual.FinancialJournalIdentity, actual.FinancialRunIdentity, actual.Lane,
            actual.PolicyDigest, actual.PremiumModelRevisionIdentity, new ByokAccountBindingIdentity("byok-account-sha256-" + Hash("other-account")),
            actual.RunCeilingMicrousd, actual.AccountCeilingMicrousd, actual.MaximumJobs, actual.MaximumOpenReservations);

        Assert.Throws<ArgumentException>(() => new SnowGlobeTwoTierCognitionModule(CognitionLane.Premium, wrongCaps, Policy(), local, journal, premium));
        Assert.Throws<ArgumentException>(() => new SnowGlobeTwoTierCognitionModule(CognitionLane.Premium, wrongByok, Policy(), local, journal, premium));
        Assert.NotNull(new SnowGlobeTwoTierCognitionModule(CognitionLane.Premium, actual, Policy(), local, journal, premium));
    }

    [Fact]
    public async Task ReopenAfterReservationBeforeMarker_MayMarkAndDispatchExactlyOnce()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(); DirectWork work = Work(header);
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
                Assert.Equal(FinancialJournalApplyStatus.Admitted, journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20)).Status);

            CountingProvider premium = new(Success()); CountingAdapter local = new(Proposal());
            using FileFinancialJournal reopened = FileFinancialJournal.OpenForAppend(root);
            Assert.Equal(Proposal(), await Module(reopened, local, premium).ProposeAsync(Observation(), default));
            Assert.Equal(1, premium.Calls); Assert.Equal(0, local.Calls);
            FinancialJournalReadResult read = reopened.Read(new FinancialJournalPageQuery());
            Assert.Equal(ChargeState.Settled, Assert.Single(read.Entries).EffectiveChargeState);
            Assert.Equal(3, read.RecordCount);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task ReopenAfterDispatchMarker_NeverDispatchesAndCompletesFallbackUnknown()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(); DirectWork work = Work(header);
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
            {
                FinancialJournalApplyResult admit = journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20));
                Assert.Equal(FinancialJournalApplyStatus.DispatchMarked, journal.Apply(new MarkDispatchUnknownFinancialJournalCommand(work.Key, work.Digest, admit.Version)).Status);
            }
            CountingProvider premium = new(Success()); CountingAdapter local = new(Proposal());
            using FileFinancialJournal reopened = FileFinancialJournal.OpenForAppend(root);
            Assert.Equal(Proposal(), await Module(reopened, local, premium).ProposeAsync(Observation(), default));
            Assert.Equal(0, premium.Calls); Assert.Equal(1, local.Calls);
            FinancialJournalEntrySnapshot entry = Assert.Single(reopened.Read(new FinancialJournalPageQuery()).Entries);
            Assert.Equal(ChargeState.Unknown, entry.EffectiveChargeState); Assert.Equal(20, entry.ReservedMicrousd);
            Assert.Equal("provider_unknown", entry.Receipt!.PrimaryOutcomeCode);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task SameKeyReplayConflictAcrossRestart_AndConcurrentSingleFlight()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(); GateProvider premium = new(Success()); CountingAdapter local = new(Proposal());
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
            {
                SnowGlobeTwoTierCognitionModule module = Module(journal, local, premium);
                Task<SnowGlobeActionProposal> first = module.ProposeAsync(Observation(), default).AsTask();
                await premium.Started.Task;
                Task<SnowGlobeActionProposal> duplicate = module.ProposeAsync(Observation(), default).AsTask();
                Assert.Equal(1, premium.Calls); premium.Release();
                Assert.Equal(await first, await duplicate); Assert.Equal(1, premium.Calls);
            }
            using FileFinancialJournal reopened = FileFinancialJournal.OpenForAppend(root);
            CountingProvider replayPremium = new(Success()); CountingAdapter replayLocal = new(Proposal());
            SnowGlobeTwoTierCognitionModule restarted = Module(reopened, replayLocal, replayPremium);
            Assert.Equal(Proposal(), await restarted.ProposeAsync(Observation(), default));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await restarted.ProposeAsync(Observation() with { AvailableWood = 99 }, default));
            Assert.Equal(0, replayPremium.Calls); Assert.Equal(0, replayLocal.Calls);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void AdmissionCapsAndOverflow_AreDeterministicAndUnknownContinuesConsumingCapacity()
    {
        Assert.Equal(FinancialJournalApplyStatus.JobCapDenied, SecondAdmission(Header(maximumJobs: 1, maximumOpen: 1, run: 100, account: 100), completeFirst: true, firstCharge: ChargeState.Released));
        Assert.Equal(FinancialJournalApplyStatus.RunCeilingDenied, FirstAdmission(Header(run: 19, account: 100, maximumOpen: 2)));
        Assert.Equal(FinancialJournalApplyStatus.AccountCeilingDenied, FirstAdmission(Header(run: 100, account: 19, maximumOpen: 2)));
        Assert.Equal(FinancialJournalApplyStatus.OpenReservationCapDenied, SecondAdmission(Header(maximumOpen: 1, run: 100, account: 100), completeFirst: true, firstCharge: ChargeState.Unknown));
        Assert.Equal(FinancialJournalApplyStatus.Admitted, SecondAdmission(Header(maximumOpen: 1, run: 100, account: 100), completeFirst: true, firstCharge: ChargeState.Released));

        FinancialJournalHeader overflowHeader = Header(run: long.MaxValue, account: long.MaxValue, maximumOpen: 2);
        using InMemoryCognitionJobJournal overflow = new(overflowHeader);
        DirectWork one = Work(overflowHeader, "one"); DirectWork two = Work(overflowHeader, "two");
        Assert.Equal(FinancialJournalApplyStatus.Admitted, overflow.Apply(new AdmitAndReserveFinancialJournalCommand(one.Key, one.Digest, one.Job, long.MaxValue)).Status);
        Assert.Throws<InvalidDataException>(() => overflow.Apply(new AdmitAndReserveFinancialJournalCommand(two.Key, two.Digest, two.Job, 1)));
    }

    [Fact]
    public void AdmissionAndCompletionReplay_RequireExactImmutableCommands()
    {
        FinancialJournalHeader header = Header();
        using InMemoryCognitionJobJournal journal = new(header);
        DirectWork work = Work(header);
        FinancialJournalApplyResult admitted = journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20));

        Assert.Equal(FinancialJournalApplyStatus.Conflict,
            journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 19)).Status);
        Assert.Equal(FinancialJournalApplyStatus.Conflict,
            journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job with { Observation = work.Job.Observation with { Tick = 1 } }, 20)).Status);

        FinancialJournalApplyResult marked = journal.Apply(new MarkDispatchUnknownFinancialJournalCommand(work.Key, work.Digest, admitted.Version));
        InferenceReceipt receipt = Receipt(header, work, ChargeState.Unknown, SubmissionState.SubmissionUnknown, 0);
        CompleteFinancialJournalCommand complete = new(work.Key, work.Digest, marked.Version, receipt);
        Assert.Equal(FinancialJournalApplyStatus.Completed, journal.Apply(complete).Status);
        Assert.Equal(FinancialJournalApplyStatus.Replay, journal.Apply(complete).Status);
        Assert.Equal(FinancialJournalApplyStatus.Conflict, journal.Apply(complete with { ExpectedVersion = admitted.Version }).Status);
    }

    [Fact]
    public void MalformedNestedValuesAndNonAllowlistedEvidence_FailBeforeMutation()
    {
        FinancialJournalHeader header = Header();
        using InMemoryCognitionJobJournal journal = new(header);
        DirectWork work = Work(header);
        PremiumCognitionJob malformed = work.Job with { Observation = null! };
        Assert.Throws<InvalidDataException>(() => journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, malformed, 20)));
        Assert.Equal(0, journal.Read(new FinancialJournalSnapshotQuery()).RecordCount);

        CompleteUnknown(journal, header, work);
        Assert.Throws<InvalidDataException>(() => journal.Apply(new ReconcileUnknownFinancialJournalCommand(work.Key, work.Digest, 3,
            header.ByokAccountBinding!, "untrusted-evidence", Hash("evidence"), FinancialReconciliationOutcome.Release, 0)));

        FinancialJournalHeader deniedHeader = Header(maximumOpen: 0);
        using InMemoryCognitionJobJournal denied = new(deniedHeader);
        DirectWork deniedWork = Work(deniedHeader);
        FinancialJournalApplyResult denial = denied.Apply(new AdmitAndReserveFinancialJournalCommand(deniedWork.Key, deniedWork.Digest, deniedWork.Job, 20));
        InferenceReceipt mismatched = Receipt(deniedHeader, deniedWork, ChargeState.NotApplicable, SubmissionState.DefinitelyNotSubmitted, 0)
            with { PremiumModelIdentity = "other-model", PrimaryOutcomeCode = "capacity_denied" };
        Assert.Throws<InvalidDataException>(() => denied.Apply(new CompleteFinancialJournalCommand(deniedWork.Key, deniedWork.Digest, denial.Version, mismatched)));
    }

    [Fact]
    public void ReceiptInspectorAndSnapshotQuery_DoNotSilentlyTruncatePastOnePage()
    {
        FinancialJournalHeader header = Header(lane: CognitionLane.Local, maximumJobs: 80, maximumOpen: 0);
        using InMemoryCognitionJobJournal journal = new(header);
        for (int index = 0; index < 70; index++)
        {
            string key = Hash("local-key-" + index); string digest = Hash("local-job-" + index);
            FinancialJournalApplyResult admitted = journal.Apply(new AdmitAndReserveFinancialJournalCommand(key, digest, null, 0));
            InferenceReceipt receipt = new(key, digest, header.PolicyDigest, header.FinancialJournalIdentity, CognitionLane.Local,
                "qwen3.8-27b", header.PremiumModelRevisionIdentity, SubmissionState.NotApplicable, ChargeState.NotApplicable,
                new CostReservation(0, 0), "local_success", "local_success", Proposal());
            Assert.Equal(FinancialJournalApplyStatus.Completed,
                journal.Apply(new CompleteFinancialJournalCommand(key, digest, admitted.Version, receipt)).Status);
        }

        Assert.Equal(70, journal.SnapshotReceipts().Count);
        Assert.Equal(70, journal.Read(new FinancialJournalSnapshotQuery()).Entries.Count);
    }

    [Fact]
    public void NaturalFileArchiveRead_ReturnsAllBoundedEntriesPastOnePage()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(lane: CognitionLane.Local, maximumJobs: 80, maximumOpen: 0);
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
            {
                for (int index = 0; index < 70; index++) CompleteLocal(journal, header, index);
            }

            FinancialJournalReadResult archive = FileFinancialJournal.ReadArchive(root);
            Assert.Equal(70, archive.Entries.Count);
            Assert.Equal(140, archive.RecordCount);
            Assert.All(archive.Entries, entry => Assert.NotNull(entry.Receipt));
            Assert.Equal(64, FileFinancialJournal.ReadArchive(root, new FinancialJournalPageQuery(0, 64)).Entries.Count);
            Assert.Equal(6, FileFinancialJournal.ReadArchive(root, new FinancialJournalPageQuery(64, 64)).Entries.Count);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void SourceImpossibleCompletionTuples_AreRejectedBeforeAppendAndWriterRemainsUsable()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(); DirectWork work = Work(header);
            using FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header);
            FinancialJournalApplyResult admitted = journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20));
            FinancialJournalApplyResult marked = journal.Apply(new MarkDispatchUnknownFinancialJournalCommand(work.Key, work.Digest, admitted.Version));
            InferenceReceipt validUnknown = Receipt(header, work, ChargeState.Unknown, SubmissionState.SubmissionUnknown, 0);
            InferenceReceipt[] impossible =
            {
                validUnknown with { SubmissionState = SubmissionState.ResponseReceived, ChargeState = ChargeState.Settled,
                    Reservation = new CostReservation(20, 1), ReasonCode = "local_fallback", PrimaryOutcomeCode = "capacity_denied" },
                validUnknown with { ReasonCode = "premium_success", PrimaryOutcomeCode = "premium_success" },
                validUnknown with { PremiumModelIdentity = "other-model" },
                validUnknown with { PremiumModelRevisionIdentity = "sha256-abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789" },
                validUnknown with { Proposal = validUnknown.Proposal with { AgentId = "other-agent" } },
                validUnknown with { Proposal = validUnknown.Proposal with { Action = (SnowGlobeActionKind)999 } },
                validUnknown with { Proposal = validUnknown.Proposal with { Quantity = -1 } },
                validUnknown with { Proposal = validUnknown.Proposal with { Quantity = 65 } },
                validUnknown with { Proposal = validUnknown.Proposal with { Action = SnowGlobeActionKind.Idle, Quantity = 1 } }
            };
            string records = Path.Combine(root, FileFinancialJournal.RecordsFileName);
            byte[] before = ReadShared(records);
            foreach (InferenceReceipt receipt in impossible)
            {
                Assert.Throws<InvalidDataException>(() => journal.Apply(new CompleteFinancialJournalCommand(work.Key, work.Digest, marked.Version, receipt)));
                Assert.Equal(before, ReadShared(records));
                Assert.False(journal.IsPoisoned);
            }
            Assert.Equal(FinancialJournalApplyStatus.Completed,
                journal.Apply(new CompleteFinancialJournalCommand(work.Key, work.Digest, marked.Version, validUnknown)).Status);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task RecordHeadroom_DeniesWithoutProviderAndPreservesExistingUnknownReconciliation()
    {
        FinancialJournalHeader header = Header(maximumJobs: FinancialJournalBoundsForTests.MaximumRecords, maximumOpen: 1);
        using InMemoryCognitionJobJournal journal = UnknownCompleted(header, out DirectWork existingUnknown);

        for (int index = 0; index < 2045; index++)
        {
            DirectWork deniedWork = Work(header, "headroom-fill-" + index);
            FinancialJournalApplyResult denied = journal.Apply(new AdmitAndReserveFinancialJournalCommand(deniedWork.Key, deniedWork.Digest, deniedWork.Job, 20));
            Assert.Equal(FinancialJournalApplyStatus.OpenReservationCapDenied, denied.Status);
            InferenceReceipt deniedReceipt = Receipt(header, deniedWork, ChargeState.NotApplicable, SubmissionState.DefinitelyNotSubmitted, 0)
                with { PrimaryOutcomeCode = "capacity_denied" };
            Assert.Equal(FinancialJournalApplyStatus.Completed,
                journal.Apply(new CompleteFinancialJournalCommand(deniedWork.Key, deniedWork.Digest, denied.Version, deniedReceipt)).Status);
        }
        Assert.Equal(4093, journal.Read(new FinancialJournalSnapshotQuery()).RecordCount);

        CountingAdapter local = new(Proposal()); CountingProvider premium = new(Success());
        SnowGlobeTwoTierCognitionModule module = Module(journal, local, premium);
        SnowGlobeObservation deniedObservation = Observation() with { Tick = 1 };
        Assert.Equal(Proposal(), await module.ProposeAsync(deniedObservation, default));
        Assert.Equal(Proposal(), await module.ProposeAsync(deniedObservation, default));
        Assert.Equal(0, premium.Calls); Assert.Equal(1, local.Calls);
        Assert.Equal(4095, journal.Read(new FinancialJournalSnapshotQuery()).RecordCount);
        Assert.Contains(journal.SnapshotReceipts(), receipt => receipt.PrimaryOutcomeCode == "record_headroom_denied");

        Assert.Equal(FinancialJournalApplyStatus.Reconciled, journal.Apply(new ReconcileUnknownFinancialJournalCommand(
            existingUnknown.Key, existingUnknown.Digest, 3, header.ByokAccountBinding!, EvidenceIdentity, Hash("final-release"),
            FinancialReconciliationOutcome.Release, 0)).Status);
        Assert.Equal(4096, journal.Read(new FinancialJournalSnapshotQuery()).RecordCount);

        SnowGlobeObservation exhaustedObservation = Observation() with { Tick = 2 };
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), await module.ProposeAsync(exhaustedObservation, default));
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.Idle), await module.ProposeAsync(exhaustedObservation, default));
        Assert.Equal(0, premium.Calls); Assert.Equal(1, local.Calls);
        Assert.Equal(4096, journal.Read(new FinancialJournalSnapshotQuery()).RecordCount);
        // At full capacity the denial is deterministic and module-cached, but cannot create a durable idempotency key.
    }

    [Fact]
    public void DeniedAttemptDoesNotConsumeJobCap_AndReleasedUnknownRestoresOpenCapacity()
    {
        FinancialJournalHeader header = Header(maximumJobs: 2, maximumOpen: 1, run: 100, account: 100);
        using InMemoryCognitionJobJournal journal = new(header);
        DirectWork first = Work(header, "first"); DirectWork denied = Work(header, "denied"); DirectWork third = Work(header, "third");
        CompleteUnknown(journal, header, first);
        FinancialJournalApplyResult denial = journal.Apply(new AdmitAndReserveFinancialJournalCommand(denied.Key, denied.Digest, denied.Job, 20));
        Assert.Equal(FinancialJournalApplyStatus.OpenReservationCapDenied, denial.Status);
        InferenceReceipt deniedReceipt = Receipt(header, denied, ChargeState.NotApplicable, SubmissionState.DefinitelyNotSubmitted, 0)
            with { PrimaryOutcomeCode = "capacity_denied" };
        Assert.Equal(FinancialJournalApplyStatus.Completed, journal.Apply(new CompleteFinancialJournalCommand(denied.Key, denied.Digest, denial.Version, deniedReceipt)).Status);
        Assert.Equal(FinancialJournalApplyStatus.Reconciled, journal.Apply(new ReconcileUnknownFinancialJournalCommand(first.Key, first.Digest, 3,
            header.ByokAccountBinding!, EvidenceIdentity, Hash("release"), FinancialReconciliationOutcome.Release, 0)).Status);
        Assert.Equal(FinancialJournalApplyStatus.Admitted, journal.Apply(new AdmitAndReserveFinancialJournalCommand(third.Key, third.Digest, third.Job, 20)).Status);
    }

    [Fact]
    public void Reconciliation_IsVersionedIdempotentAccountBoundAndReceiptImmutable()
    {
        FinancialJournalHeader header = Header();
        using InMemoryCognitionJobJournal journal = UnknownCompleted(header, out DirectWork work);
        FinancialJournalEntrySnapshot before = Assert.Single(journal.Read(new FinancialJournalPageQuery()).Entries);
        Assert.Equal(ChargeState.Unknown, before.Receipt!.ChargeState); Assert.Equal(3, before.Version);
        ReconcileUnknownFinancialJournalCommand reconcile = new(work.Key, work.Digest, 3, header.ByokAccountBinding!, EvidenceIdentity, Hash("evidence"), FinancialReconciliationOutcome.Release, 0);
        Assert.Equal(FinancialJournalApplyStatus.Reconciled, journal.Apply(reconcile).Status);
        Assert.Equal(FinancialJournalApplyStatus.ReconciliationReplay, journal.Apply(reconcile).Status);
        Assert.Equal(FinancialJournalApplyStatus.Conflict, journal.Apply(reconcile with { ExpectedVersion = 2 }).Status);
        FinancialJournalEntrySnapshot after = Assert.Single(journal.Read(new FinancialJournalPageQuery()).Entries);
        Assert.Equal(ChargeState.Released, after.EffectiveChargeState); Assert.Equal(ChargeState.Unknown, after.Receipt!.ChargeState);
        Assert.Equal(before.Receipt, after.Receipt); Assert.Equal(4, after.Version);
        Assert.Equal(FinancialJournalApplyStatus.Conflict, journal.Apply(reconcile with { EvidenceDigest = Hash("other") }).Status);

        using InMemoryCognitionJobJournal wrongs = UnknownCompleted(header, out DirectWork other);
        Assert.Equal(FinancialJournalApplyStatus.Conflict, wrongs.Apply(new ReconcileUnknownFinancialJournalCommand(other.Key, other.Digest, 2, header.ByokAccountBinding!, EvidenceIdentity, Hash("evidence"), FinancialReconciliationOutcome.Release, 0)).Status);
        Assert.Throws<InvalidDataException>(() => wrongs.Apply(new ReconcileUnknownFinancialJournalCommand(other.Key, other.Digest, 3,
            new("byok-account-sha256-" + Hash("other-binding")), EvidenceIdentity, Hash("evidence"), FinancialReconciliationOutcome.Release, 0)));
        Assert.Throws<InvalidDataException>(() => wrongs.Apply(new ReconcileUnknownFinancialJournalCommand(other.Key, Hash("wrong"), 3, header.ByokAccountBinding!, EvidenceIdentity, Hash("evidence"), FinancialReconciliationOutcome.Release, 0)));
        Assert.Throws<InvalidDataException>(() => wrongs.Apply(new ReconcileUnknownFinancialJournalCommand(other.Key, other.Digest, 3, header.ByokAccountBinding!, EvidenceIdentity, Hash("evidence"), FinancialReconciliationOutcome.Settle, 21)));
    }

    [Fact]
    public async Task PremiumProviderReentrancy_FailsClosedWithoutDeadlockOrSecondDispatch()
    {
        FinancialJournalHeader header = Header();
        using InMemoryCognitionJobJournal journal = new(header);
        CountingAdapter local = new(Proposal());
        ReentrantProvider premium = new();
        SnowGlobeTwoTierCognitionModule module = Module(journal, local, premium);
        premium.Module = module;

        Task<SnowGlobeActionProposal> operation = module.ProposeAsync(Observation(), default).AsTask();
        Task completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(operation, completed);
        Assert.Equal(Proposal(), await operation);
        Assert.Equal(1, premium.Calls);
        Assert.Equal(1, local.Calls);
        Assert.Equal(ChargeState.Unknown, Assert.Single(module.SnapshotReceipts()).ChargeState);
    }

    [Fact]
    public void Reconciliation_PersistsAcrossRestartWithoutChangingReceipt()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(); DirectWork work = Work(header);
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header))
            {
                CompleteUnknown(journal, header, work);
                journal.Apply(new ReconcileUnknownFinancialJournalCommand(work.Key, work.Digest, 3, header.ByokAccountBinding!, EvidenceIdentity, Hash("evidence"), FinancialReconciliationOutcome.Settle, 7));
            }
            FinancialJournalEntrySnapshot entry = Assert.Single(FileFinancialJournal.ReadArchive(root).Entries);
            Assert.Equal(ChargeState.Settled, entry.EffectiveChargeState); Assert.Equal(7, entry.EffectiveSettledMicrousd);
            Assert.Equal(ChargeState.Unknown, entry.Receipt!.ChargeState); Assert.Equal(0, entry.Receipt.Reservation.SettledMicrousd);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void StrictHeaderCorruption_IsRejectedWithoutArtifactRewrite()
    {
        string source = ValidEmptyJournal();
        try
        {
            string original = File.ReadAllText(Path.Combine(source, FileFinancialJournal.HeaderFileName), Encoding.UTF8);
            byte[][] corruptions =
            {
                Encoding.UTF8.GetBytes(original[..^1] + ",\"unknown\":0}"),
                Encoding.UTF8.GetBytes(original[..^1] + ",\"schema\":\"snow_globe_financial_journal/v1\"}"),
                Encoding.UTF8.GetBytes(original.Replace("\"maximum_jobs\":64,", "", StringComparison.Ordinal)),
                Encoding.UTF8.GetBytes(original.Replace("\"maximum_jobs\":64", "\"maximum_jobs\":" + new string('[', 20) + "0" + new string(']', 20), StringComparison.Ordinal)),
                new byte[] { 0xff, 0xfe, 0xfd },
                new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(original)).ToArray(),
                Enumerable.Repeat((byte)'x', 4097).ToArray()
            };
            foreach (byte[] bytes in corruptions)
            {
                string copy = Copy(source); File.WriteAllBytes(Path.Combine(copy, FileFinancialJournal.HeaderFileName), bytes);
                AssertRejectedUnchanged(copy); Delete(copy);
            }
        }
        finally { Delete(source); }
    }

    [Fact]
    public void StrictRecordCorruptionMatrix_IsRejectedWithoutArtifactRewrite()
    {
        string source = ValidAdmittedJournal();
        try
        {
            string recordPath = Path.Combine(source, FileFinancialJournal.RecordsFileName);
            byte[] original = File.ReadAllBytes(recordPath); string text = Encoding.UTF8.GetString(original);
            List<byte[]> corruptions = new()
            {
                Encoding.UTF8.GetBytes(text.Replace("\"sequence\":1", "\"sequence\":2", StringComparison.Ordinal)),
                Encoding.UTF8.GetBytes(text.Replace("\"previous_checksum\":\"", "\"previous_checksum\":\"f", StringComparison.Ordinal)),
                Encoding.UTF8.GetBytes(text.Replace("\"header_checksum\":\"", "\"header_checksum\":\"f", StringComparison.Ordinal)),
                Encoding.UTF8.GetBytes(text.Replace("\"kind\":\"admit\"", "\"kind\":\"other\"", StringComparison.Ordinal)),
                Encoding.UTF8.GetBytes(text.Replace("\"record_checksum\":\"", "\"record_checksum\":\"f", StringComparison.Ordinal)),
                Encoding.UTF8.GetBytes(text.Replace("\"reserved_microusd\":20", "\"reserved_microusd\":20,\"unknown\":0", StringComparison.Ordinal)),
                original[..^1],
                new byte[] { 0xff, (byte)'\n' },
                new byte[] { 0xef, 0xbb, 0xbf }.Concat(original).ToArray(),
                Enumerable.Repeat((byte)'x', 8193).Append((byte)'\n').ToArray(),
                Enumerable.Repeat((byte)'\n', 4097).ToArray(),
                Enumerable.Repeat((byte)'x', 32 * 1024 * 1024 + 1).ToArray()
            };
            foreach (byte[] bytes in corruptions)
            {
                string copy = Copy(source); File.WriteAllBytes(Path.Combine(copy, FileFinancialJournal.RecordsFileName), bytes);
                AssertRejectedUnchanged(copy); Delete(copy);
            }
        }
        finally { Delete(source); }
    }

    [Fact]
    public void LiveWriterLeaseExcludesPeers_ButStaleLockFileDoesNotBlockReopen()
    {
        string root = Temp();
        try
        {
            FileFinancialJournal first = FileFinancialJournal.CreateNew(root, Header());
            Assert.Throws<IOException>(() => FileFinancialJournal.OpenForAppend(root));
            Assert.Throws<IOException>(() => FileFinancialJournal.ReadArchive(root));
            first.Dispose();
            Assert.True(File.Exists(Path.Combine(root, FileFinancialJournal.WriterLeaseFileName)));
            Assert.Equal(0, FileFinancialJournal.ReadArchive(root).RecordCount);
            using FileFinancialJournal reopened = FileFinancialJournal.OpenForAppend(root);
            Assert.False(reopened.IsPoisoned);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void UncertainAppendFault_PoisonsWriterAndNeverAttemptsRepair()
    {
        string root = Temp();
        try
        {
            FinancialJournalHeader header = Header(); DirectWork work = Work(header); AfterWriteFault fault = new();
            long before;
            using (FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, header, fault))
            {
                before = new FileInfo(Path.Combine(root, FileFinancialJournal.RecordsFileName)).Length;
                Assert.Throws<IOException>(() => journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20)));
                Assert.True(journal.IsPoisoned);
                Assert.Throws<InvalidOperationException>(() => journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20)));
                Assert.Throws<InvalidOperationException>(() => journal.Apply(new ReconcileUnknownFinancialJournalCommand(work.Key, work.Digest, 1, header.ByokAccountBinding!, EvidenceIdentity, Hash("e"), FinancialReconciliationOutcome.Release, 0)));
            }
            Assert.True(new FileInfo(Path.Combine(root, FileFinancialJournal.RecordsFileName)).Length >= before);
            try { Assert.InRange(FileFinancialJournal.ReadArchive(root).RecordCount, 0, 1); }
            catch (InvalidDataException) { /* An uncertain persisted tail must fail closed, never be repaired. */ }
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ManagedValidationFailureBeforeAppend_LeavesBytesAndWriterUsable()
    {
        string root = Temp();
        try
        {
            using FileFinancialJournal journal = FileFinancialJournal.CreateNew(root, Header());
            string records = Path.Combine(root, FileFinancialJournal.RecordsFileName);
            byte[] before = ReadShared(records);
            Assert.Throws<InvalidDataException>(() => journal.Apply(new AdmitAndReserveFinancialJournalCommand("not-a-digest", Hash("job"), null, -1)));
            Assert.Equal(before, ReadShared(records)); Assert.False(journal.IsPoisoned);
            DirectWork work = Work(Header());
            Assert.Equal(FinancialJournalApplyStatus.Admitted, journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20)).Status);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task InferenceAdaptersAreNeverCalledWhileJournalApplyOrReadIsActive()
    {
        FinancialJournalHeader premiumHeader = Header();
        using ProbeJournal premiumJournal = new(new InMemoryCognitionJobJournal(premiumHeader));
        CountingAdapter local = new(Proposal(), () => Assert.False(premiumJournal.Active));
        CountingProvider premium = new(Success(), () => Assert.False(premiumJournal.Active));
        Assert.Equal(Proposal(), await Module(premiumJournal, local, premium).ProposeAsync(Observation(), default));

        FinancialJournalHeader localHeader = Header(lane: CognitionLane.Local);
        using ProbeJournal localJournal = new(new InMemoryCognitionJobJournal(localHeader));
        CountingAdapter localOnly = new(Proposal(), () => Assert.False(localJournal.Active));
        SnowGlobeTwoTierCognitionModule localModule = new(CognitionLane.Local, localHeader, Policy(), localOnly, localJournal);
        Assert.Equal(Proposal(), await localModule.ProposeAsync(Observation(), default));
    }

    private static FinancialJournalApplyStatus FirstAdmission(FinancialJournalHeader header)
    {
        using InMemoryCognitionJobJournal journal = new(header); DirectWork work = Work(header);
        return journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20)).Status;
    }

    private static FinancialJournalApplyStatus SecondAdmission(FinancialJournalHeader header, bool completeFirst, ChargeState firstCharge)
    {
        using InMemoryCognitionJobJournal journal = new(header); DirectWork first = Work(header, "first"); DirectWork second = Work(header, "second");
        FinancialJournalApplyResult admitted = journal.Apply(new AdmitAndReserveFinancialJournalCommand(first.Key, first.Digest, first.Job, 20));
        if (completeFirst)
        {
            FinancialJournalApplyResult marked = journal.Apply(new MarkDispatchUnknownFinancialJournalCommand(first.Key, first.Digest, admitted.Version));
            InferenceReceipt receipt = Receipt(header, first, firstCharge, firstCharge == ChargeState.Released ? SubmissionState.DefinitelyNotSubmitted : SubmissionState.SubmissionUnknown, 0)
                with { PrimaryOutcomeCode = firstCharge == ChargeState.Released ? "provider_rejected" : "provider_unknown" };
            journal.Apply(new CompleteFinancialJournalCommand(first.Key, first.Digest, marked.Version, receipt));
        }
        return journal.Apply(new AdmitAndReserveFinancialJournalCommand(second.Key, second.Digest, second.Job, 20)).Status;
    }

    private static InMemoryCognitionJobJournal UnknownCompleted(FinancialJournalHeader header, out DirectWork work)
    {
        InMemoryCognitionJobJournal journal = new(header); work = Work(header); CompleteUnknown(journal, header, work); return journal;
    }

    private static void CompleteUnknown(IFinancialJournal journal, FinancialJournalHeader header, DirectWork work)
    {
        FinancialJournalApplyResult admitted = journal.Apply(new AdmitAndReserveFinancialJournalCommand(work.Key, work.Digest, work.Job, 20));
        FinancialJournalApplyResult marked = journal.Apply(new MarkDispatchUnknownFinancialJournalCommand(work.Key, work.Digest, admitted.Version));
        journal.Apply(new CompleteFinancialJournalCommand(work.Key, work.Digest, marked.Version, Receipt(header, work, ChargeState.Unknown, SubmissionState.SubmissionUnknown, 0)));
    }

    private static void CompleteLocal(IFinancialJournal journal, FinancialJournalHeader header, int index)
    {
        string key = Hash("file-local-key-" + index); string digest = Hash("file-local-job-" + index);
        FinancialJournalApplyResult admitted = journal.Apply(new AdmitAndReserveFinancialJournalCommand(key, digest, null, 0));
        InferenceReceipt receipt = new(key, digest, header.PolicyDigest, header.FinancialJournalIdentity, CognitionLane.Local,
            "qwen3.8-27b", header.PremiumModelRevisionIdentity, SubmissionState.NotApplicable, ChargeState.NotApplicable,
            new CostReservation(0, 0), "local_success", "local_success", Proposal());
        Assert.Equal(FinancialJournalApplyStatus.Completed,
            journal.Apply(new CompleteFinancialJournalCommand(key, digest, admitted.Version, receipt)).Status);
    }

    private static InferenceReceipt Receipt(FinancialJournalHeader header, DirectWork work, ChargeState charge, SubmissionState submission, long settled) =>
        new(work.Key, work.Digest, header.PolicyDigest, header.FinancialJournalIdentity, header.Lane, "qwen3.8-27b", header.PremiumModelRevisionIdentity,
            submission, charge, new CostReservation(charge == ChargeState.NotApplicable ? 0 : 20, settled), "local_fallback", "provider_unknown", Proposal());

    private static FinancialJournalHeader Header(CognitionLane lane = CognitionLane.Premium, long run = 100, long account = 100, int maximumJobs = 64, int maximumOpen = 4) =>
        FinancialJournalHeader.Create("journal-v1", "financial-run-v1", lane, Policy().Digest, Revision,
            lane == CognitionLane.Premium ? new ByokAccountBindingIdentity("byok-account-sha256-" + Hash("account-binding-v1")) : null, run, account, maximumJobs, maximumOpen);
    private static ModelPolicySnapshot Policy() => new("policy-v1", "premium.example", "v1/propose", "qwen3.8-27b", Revision, "prompt-v1", "proposal-v1", "test_local/v1", "usd", 1_000_000, 1_000_000, 10, 10, 20, 1000);
    private static SnowGlobeObservation Observation() => new("agent-00", 0, 0, 10, 10, 0, 0, 0, 0);
    private static SnowGlobeActionProposal Proposal() => new("agent-00", SnowGlobeActionKind.GatherWood, 1);
    private static PremiumCognitionProviderResult Success() => new(SubmissionState.ResponseReceived, PremiumResponseStatus.Success, "premium.example", "v1/propose", "qwen3.8-27b", Revision, false, 1, 1, Proposal());
    private static SnowGlobeTwoTierCognitionModule Module(IFinancialJournal journal, ISnowGlobeIdentifiedInferenceAdapter local, IPremiumCognitionProvider premium)
    {
        FinancialJournalHeader expected = journal.Read(new FinancialJournalPageQuery(0, 0)).Header;
        return new(CognitionLane.Premium, expected, Policy(), local, journal, premium);
    }

    private static DirectWork Work(FinancialJournalHeader header, string salt = "work")
    {
        string adapter = $"snow_globe_two_tier/premium/{Policy().Digest}/{header.FinancialJournalIdentity}";
        SnowGlobeObservation observation = Observation(); string key = Hash($"{adapter}|{observation.Tick}|{observation.AgentId}|{salt}");
        if (salt == "work") key = Hash($"{adapter}|{observation.Tick}|{observation.AgentId}");
        string digest = Hash($"{adapter}|{ObservationCanonical(observation)}|{salt}");
        if (salt == "work") digest = Hash($"{adapter}|{ObservationCanonical(observation)}");
        return new(key, digest, new PremiumCognitionJob(key, digest, header.PolicyDigest, "qwen3.8-27b", header.PremiumModelRevisionIdentity, observation));
    }

    private static string ObservationCanonical(SnowGlobeObservation o) => string.Join("|", Field(o.AgentId), o.HomeSlot, o.Tick, o.AvailableWood, o.AvailableStone, o.StockpileWood, o.StockpileStone, o.ShelterCount, o.StorageCount);
    private static string Field(string value) => $"{Encoding.UTF8.GetByteCount(value)}:{value}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Temp() => Path.Combine(Path.GetTempPath(), "snow-globe-financial-" + Guid.NewGuid().ToString("N"));
    private static string ValidEmptyJournal() { string root = Temp(); using FileFinancialJournal _ = FileFinancialJournal.CreateNew(root, Header()); return root; }
    private static string ValidAdmittedJournal() { string root = Temp(); FinancialJournalHeader h = Header(); DirectWork w = Work(h); using (FileFinancialJournal j = FileFinancialJournal.CreateNew(root, h)) j.Apply(new AdmitAndReserveFinancialJournalCommand(w.Key, w.Digest, w.Job, 20)); return root; }
    private static string Copy(string source) { string target = Temp(); Directory.CreateDirectory(target); foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file))); return target; }
    private static byte[] ReadShared(string path) { using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using MemoryStream copy = new(); stream.CopyTo(copy); return copy.ToArray(); }
    private static void AssertRejectedUnchanged(string root)
    {
        string header = Path.Combine(root, FileFinancialJournal.HeaderFileName), records = Path.Combine(root, FileFinancialJournal.RecordsFileName);
        byte[] beforeHeader = File.ReadAllBytes(header), beforeRecords = File.ReadAllBytes(records);
        Assert.ThrowsAny<Exception>(() => FileFinancialJournal.ReadArchive(root));
        Assert.ThrowsAny<Exception>(() => FileFinancialJournal.OpenForAppend(root));
        Assert.Equal(beforeHeader, File.ReadAllBytes(header)); Assert.Equal(beforeRecords, File.ReadAllBytes(records));
    }
    private static void Delete(string root) { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private sealed record DirectWork(string Key, string Digest, PremiumCognitionJob Job);
    private static class FinancialJournalBoundsForTests { internal const int MaximumRecords = 4096; }

    private sealed class CountingAdapter : ISnowGlobeIdentifiedInferenceAdapter
    {
        private readonly SnowGlobeActionProposal _proposal; private readonly Action? _before; private int _calls;
        internal CountingAdapter(SnowGlobeActionProposal proposal, Action? before = null) { _proposal = proposal; _before = before; }
        public string AdapterIdentity => "test_local/v1"; public int Calls => _calls;
        public ValueTask<SnowGlobeActionProposal> ProposeAsync(SnowGlobeObservation observation, CancellationToken cancellationToken) { _before?.Invoke(); Interlocked.Increment(ref _calls); return ValueTask.FromResult(_proposal with { }); }
    }
    private sealed class CountingProvider : IPremiumCognitionProvider
    {
        private readonly PremiumCognitionProviderResult _result; private readonly Action? _before; private int _calls;
        internal CountingProvider(PremiumCognitionProviderResult result, Action? before = null) { _result = result; _before = before; }
        internal int Calls => _calls;
        public ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken) { _before?.Invoke(); Interlocked.Increment(ref _calls); return ValueTask.FromResult(_result); }
    }
    private sealed class GateProvider : IPremiumCognitionProvider
    {
        private readonly PremiumCognitionProviderResult _result; private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously); private int _calls;
        internal GateProvider(PremiumCognitionProviderResult result) { _result = result; }
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); internal int Calls => _calls;
        public async ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken) { Interlocked.Increment(ref _calls); Started.TrySetResult(); await _release.Task; return _result; }
        internal void Release() => _release.TrySetResult();
    }
    private sealed class ReentrantProvider : IPremiumCognitionProvider
    {
        private int _calls;
        internal SnowGlobeTwoTierCognitionModule? Module { get; set; }
        internal int Calls => _calls;
        public async ValueTask<PremiumCognitionProviderResult> SubmitOnceAsync(PremiumCognitionJob job, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Module!.ProposeAsync(job.Observation, cancellationToken);
            return Success();
        }
    }
    private sealed class AfterWriteFault : IFileFinancialJournalAppendFault
    {
        public void BeforeWrite(int sequence) { }
        public void AfterWriteBeforeFlush(int sequence) => throw new IOException("injected uncertain append");
    }
    private sealed class ProbeJournal : IFinancialJournal
    {
        private readonly IFinancialJournal _inner; internal ProbeJournal(IFinancialJournal inner) { _inner = inner; } internal bool Active { get; private set; }
        public FinancialJournalApplyResult Apply(FinancialJournalCommand command) { Active = true; try { return _inner.Apply(command); } finally { Active = false; } }
        public FinancialJournalReadResult Read(FinancialJournalQuery query) { Active = true; try { return _inner.Read(query); } finally { Active = false; } }
        public void Dispose() => _inner.Dispose();
    }
}
