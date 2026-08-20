namespace Societies.SnowGlobe;

internal enum OllamaRecordingTerminalCheckpointCode
{
    None,
    BeforeDispatch,
    RequestDispatch,
    ResponseHeaders,
    ResponseBody,
    AfterExchange,
    WrapperDecode,
    EvidenceConstruction,
    Authorization,
    Composition
}

internal enum OllamaRecordingTerminalPolicyCode
{
    None,
    Capability,
    RuntimeBinding,
    RuntimeOwnership,
    TransportState,
    RequestPolicy,
    Cancellation,
    Timeout,
    TransportIo,
    HttpStatus,
    HttpVersion,
    Redirect,
    TransferEncoding,
    ContentType,
    ContentEncoding,
    ContentLength,
    BodyBounds,
    BodyRead,
    Trailer,
    WrapperShape,
    EvidenceShape,
    Authorization,
    UnexpectedException
}

internal readonly record struct OllamaRecordingTerminalFacts(
    OllamaRecordingCompositionOutcomeCode CompositionOutcome,
    OllamaRecordingCompositionFailureCode CompositionFailure,
    bool RecordingResultPresent,
    SnowGlobeOllamaLoopbackRecordingOutcomeCode? RecordingOutcome,
    SnowGlobeOllamaLoopbackRecordingFailureCode? RecordingFailure,
    int? CompletedSlotCount,
    int? TerminalSlotOrdinal,
    SubmissionState? SubmissionState,
    int? StatusCode,
    bool ReceiptPresent,
    bool TerminalReceiptRowPresent,
    bool TerminalWrapperDigestPresent,
    bool NestedEvidenceDigestPresent,
    OllamaRecordingTerminalCheckpointCode? Checkpoint,
    OllamaRecordingTerminalPolicyCode? Policy);

