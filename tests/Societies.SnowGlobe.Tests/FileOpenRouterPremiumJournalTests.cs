using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class FileOpenRouterPremiumJournalTests
{
    [Fact]
    public void ExactTransitionsFlushAndReopenThroughExistingJournalContract()
    {
        string root = Temp();
        try
        {
            OpenRouterPremiumJournalHeader header = Header();
            using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, header))
            {
                long one = journal.Admit(1, "cq1", Digest('a'), Digest('b'), 1_500);
                long two = journal.MarkDispatchUnknown(1, Digest('b'), one);
                Assert.Equal(3, journal.Complete(Receipt(), two));
                Assert.True(journal.RestartEvidence.FlushToDiskRequested);
            }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumJournalSlotSnapshot slot = Assert.Single(reopened.Snapshot().Slots);
            Assert.Equal(3, slot.Version);
            Assert.Equal(ChargeState.Settled, slot.ChargeState);
            Assert.True(reopened.RestartEvidence.RestartVerified);
            Assert.Equal(3, reopened.RestartEvidence.RecordCount);
            Assert.Matches("^[0-9a-f]{64}$", reopened.RestartEvidence.EvidenceDigestSha256);
            Assert.DoesNotContain(root, reopened.RestartEvidence.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData("provider_response_rejected")]
    [InlineData("provider_response_rejected_response_finish_invalid")]
    public void HistoricalProviderResponseRejectionJournalsRemainReadable(string outcomeCode)
    {
        string root = Temp();
        try
        {
            using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, Header()))
            {
                long admitted = journal.Admit(1, "cq1", Digest('a'), Digest('b'), 1_500);
                long dispatched = journal.MarkDispatchUnknown(1, Digest('b'), admitted);
                OpenRouterPremiumSlotReceipt historical = new(1, "cq1", Digest('a'), Digest('b'), Digest('c'),
                    SubmissionState.SubmissionUnknown, ChargeState.Unknown, 0, 0, 0, 0, null,
                    outcomeCode);
                Assert.Equal(3, journal.Complete(historical, dispatched));
            }

            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumJournalSlotSnapshot slot = Assert.Single(reopened.Snapshot().Slots);
            Assert.Equal(outcomeCode, slot.Receipt!.OutcomeCode);
            Assert.Contains($"cq1/completed/{outcomeCode}", reopened.Snapshot().Trace);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ConcurrentWriterAndReplayOrConflict_FailClosed()
    {
        string root = Temp();
        try
        {
            using FileOpenRouterPremiumJournal first = FileOpenRouterPremiumJournal.CreateNew(root, Header());
            Assert.Throws<IOException>(() => FileOpenRouterPremiumJournal.OpenForAppend(root));
            Assert.Equal(1, first.Admit(1, "cq1", Digest('a'), Digest('b'), 1_500));
            Assert.Equal("journal_sequence_invalid", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
                first.Admit(1, "cq1", Digest('a'), Digest('b'), 1_500)).Code);
            Assert.Equal("journal_sequence_invalid", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
                first.Admit(1, "cq1", Digest('a'), Digest('c'), 1_500)).Code);
            Assert.Single(first.Snapshot().Slots);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void StrictLoaderRejectsUnknownDuplicateTornOversizedDeepAndExtraArtifacts()
    {
        foreach (string mutation in new[] { "unknown", "duplicate", "torn", "oversized", "deep", "extra" })
        {
            string root = Temp();
            try
            {
                using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, Header())) { }
                string headerPath = Path.Combine(root, FileOpenRouterPremiumJournal.HeaderFileName);
                string recordsPath = Path.Combine(root, FileOpenRouterPremiumJournal.RecordsFileName);
                switch (mutation)
                {
                    case "unknown":
                        File.WriteAllText(headerPath, File.ReadAllText(headerPath).Replace("{", "{\"unknown\":0,", StringComparison.Ordinal), new UTF8Encoding(false));
                        break;
                    case "duplicate":
                        File.WriteAllText(headerPath, File.ReadAllText(headerPath).Replace("{", "{\"schema_version\":\"duplicate\",", StringComparison.Ordinal), new UTF8Encoding(false));
                        break;
                    case "torn": File.WriteAllText(recordsPath, "{\"schema_version\":", new UTF8Encoding(false)); break;
                    case "oversized": File.WriteAllBytes(recordsPath, new byte[FileOpenRouterPremiumJournal.MaximumTotalBytes + 1]); break;
                    case "deep": File.WriteAllText(recordsPath, "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{\"i\":0}}}}}}}}}\n", new UTF8Encoding(false)); break;
                    case "extra": File.WriteAllText(Path.Combine(root, "unexpected.txt"), "x", new UTF8Encoding(false)); break;
                }
                Assert.ThrowsAny<Exception>(() => FileOpenRouterPremiumJournal.OpenForAppend(root));
            }
            finally { Delete(root); }
        }
    }

    [Fact]
    public void AppendUncertaintyPoisonsWriterAndRestartNeverSilentlyReplaysProviderWork()
    {
        string root = Temp();
        try
        {
            ThrowAfterWrite fault = new();
            using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, Header(), fault))
            {
                Assert.Throws<IOException>(() => journal.Admit(1, "cq1", Digest('a'), Digest('b'), 1_500));
                Assert.True(journal.IsPoisoned);
                Assert.Throws<InvalidOperationException>(() => journal.Snapshot());
            }

            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumJournalSnapshot snapshot = reopened.Snapshot();
            Assert.InRange(snapshot.Slots.Count, 0, 1);
            if (snapshot.Slots.Count == 1)
            {
                Assert.Equal(SubmissionState.Dispatching, snapshot.Slots[0].SubmissionState);
                Assert.Equal(ChargeState.Reserved, snapshot.Slots[0].ChargeState);
            }
        }
        finally { Delete(root); }
    }

    [Fact]
    public void ReleasedAndUnknownExposureRemainExactAcrossRestart()
    {
        string root = Temp();
        try
        {
            using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, Header()))
            {
                long admitted = journal.Admit(1, "cq1", Digest('a'), Digest('b'), 1_500);
                OpenRouterPremiumSlotReceipt released = Receipt() with
                {
                    ResponseDigestSha256 = Digest('0'), SubmissionState = SubmissionState.DefinitelyNotSubmitted,
                    ChargeState = ChargeState.Released, PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0,
                    SettledMicrousd = 0, Proposal = null, OutcomeCode = "credential_acquisition_failed"
                };
                journal.CompleteBeforeDispatch(released, admitted);
                long second = journal.Admit(2, "cq2", Digest('c'), Digest('d'), 1_500);
                journal.MarkDispatchUnknown(2, Digest('d'), second);
            }
            using FileOpenRouterPremiumJournal reopened = FileOpenRouterPremiumJournal.OpenForAppend(root);
            OpenRouterPremiumJournalSnapshot snapshot = reopened.Snapshot();
            Assert.Equal(2, snapshot.Slots.Count);
            Assert.Equal(1_500, snapshot.ReservedExposureMicrousd);
            Assert.Equal(ChargeState.Released, snapshot.Slots[0].ChargeState);
            Assert.Equal(ChargeState.Unknown, snapshot.Slots[1].ChargeState);
            Assert.Equal(4, reopened.RestartEvidence.RecordCount);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void HeaderRejectsNonHexOpaqueAccountSuffix()
    {
        OpenRouterPremiumJournalHeader malformed = Header() with
        {
            AccountBindingIdentity = "byok-account-sha256-" + new string('g', 64)
        };
        Assert.Throws<InvalidDataException>(() => malformed.Validate(includeChecksum: false));
    }

    [Theory]
    [InlineData(FileOpenRouterPremiumJournal.HeaderFileName)]
    [InlineData(FileOpenRouterPremiumJournal.RecordsFileName)]
    [InlineData(FileOpenRouterPremiumJournal.WriterLeaseFileName)]
    public void HardLinkedFixedArtifactsAreRejected(string fileName)
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = Temp(); string aliases = Temp();
        try
        {
            using (FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, Header())) { }
            CreateHardLinkExact(Path.Combine(aliases, fileName + ".alias"), Path.Combine(root, fileName));
            Assert.Throws<InvalidDataException>(() => FileOpenRouterPremiumJournal.OpenForAppend(root));
        }
        finally { Delete(root); Delete(aliases); }
    }

    [Fact]
    public void OpenWriterDetectsPostOpenHardLinkAndPinsRootAgainstSwap()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = Temp(); string aliases = Temp(); string moved = root + "-moved";
        try
        {
            using FileOpenRouterPremiumJournal journal = FileOpenRouterPremiumJournal.CreateNew(root, Header());
            Assert.ThrowsAny<IOException>(() => Directory.Move(root, moved));
            CreateHardLinkExact(Path.Combine(aliases, "records.alias"), Path.Combine(root, FileOpenRouterPremiumJournal.RecordsFileName));
            Assert.Throws<InvalidDataException>(() => journal.Snapshot());
        }
        finally { Delete(root); Delete(moved); Delete(aliases); }
    }

    private static OpenRouterPremiumJournalHeader Header() => OpenRouterPremiumJournalHeader.Create(
        "openrouter-premium-journal/file-test", "openrouter-premium-run/file-test",
        OpenRouterPremiumProfileRegistry.Selected,
        new ByokAccountBindingIdentity("byok-account-sha256-" + new string('a', 64)));

    private static OpenRouterPremiumSlotReceipt Receipt() => new(1, "cq1", Digest('a'), Digest('b'), Digest('c'),
        SubmissionState.ResponseReceived, ChargeState.Settled, 10, 5, 15, 1,
        new SnowGlobeActionProposal("agent-001", SnowGlobeActionKind.Idle, 0), "premium_evidence_success");

    private static string Digest(char value) => new(value, 64);
    private static string Temp() { string path = Path.Combine(Path.GetTempPath(), "societies-openrouter-journal-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Delete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { } }

    private sealed class ThrowAfterWrite : IFileOpenRouterPremiumJournalAppendFault
    {
        public void BeforeWrite(int sequence) { }
        public void AfterWriteBeforeFlush(int sequence) => throw new IOException("simulated_crash_after_write");
    }

    private static void CreateHardLinkExact(string alias, string existing)
    {
        if (!CreateHardLink(alias, existing, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
}