/// <summary>Single raw-free semantic authority shared by artifact validation and the isolated CLI.</summary>
internal static class OllamaRecordingTerminalCoherenceModule
{
    internal static bool IsValid(OllamaRecordingTerminalFacts facts)
    {
        if (facts.CompositionOutcome == OllamaRecordingCompositionOutcomeCode.AuthorizationRejected)
            return IsCompositionOnly(facts, OllamaRecordingCompositionFailureCode.AuthorizationRejected,
                OllamaRecordingTerminalCheckpointCode.Authorization, OllamaRecordingTerminalPolicyCode.Authorization);
        if (facts.CompositionOutcome == OllamaRecordingCompositionOutcomeCode.CompositionFailed)
            return IsCompositionOnly(facts, OllamaRecordingCompositionFailureCode.CompositionFailed,
                OllamaRecordingTerminalCheckpointCode.Composition, OllamaRecordingTerminalPolicyCode.UnexpectedException);
        if (!facts.RecordingResultPresent || facts.RecordingOutcome is null || facts.RecordingFailure is null
            || facts.CompletedSlotCount is null || facts.SubmissionState is null || facts.Checkpoint is null || facts.Policy is null)
            return false;

        int completed = facts.CompletedSlotCount.Value;
        if (completed is < 0 or > CognitionQualityCorpusV1.ScenarioCount
            || facts.StatusCode is < 100 or > 599)
            return false;

        if (facts.RecordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete)
            return facts.CompositionOutcome == OllamaRecordingCompositionOutcomeCode.Complete
                && facts.CompositionFailure == OllamaRecordingCompositionFailureCode.None
                && facts.RecordingFailure == SnowGlobeOllamaLoopbackRecordingFailureCode.None
                && completed == CognitionQualityCorpusV1.ScenarioCount
                && facts.TerminalSlotOrdinal is null
                && facts.SubmissionState == Societies.SnowGlobe.SubmissionState.ResponseReceived
                && facts.StatusCode == 200
                && facts.ReceiptPresent && !facts.TerminalReceiptRowPresent
                && !facts.TerminalWrapperDigestPresent && facts.NestedEvidenceDigestPresent
                && facts.Checkpoint == OllamaRecordingTerminalCheckpointCode.None
                && facts.Policy == OllamaRecordingTerminalPolicyCode.None;

        if (facts.RecordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed
            && facts.RecordingFailure == SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityExpired)
            return facts.CompositionOutcome == OllamaRecordingCompositionOutcomeCode.Failed
                && facts.CompositionFailure == OllamaRecordingCompositionFailureCode.CapabilityExpired
                && completed == 0 && facts.TerminalSlotOrdinal is null
                && facts.SubmissionState == Societies.SnowGlobe.SubmissionState.DefinitelyNotSubmitted
                && facts.StatusCode is null && !facts.ReceiptPresent && !facts.TerminalReceiptRowPresent
                && !facts.TerminalWrapperDigestPresent && !facts.NestedEvidenceDigestPresent
                && facts.Checkpoint == OllamaRecordingTerminalCheckpointCode.BeforeDispatch
                && facts.Policy == OllamaRecordingTerminalPolicyCode.Capability;

        if (facts.RecordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed
            && facts.RecordingFailure == SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected)
            return facts.CompositionOutcome == OllamaRecordingCompositionOutcomeCode.Failed
                && facts.CompositionFailure == OllamaRecordingCompositionFailureCode.EvidenceRejected
                && completed == CognitionQualityCorpusV1.ScenarioCount
                && facts.TerminalSlotOrdinal == CognitionQualityCorpusV1.ScenarioCount
                && facts.SubmissionState == Societies.SnowGlobe.SubmissionState.ResponseReceived
                && facts.StatusCode == 200 && facts.ReceiptPresent
                && !facts.TerminalReceiptRowPresent && !facts.TerminalWrapperDigestPresent
                && !facts.NestedEvidenceDigestPresent
                && facts.Checkpoint == OllamaRecordingTerminalCheckpointCode.EvidenceConstruction
                && facts.Policy == OllamaRecordingTerminalPolicyCode.EvidenceShape;

        if (completed >= CognitionQualityCorpusV1.ScenarioCount
            || facts.TerminalSlotOrdinal != completed + 1
            || facts.NestedEvidenceDigestPresent)
            return false;

        if (facts.RecordingOutcome is SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled or SnowGlobeOllamaLoopbackRecordingOutcomeCode.TimedOut)
            return IsCancellationOrTimeout(facts);
        if (facts.RecordingOutcome != SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed
            || facts.CompositionOutcome != OllamaRecordingCompositionOutcomeCode.Failed
            || !Enum.TryParse(facts.RecordingFailure.Value.ToString(), false, out OllamaRecordingCompositionFailureCode mapped)
            || facts.CompositionFailure != mapped)
            return false;

        return facts.RecordingFailure.Value switch
        {
            SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeBindingInvalid =>
                IsExact(facts, SubmissionState.DefinitelyNotSubmitted, null, receipt: true, row: false, wrapper: false,
                    OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.RuntimeBinding),
            SnowGlobeOllamaLoopbackRecordingFailureCode.RuntimeChanged => IsRuntimeChanged(facts),
            SnowGlobeOllamaLoopbackRecordingFailureCode.TransportPoisoned =>
                IsExact(facts, SubmissionState.DefinitelyNotSubmitted, null, receipt: true, row: true, wrapper: false,
                    OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.TransportState),
            SnowGlobeOllamaLoopbackRecordingFailureCode.TransportFailure => IsTransportFailure(facts),
            SnowGlobeOllamaLoopbackRecordingFailureCode.HttpResponseRejected => IsHttpResponseRejected(facts),
            SnowGlobeOllamaLoopbackRecordingFailureCode.ResponseBodyRejected => IsResponseBodyRejected(facts),
            SnowGlobeOllamaLoopbackRecordingFailureCode.WrapperRejected =>
                IsExact(facts, SubmissionState.ResponseReceived, 200, receipt: true, row: true, wrapper: true,
                    OllamaRecordingTerminalCheckpointCode.WrapperDecode, OllamaRecordingTerminalPolicyCode.WrapperShape),
            _ => false
        };
    }

    internal static bool TryParseAndValidate(
        string compositionOutcome,
        string compositionFailure,
        bool recordingResultPresent,
        string? recordingOutcome,
        string? recordingFailure,
        int? completed,
        int? terminalSlot,
        string? submission,
        int? status,
        bool receiptPresent,
        bool terminalRowPresent,
        bool wrapperPresent,
        bool nestedEvidencePresent,
        string? checkpoint,
        string? policy)
    {
        if (!TryDefined(compositionOutcome, out OllamaRecordingCompositionOutcomeCode parsedCompositionOutcome)
            || !TryDefined(compositionFailure, out OllamaRecordingCompositionFailureCode parsedCompositionFailure)
            || !TryNullableDefined(recordingOutcome, out SnowGlobeOllamaLoopbackRecordingOutcomeCode? parsedRecordingOutcome)
            || !TryNullableDefined(recordingFailure, out SnowGlobeOllamaLoopbackRecordingFailureCode? parsedRecordingFailure)
            || !TryNullableDefined(submission, out SubmissionState? parsedSubmission)
            || !TryNullableDefined(checkpoint, out OllamaRecordingTerminalCheckpointCode? parsedCheckpoint)
            || !TryNullableDefined(policy, out OllamaRecordingTerminalPolicyCode? parsedPolicy))
            return false;
        return IsValid(new(parsedCompositionOutcome, parsedCompositionFailure, recordingResultPresent,
            parsedRecordingOutcome, parsedRecordingFailure, completed, terminalSlot, parsedSubmission, status,
            receiptPresent, terminalRowPresent, wrapperPresent, nestedEvidencePresent, parsedCheckpoint, parsedPolicy));
    }

    private static bool IsCompositionOnly(
        OllamaRecordingTerminalFacts facts,
        OllamaRecordingCompositionFailureCode failure,
        OllamaRecordingTerminalCheckpointCode checkpoint,
        OllamaRecordingTerminalPolicyCode policy) =>
        facts.CompositionFailure == failure && !facts.RecordingResultPresent
        && facts.RecordingOutcome is null && facts.RecordingFailure is null
        && facts.CompletedSlotCount is null && facts.TerminalSlotOrdinal is null
        && facts.SubmissionState is null && facts.StatusCode is null
        && !facts.ReceiptPresent && !facts.TerminalReceiptRowPresent
        && !facts.TerminalWrapperDigestPresent && !facts.NestedEvidenceDigestPresent
        && facts.Checkpoint == checkpoint && facts.Policy == policy;

    private static bool IsCancellationOrTimeout(OllamaRecordingTerminalFacts facts)
    {
        bool cancelled = facts.RecordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled;
        if (facts.CompositionOutcome != (cancelled ? OllamaRecordingCompositionOutcomeCode.Cancelled : OllamaRecordingCompositionOutcomeCode.TimedOut)
            || facts.CompositionFailure != (cancelled ? OllamaRecordingCompositionFailureCode.Cancelled : OllamaRecordingCompositionFailureCode.TimedOut)
            || facts.RecordingFailure != SnowGlobeOllamaLoopbackRecordingFailureCode.None
            || facts.TerminalWrapperDigestPresent)
            return false;
        OllamaRecordingTerminalPolicyCode expectedPolicy = cancelled
            ? OllamaRecordingTerminalPolicyCode.Cancellation
            : OllamaRecordingTerminalPolicyCode.Timeout;
        if (facts.Policy != expectedPolicy)
            return false;
        return facts.Checkpoint switch
        {
            OllamaRecordingTerminalCheckpointCode.BeforeDispatch =>
                facts.SubmissionState == SubmissionState.DefinitelyNotSubmitted && facts.StatusCode is null
                && !facts.TerminalReceiptRowPresent,
            OllamaRecordingTerminalCheckpointCode.RequestDispatch =>
                facts.SubmissionState is SubmissionState.DefinitelyNotSubmitted or SubmissionState.SubmissionUnknown
                && facts.StatusCode is null && facts.ReceiptPresent && facts.TerminalReceiptRowPresent,
            OllamaRecordingTerminalCheckpointCode.ResponseHeaders =>
                facts.SubmissionState == SubmissionState.ResponseReceived && facts.StatusCode is >= 100 and <= 599
                && facts.ReceiptPresent && facts.TerminalReceiptRowPresent,
            OllamaRecordingTerminalCheckpointCode.ResponseBody =>
                facts.SubmissionState == SubmissionState.ResponseReceived && facts.StatusCode == 200
                && facts.ReceiptPresent && facts.TerminalReceiptRowPresent,
            _ => false
        };
    }

    private static bool IsRuntimeChanged(OllamaRecordingTerminalFacts facts) =>
        IsExact(facts, SubmissionState.DefinitelyNotSubmitted, null, true, true, false,
            OllamaRecordingTerminalCheckpointCode.BeforeDispatch, OllamaRecordingTerminalPolicyCode.RuntimeOwnership)
        || (facts.SubmissionState == SubmissionState.ResponseReceived && facts.StatusCode is >= 100 and <= 599
            && facts.ReceiptPresent && facts.TerminalReceiptRowPresent && !facts.TerminalWrapperDigestPresent
            && facts.Checkpoint == OllamaRecordingTerminalCheckpointCode.ResponseHeaders
            && facts.Policy == OllamaRecordingTerminalPolicyCode.RuntimeOwnership)
        || IsExact(facts, SubmissionState.ResponseReceived, 200, true, true, true,
            OllamaRecordingTerminalCheckpointCode.AfterExchange, OllamaRecordingTerminalPolicyCode.RuntimeOwnership);

    private static bool IsTransportFailure(OllamaRecordingTerminalFacts facts) =>
        (facts.SubmissionState is SubmissionState.DefinitelyNotSubmitted or SubmissionState.SubmissionUnknown
            && facts.StatusCode is null && facts.ReceiptPresent && facts.TerminalReceiptRowPresent
            && !facts.TerminalWrapperDigestPresent
            && facts.Checkpoint == OllamaRecordingTerminalCheckpointCode.RequestDispatch
            && facts.Policy == OllamaRecordingTerminalPolicyCode.TransportIo)
        || IsExact(facts, SubmissionState.ResponseReceived, 200, true, true, false,
            OllamaRecordingTerminalCheckpointCode.ResponseBody, OllamaRecordingTerminalPolicyCode.BodyRead);

    private static bool IsHttpResponseRejected(OllamaRecordingTerminalFacts facts)
    {
        if (facts.SubmissionState != SubmissionState.ResponseReceived || facts.StatusCode is not (>= 100 and <= 599)
            || !facts.ReceiptPresent || !facts.TerminalReceiptRowPresent || facts.TerminalWrapperDigestPresent
            || facts.Checkpoint != OllamaRecordingTerminalCheckpointCode.ResponseHeaders)
            return false;
        if (facts.StatusCode != 200)
            return facts.Policy == OllamaRecordingTerminalPolicyCode.HttpStatus;
        return facts.Policy is OllamaRecordingTerminalPolicyCode.HttpVersion
            or OllamaRecordingTerminalPolicyCode.Redirect
            or OllamaRecordingTerminalPolicyCode.TransferEncoding
            or OllamaRecordingTerminalPolicyCode.ContentType
            or OllamaRecordingTerminalPolicyCode.ContentEncoding
            or OllamaRecordingTerminalPolicyCode.ContentLength
            or OllamaRecordingTerminalPolicyCode.Trailer;
    }

    private static bool IsResponseBodyRejected(OllamaRecordingTerminalFacts facts) =>
        facts.SubmissionState == SubmissionState.ResponseReceived && facts.StatusCode == 200
        && facts.ReceiptPresent && facts.TerminalReceiptRowPresent && !facts.TerminalWrapperDigestPresent
        && facts.Checkpoint == OllamaRecordingTerminalCheckpointCode.ResponseBody
        && facts.Policy is OllamaRecordingTerminalPolicyCode.BodyRead
            or OllamaRecordingTerminalPolicyCode.BodyBounds
            or OllamaRecordingTerminalPolicyCode.Trailer;

    private static bool IsExact(
        OllamaRecordingTerminalFacts facts,
        SubmissionState submission,
        int? status,
        bool receipt,
        bool row,
        bool wrapper,
        OllamaRecordingTerminalCheckpointCode checkpoint,
        OllamaRecordingTerminalPolicyCode policy) =>
        facts.SubmissionState == submission && facts.StatusCode == status
        && facts.ReceiptPresent == receipt && facts.TerminalReceiptRowPresent == row
        && facts.TerminalWrapperDigestPresent == wrapper
        && facts.Checkpoint == checkpoint && facts.Policy == policy;

    private static bool TryDefined<T>(string? value, out T parsed) where T : struct, Enum
    {
        parsed = default;
        bool accepted = value is not null && Enum.TryParse(value, false, out parsed) && Enum.IsDefined(parsed)
            && string.Equals(parsed.ToString(), value, StringComparison.Ordinal);
        if (!accepted) parsed = default;
        return accepted;
    }

    private static bool TryNullableDefined<T>(string? value, out T? parsed) where T : struct, Enum
    {
        if (value is null) { parsed = null; return true; }
        if (TryDefined(value, out T concrete)) { parsed = concrete; return true; }
        parsed = null; return false;
    }
}
