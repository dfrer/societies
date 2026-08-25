using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public enum ProviderRoutingAttemptState
{
    NotStarted = 0,
    DispatchStarted = 1,
    SubmissionUnknown = 2
}

public sealed record ProviderRoutingAttemptCreateInput(
    ReadOnlyMemory<byte> CurrentReadinessAssessmentCanonicalUtf8,
    ProviderRoutingIntent Intent,
    long CreatedAtUnixMilliseconds);

public sealed record ProviderRoutingDispatchClaimInput(
    string AttemptId,
    string ExpectedRecordDigestSha256,
    ProviderRoutingSelectedProvider SelectedProvider,
    ReadOnlyMemory<byte> RoutingDecisionCanonicalUtf8,
    long ClaimedAtUnixMilliseconds);

/// <summary>Detached authenticated evidence. It carries no provider execution capability.</summary>
public sealed class ProviderRoutingAttemptRecord
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _claimLimitationCodes;

    internal ProviderRoutingAttemptRecord(
        byte[] canonicalUtf8,
        ProviderRoutingAttemptState state,
        string attemptId,
        int sequence,
        string? previousRecordDigestSha256,
        long createdAtUnixMilliseconds,
        long expiresAtUnixMilliseconds,
        string comparisonArtifactDigestSha256,
        string readinessAssessmentDigestSha256,
        string readinessAssessmentSchemaVersion,
        string openRouterReadinessCode,
        string ollamaReadinessCode,
        string intentCode,
        ProviderRoutingSelectedProvider? selectedProvider,
        string? routingDecisionDigestSha256,
        long? claimedAtUnixMilliseconds,
        string terminalReasonCode,
        string integrityAnchorIdentitySha256,
        IReadOnlyList<string> claimLimitationCodes,
        string recordPayloadDigestSha256,
        string recordAuthenticatorSha256)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        State = state;
        AttemptId = attemptId;
        Sequence = sequence;
        PreviousRecordDigestSha256 = previousRecordDigestSha256;
        CreatedAtUnixMilliseconds = createdAtUnixMilliseconds;
        ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        ComparisonArtifactDigestSha256 = comparisonArtifactDigestSha256;
        ReadinessAssessmentDigestSha256 = readinessAssessmentDigestSha256;
        ReadinessAssessmentSchemaVersion = readinessAssessmentSchemaVersion;
        OpenRouterReadinessCode = openRouterReadinessCode;
        OllamaReadinessCode = ollamaReadinessCode;
        IntentCode = intentCode;
        SelectedProvider = selectedProvider;
        RoutingDecisionDigestSha256 = routingDecisionDigestSha256;
        ClaimedAtUnixMilliseconds = claimedAtUnixMilliseconds;
        TerminalReasonCode = terminalReasonCode;
        IntegrityAnchorIdentitySha256 = integrityAnchorIdentitySha256;
        RecordPayloadDigestSha256 = recordPayloadDigestSha256;
        RecordAuthenticatorSha256 = recordAuthenticatorSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => ProviderRoutingAttemptLedgerModule.RecordSchemaVersion;
    public ProviderRoutingAttemptState State { get; }
    public string AttemptId { get; }
    public int Sequence { get; }
    public string? PreviousRecordDigestSha256 { get; }
    public long CreatedAtUnixMilliseconds { get; }
    public long ExpiresAtUnixMilliseconds { get; }
    public string ComparisonArtifactDigestSha256 { get; }
    public string ReadinessAssessmentDigestSha256 { get; }
    public string ReadinessAssessmentSchemaVersion { get; }
    public string OpenRouterReadinessCode { get; }
    public string OllamaReadinessCode { get; }
    public string IntentCode { get; }
    public ProviderRoutingSelectedProvider? SelectedProvider { get; }
    public string? RoutingDecisionDigestSha256 { get; }
    public long? ClaimedAtUnixMilliseconds { get; }
    public string TerminalReasonCode { get; }
    public string IntegrityAnchorIdentitySha256 { get; }
    public IReadOnlyList<string> ClaimLimitationCodes =>
        Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string RecordPayloadDigestSha256 { get; }
    public string RecordAuthenticatorSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed class ProviderRoutingAttemptLedgerException : Exception
{
    internal ProviderRoutingAttemptLedgerException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "attempt_input_invalid" or "attempt_id_invalid" or "attempt_time_invalid" or
        "attempt_assessment_invalid" or "attempt_assessment_expired" or
        "attempt_already_exists" or "attempt_missing" or "attempt_expired" or
        "attempt_expected_record_mismatch" or "attempt_claim_binding_invalid" or
        "attempt_already_terminal" or "attempt_claim_outcome_ambiguous" or "attempt_poisoned" or
        "attempt_record_size_invalid" or "attempt_record_utf8_invalid" or
        "attempt_record_json_invalid" or "attempt_record_shape_invalid" or
        "attempt_record_value_invalid" or "attempt_record_digest_invalid" or
        "attempt_record_payload_digest_invalid" or "attempt_record_binding_invalid" or
        "attempt_record_noncanonical" or "attempt_storage_invalid" or
        "attempt_storage_unavailable" => code,
        _ => "attempt_ledger_failed"
    };
}

internal interface IProviderRoutingAttemptIntegrityAnchor
{
    string IdentitySha256 { get; }
    string Authenticate(ReadOnlySpan<byte> canonicalBytes);
    bool Verify(ReadOnlySpan<byte> canonicalBytes, string authenticatorSha256);
}

internal interface IProviderRoutingAttemptIdSource
{
    string NextAttemptId();
}

internal interface IProviderRoutingAttemptLedgerStorage
{
    string IntegrityAnchorIdentitySha256 { get; }
    void CreateNew(string attemptId, ReadOnlySpan<byte> initialRecordCanonicalUtf8);
    byte[] ReadCurrent(string attemptId);
    // Known closed ledger failures pass through. Every unexpected ClaimOnce failure must be
    // reported as ProviderRoutingAttemptStorageClaimException with exact exposure classification.
    byte[] ClaimOnce(
        string attemptId,
        string expectedRecordDigestSha256,
        ReadOnlySpan<byte> tombstoneCanonicalUtf8,
        ReadOnlySpan<byte> dispatchRecordCanonicalUtf8,
        ReadOnlySpan<byte> unknownRecordCanonicalUtf8);
}

internal enum ProviderRoutingAttemptStorageClaimExposure
{
    DefinitelyPreTombstone = 0,
    TerminalMaterialCreatedOrUnknown = 1
}

internal sealed class ProviderRoutingAttemptStorageClaimException : Exception
{
    internal ProviderRoutingAttemptStorageClaimException(
        ProviderRoutingAttemptStorageClaimExposure exposure)
        : base(exposure == ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone
            ? "attempt storage failed before claim tombstone creation"
            : "attempt storage claim outcome is terminal or unknown") => Exposure = exposure;

    internal ProviderRoutingAttemptStorageClaimExposure Exposure { get; }
}

internal sealed class RandomProviderRoutingAttemptIdSource : IProviderRoutingAttemptIdSource
{
    internal static RandomProviderRoutingAttemptIdSource Instance { get; } = new();
    private RandomProviderRoutingAttemptIdSource() { }
    public string NextAttemptId()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        try { return Convert.ToHexString(bytes).ToLowerInvariant(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

/// <summary>
/// Deep synchronous evidence Module. Construction is internal so no public root, storage,
/// integrity-anchor, provider transport, or execution composition is exposed.
/// </summary>
public sealed class ProviderRoutingAttemptLedgerModule
{
    public const string RecordSchemaVersion = "snow_globe_provider_routing_attempt_record/v2";
    public const string ContractSchemaVersion = "snow_globe_provider_routing_attempt_ledger_contract/v2";
    public const int MaximumRecordBytes = 8 * 1024;
    public const int MaximumRecordJsonDepth = 5;

    internal static readonly string[] ClaimLimitations =
    [
        "evidence_only_no_provider_execution_authority",
        "dispatch_claim_precedes_future_transport_bytes",
        "absence_never_proves_not_started_after_claim_material_exists",
        "no_retry_fallback_payment_generation_or_world_authority",
        "local_integrity_anchor_does_not_prevent_whole_volume_rollback"
    ];

    public static string ContractDigestSha256 { get; } = CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(
        ContractSchemaVersion + "|record_schema=" + RecordSchemaVersion +
        "|claim_tombstone_schema=" + ProviderRoutingAttemptLedgerCodec.TombstoneSchemaVersion +
        "|accepted_comparison_digest=" + ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256 +
        "|readiness_schema=" + ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion +
        "|readiness_contract_digest=" + ProviderRoutingReadinessEvidenceModule.CurrentContractDigestSha256 +
        "|routing_decision_schema=" + ProviderRoutingPolicyModule.DecisionSchemaVersion +
        "|routing_policy_digest=" + ProviderRoutingPolicyModule.PolicyDigestSha256 +
        "|states=not_started,dispatch_started,submission_unknown" +
        "|providers=openrouter,ollama|fresh_opaque_attempt_id=sha256_shape" +
        "|record_limits=8192_bytes,depth_5|tombstone_limits=4096_bytes,depth_4" +
        "|module_interface=create,inspect,claim_dispatch,validate" +
        "|file_layout=writer.lock;attempts/<sha256>/record-00000000.json," +
            "dispatch-claim-tombstone.json,record-00000001-dispatch-started.json," +
            "record-00000001-submission-unknown.json" +
        "|initial_binds_assessment_digest_intent_creation_and_expiry" +
        "|records_bind_assessment_readiness=openrouter_and_ollama_ready,not_ready,unknown" +
        "|claim_requires_exact_assessment_decision_readiness_coherence" +
        "|claim_binds_expected_record_provider_decision_digest_and_time" +
        "|claim_tombstone_durable_before_dispatch_started_success" +
        "|storage_claim_failure=definitely_pre_tombstone_or_terminal_material_created_or_unknown" +
        "|unclassified_storage_claim_failure=attempt_ledger_failed_no_ambiguity_evidence" +
        "|inspect=authenticated_current_record_or_closed_failure_never_absence_inference" +
        "|append_only_create_new_no_overwrite|restart_recovery_unknown_or_poisoned" +
        "|caller_inputs=one_owned_snapshot_zeroed|authenticated_records=external_provider_neutral_anchor" +
        "|limitations=" + string.Join(',', ClaimLimitations) +
        "|execution_authority=none|retry=failure_closed"));

    private readonly IProviderRoutingAttemptLedgerStorage _storage;
    private readonly IProviderRoutingAttemptIntegrityAnchor _anchor;
    private readonly IProviderRoutingAttemptIdSource _ids;

    internal ProviderRoutingAttemptLedgerModule(
        IProviderRoutingAttemptLedgerStorage storage,
        IProviderRoutingAttemptIntegrityAnchor anchor,
        IProviderRoutingAttemptIdSource? ids = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(anchor);
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(anchor.IdentitySha256)
            || !string.Equals(storage.IntegrityAnchorIdentitySha256, anchor.IdentitySha256,
                StringComparison.Ordinal))
            throw Failure("attempt_storage_invalid");
        _storage = storage;
        _anchor = anchor;
        _ids = ids ?? RandomProviderRoutingAttemptIdSource.Instance;
    }

    public ProviderRoutingAttemptRecord Create(ProviderRoutingAttemptCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        string intent = IntentCode(input.Intent);
        if (input.CreatedAtUnixMilliseconds <= 0
            || input.CreatedAtUnixMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            throw Failure("attempt_time_invalid");
        if (input.CurrentReadinessAssessmentCanonicalUtf8.Length is < 1
            or > ProviderRoutingReadinessEvidenceModule.MaximumCurrentAssessmentBytes)
            throw Failure("attempt_assessment_invalid");

        byte[] assessmentBytes = input.CurrentReadinessAssessmentCanonicalUtf8.Span.ToArray();
        try
        {
            ProviderRoutingCurrentReadinessAssessment assessment;
            try
            {
                assessment = ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
                    assessmentBytes, input.CreatedAtUnixMilliseconds);
            }
            catch (ProviderRoutingReadinessEvidenceException exception)
            {
                throw Failure(exception.Code == "assessment_binding_invalid"
                    ? "attempt_assessment_expired"
                    : "attempt_assessment_invalid");
            }
            if (assessment.GapCodes.Contains("accepted_comparison_evidence_unproven", StringComparer.Ordinal)
                || input.CreatedAtUnixMilliseconds >= assessment.ExpiresAtUnixMilliseconds)
                throw Failure("attempt_assessment_expired");

            string attemptId = _ids.NextAttemptId();
            if (!ProviderRoutingAttemptLedgerCodec.IsDigest(attemptId))
                throw Failure("attempt_id_invalid");
            ProviderRoutingAttemptRecord record;
            try
            {
                record = ProviderRoutingAttemptLedgerCodec.CreateRecord(
                    _anchor,
                    ProviderRoutingAttemptState.NotStarted,
                    attemptId,
                    sequence: 0,
                    previousRecordDigestSha256: null,
                    input.CreatedAtUnixMilliseconds,
                    assessment.ExpiresAtUnixMilliseconds,
                    ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
                    assessment.CanonicalDigestSha256,
                    ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion,
                    assessment.OpenRouterCurrentReadiness,
                    assessment.OllamaCurrentReadiness,
                    intent,
                    selectedProvider: null,
                    routingDecisionDigestSha256: null,
                    claimedAtUnixMilliseconds: null,
                    terminalReasonCode: "none");
            }
            catch (ProviderRoutingAttemptLedgerException) { throw; }
            catch { throw Failure("attempt_storage_invalid"); }
            byte[] detached = record.CanonicalUtf8.ToArray();
            try
            {
                try { _storage.CreateNew(attemptId, detached); }
                catch (ProviderRoutingAttemptLedgerException) { throw; }
                catch { throw Failure("attempt_storage_unavailable"); }
            }
            finally { CryptographicOperations.ZeroMemory(detached); }
            return record;
        }
        finally { CryptographicOperations.ZeroMemory(assessmentBytes); }
    }

    public ProviderRoutingAttemptRecord ClaimDispatch(ProviderRoutingDispatchClaimInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(input.AttemptId))
            throw Failure("attempt_id_invalid");
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(input.ExpectedRecordDigestSha256))
            throw Failure("attempt_expected_record_mismatch");
        string provider = ProviderCode(input.SelectedProvider);
        if (input.ClaimedAtUnixMilliseconds <= 0
            || input.ClaimedAtUnixMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            throw Failure("attempt_time_invalid");
        if (input.RoutingDecisionCanonicalUtf8.Length is < 1
            or > ProviderRoutingPolicyModule.MaximumDecisionBytes)
            throw Failure("attempt_claim_binding_invalid");

        byte[] decisionBytes = input.RoutingDecisionCanonicalUtf8.Span.ToArray();
        byte[] tombstoneBytes = [];
        byte[] dispatchBytes = [];
        byte[] unknownBytes = [];
        try
        {
            ProviderRoutingDecision decision;
            try { decision = ProviderRoutingPolicyModule.Validate(decisionBytes); }
            catch (ProviderRoutingPolicyException) { throw Failure("attempt_claim_binding_invalid"); }
            if (decision.SelectedProvider != input.SelectedProvider
                || decision.PrimaryAttemptStateCode != "not_started"
                || decision.ComparisonStatus != "accepted"
                || decision.ComparisonArtifactDigestSha256 !=
                    ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256)
                throw Failure("attempt_claim_binding_invalid");

            ProviderRoutingAttemptRecord current = Inspect(input.AttemptId);
            if (current.AttemptId != input.AttemptId)
                throw Failure("attempt_claim_binding_invalid");
            if (current.State != ProviderRoutingAttemptState.NotStarted)
                throw Failure("attempt_already_terminal");
            if (current.CanonicalDigestSha256 != input.ExpectedRecordDigestSha256)
                throw Failure("attempt_expected_record_mismatch");
            if (input.ClaimedAtUnixMilliseconds < current.CreatedAtUnixMilliseconds
                || input.ClaimedAtUnixMilliseconds > current.ExpiresAtUnixMilliseconds)
                throw Failure("attempt_expired");
            if (current.IntentCode != decision.IntentCode)
                throw Failure("attempt_claim_binding_invalid");
            if (current.OpenRouterReadinessCode != decision.OpenRouterReadinessCode
                || current.OllamaReadinessCode != decision.OllamaReadinessCode)
                throw Failure("attempt_claim_binding_invalid");

            ProviderRoutingAttemptClaimTombstone tombstone;
            ProviderRoutingAttemptRecord dispatch;
            ProviderRoutingAttemptRecord unknown;
            try
            {
                tombstone = ProviderRoutingAttemptLedgerCodec.CreateTombstone(
                    _anchor, current, provider, decision.CanonicalDigestSha256,
                    input.ClaimedAtUnixMilliseconds);
                dispatch = ProviderRoutingAttemptLedgerCodec.CreateRecord(
                    _anchor, ProviderRoutingAttemptState.DispatchStarted, current.AttemptId, 1,
                    current.CanonicalDigestSha256, current.CreatedAtUnixMilliseconds,
                    current.ExpiresAtUnixMilliseconds, current.ComparisonArtifactDigestSha256,
                    current.ReadinessAssessmentDigestSha256, current.ReadinessAssessmentSchemaVersion,
                    current.OpenRouterReadinessCode, current.OllamaReadinessCode,
                    current.IntentCode, input.SelectedProvider, decision.CanonicalDigestSha256,
                    input.ClaimedAtUnixMilliseconds, "dispatch_claimed");
                unknown = ProviderRoutingAttemptLedgerCodec.CreateRecord(
                    _anchor, ProviderRoutingAttemptState.SubmissionUnknown, current.AttemptId, 1,
                    current.CanonicalDigestSha256, current.CreatedAtUnixMilliseconds,
                    current.ExpiresAtUnixMilliseconds, current.ComparisonArtifactDigestSha256,
                    current.ReadinessAssessmentDigestSha256, current.ReadinessAssessmentSchemaVersion,
                    current.OpenRouterReadinessCode, current.OllamaReadinessCode,
                    current.IntentCode, input.SelectedProvider, decision.CanonicalDigestSha256,
                    input.ClaimedAtUnixMilliseconds, "claim_outcome_ambiguous");
            }
            catch (ProviderRoutingAttemptLedgerException) { throw; }
            catch { throw Failure("attempt_storage_invalid"); }
            tombstoneBytes = tombstone.CanonicalUtf8.ToArray();
            dispatchBytes = dispatch.CanonicalUtf8.ToArray();
            unknownBytes = unknown.CanonicalUtf8.ToArray();
            byte[] retained;
            try
            {
                retained = _storage.ClaimOnce(input.AttemptId, current.CanonicalDigestSha256,
                    tombstoneBytes, dispatchBytes, unknownBytes);
            }
            catch (ProviderRoutingAttemptStorageClaimException exception)
            {
                throw Failure(exception.Exposure switch
                {
                    ProviderRoutingAttemptStorageClaimExposure.DefinitelyPreTombstone =>
                        "attempt_storage_unavailable",
                    ProviderRoutingAttemptStorageClaimExposure.TerminalMaterialCreatedOrUnknown =>
                        "attempt_claim_outcome_ambiguous",
                    _ => "attempt_ledger_failed"
                });
            }
            catch (ProviderRoutingAttemptLedgerException) { throw; }
            catch { throw Failure("attempt_ledger_failed"); }
            try
            {
                ProviderRoutingAttemptRecord validated = Validate(retained);
                if (validated.State != ProviderRoutingAttemptState.DispatchStarted
                    || validated.CanonicalDigestSha256 != dispatch.CanonicalDigestSha256)
                    throw Failure("attempt_claim_outcome_ambiguous");
                return validated;
            }
            finally { CryptographicOperations.ZeroMemory(retained); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decisionBytes);
            CryptographicOperations.ZeroMemory(tombstoneBytes);
            CryptographicOperations.ZeroMemory(dispatchBytes);
            CryptographicOperations.ZeroMemory(unknownBytes);
        }
    }

    public ProviderRoutingAttemptRecord Inspect(string attemptId)
    {
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(attemptId))
            throw Failure("attempt_id_invalid");
        byte[] retained;
        try { retained = _storage.ReadCurrent(attemptId); }
        catch (ProviderRoutingAttemptLedgerException) { throw; }
        catch { throw Failure("attempt_storage_unavailable"); }
        try
        {
            ProviderRoutingAttemptRecord record = Validate(retained);
            if (record.AttemptId != attemptId)
                throw Failure("attempt_record_binding_invalid");
            return record;
        }
        finally { CryptographicOperations.ZeroMemory(retained); }
    }

    public ProviderRoutingAttemptRecord Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumRecordBytes)
            throw Failure("attempt_record_size_invalid");
        byte[] snapshot = canonicalUtf8.Span.ToArray();
        try
        {
            try { return ProviderRoutingAttemptLedgerCodec.ValidateRecord(snapshot, _anchor); }
            catch (ProviderRoutingAttemptLedgerException) { throw; }
            catch { throw Failure("attempt_record_binding_invalid"); }
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static string IntentCode(ProviderRoutingIntent intent) => intent switch
    {
        ProviderRoutingIntent.PreferredOnline => "preferred_online",
        ProviderRoutingIntent.LocalOnly => "local_only",
        _ => throw Failure("attempt_input_invalid")
    };

    private static string ProviderCode(ProviderRoutingSelectedProvider provider) => provider switch
    {
        ProviderRoutingSelectedProvider.OpenRouter => "openrouter",
        ProviderRoutingSelectedProvider.Ollama => "ollama",
        _ => throw Failure("attempt_claim_binding_invalid")
    };

    internal static ProviderRoutingAttemptLedgerException Failure(string code) => new(code);
}

internal sealed class ProviderRoutingAttemptClaimTombstone
{
    private readonly byte[] _canonicalUtf8;
    internal ProviderRoutingAttemptClaimTombstone(
        byte[] canonicalUtf8,
        string attemptId,
        string expectedRecordDigestSha256,
        string selectedProviderCode,
        string routingDecisionDigestSha256,
        long claimedAtUnixMilliseconds,
        string integrityAnchorIdentitySha256,
        string payloadDigestSha256,
        string authenticatorSha256)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        AttemptId = attemptId;
        ExpectedRecordDigestSha256 = expectedRecordDigestSha256;
        SelectedProviderCode = selectedProviderCode;
        RoutingDecisionDigestSha256 = routingDecisionDigestSha256;
        ClaimedAtUnixMilliseconds = claimedAtUnixMilliseconds;
        IntegrityAnchorIdentitySha256 = integrityAnchorIdentitySha256;
        PayloadDigestSha256 = payloadDigestSha256;
        AuthenticatorSha256 = authenticatorSha256;
    }
    internal string AttemptId { get; }
    internal string ExpectedRecordDigestSha256 { get; }
    internal string SelectedProviderCode { get; }
    internal string RoutingDecisionDigestSha256 { get; }
    internal long ClaimedAtUnixMilliseconds { get; }
    internal string IntegrityAnchorIdentitySha256 { get; }
    internal string PayloadDigestSha256 { get; }
    internal string AuthenticatorSha256 { get; }
    internal ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

internal static class ProviderRoutingAttemptLedgerCodec
{
    internal const string TombstoneSchemaVersion = "snow_globe_provider_routing_dispatch_claim_tombstone/v1";
    internal const int MaximumTombstoneBytes = 4 * 1024;
    private static readonly string[] RecordNames =
    [
        "schema_version", "state", "contract_schema_version", "contract_digest_sha256",
        "attempt_id", "sequence", "previous_record_digest_sha256", "created_at_unix_ms",
        "expires_at_unix_ms", "comparison_artifact_digest_sha256",
        "readiness_assessment_digest_sha256", "readiness_assessment_schema_version",
        "openrouter_readiness", "ollama_readiness", "intent",
        "selected_provider", "routing_decision_digest_sha256", "claimed_at_unix_ms",
        "terminal_reason_code", "integrity_anchor_identity_sha256", "claim_limitation_codes",
        "record_payload_digest_sha256", "record_authenticator_sha256"
    ];
    private static readonly string[] TombstoneNames =
    [
        "schema_version", "contract_digest_sha256", "attempt_id",
        "expected_record_digest_sha256", "selected_provider", "routing_decision_digest_sha256",
        "claimed_at_unix_ms", "integrity_anchor_identity_sha256", "tombstone_payload_digest_sha256",
        "tombstone_authenticator_sha256"
    ];

    internal static ProviderRoutingAttemptRecord CreateRecord(
        IProviderRoutingAttemptIntegrityAnchor anchor,
        ProviderRoutingAttemptState state,
        string attemptId,
        int sequence,
        string? previousRecordDigestSha256,
        long createdAtUnixMilliseconds,
        long expiresAtUnixMilliseconds,
        string comparisonArtifactDigestSha256,
        string readinessAssessmentDigestSha256,
        string readinessAssessmentSchemaVersion,
        string openRouterReadinessCode,
        string ollamaReadinessCode,
        string intentCode,
        ProviderRoutingSelectedProvider? selectedProvider,
        string? routingDecisionDigestSha256,
        long? claimedAtUnixMilliseconds,
        string terminalReasonCode)
    {
        ValidateRecordBindings(state, attemptId, sequence, previousRecordDigestSha256,
            createdAtUnixMilliseconds, expiresAtUnixMilliseconds, comparisonArtifactDigestSha256,
            readinessAssessmentDigestSha256, readinessAssessmentSchemaVersion,
            openRouterReadinessCode, ollamaReadinessCode, intentCode,
            selectedProvider, routingDecisionDigestSha256, claimedAtUnixMilliseconds,
            terminalReasonCode, anchor.IdentitySha256);
        byte[] payload = WriteRecord(state, attemptId, sequence, previousRecordDigestSha256,
            createdAtUnixMilliseconds, expiresAtUnixMilliseconds, comparisonArtifactDigestSha256,
            readinessAssessmentDigestSha256, readinessAssessmentSchemaVersion,
            openRouterReadinessCode, ollamaReadinessCode, intentCode,
            selectedProvider, routingDecisionDigestSha256, claimedAtUnixMilliseconds,
            terminalReasonCode, anchor.IdentitySha256, null, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        byte[] authenticated = WriteRecord(state, attemptId, sequence, previousRecordDigestSha256,
            createdAtUnixMilliseconds, expiresAtUnixMilliseconds, comparisonArtifactDigestSha256,
            readinessAssessmentDigestSha256, readinessAssessmentSchemaVersion,
            openRouterReadinessCode, ollamaReadinessCode, intentCode,
            selectedProvider, routingDecisionDigestSha256, claimedAtUnixMilliseconds,
            terminalReasonCode, anchor.IdentitySha256, payloadDigest, null);
        string authenticator;
        try { authenticator = anchor.Authenticate(authenticated); }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(authenticated);
        }
        if (!IsDigest(authenticator)) throw ProviderRoutingAttemptLedgerModule.Failure("attempt_storage_invalid");
        byte[] canonical = WriteRecord(state, attemptId, sequence, previousRecordDigestSha256,
            createdAtUnixMilliseconds, expiresAtUnixMilliseconds, comparisonArtifactDigestSha256,
            readinessAssessmentDigestSha256, readinessAssessmentSchemaVersion,
            openRouterReadinessCode, ollamaReadinessCode, intentCode,
            selectedProvider, routingDecisionDigestSha256, claimedAtUnixMilliseconds,
            terminalReasonCode, anchor.IdentitySha256, payloadDigest, authenticator);
        if (canonical.Length > ProviderRoutingAttemptLedgerModule.MaximumRecordBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_size_invalid");
        }
        return new(canonical, state, attemptId, sequence, previousRecordDigestSha256,
            createdAtUnixMilliseconds, expiresAtUnixMilliseconds, comparisonArtifactDigestSha256,
            readinessAssessmentDigestSha256, readinessAssessmentSchemaVersion,
            openRouterReadinessCode, ollamaReadinessCode, intentCode,
            selectedProvider, routingDecisionDigestSha256, claimedAtUnixMilliseconds,
            terminalReasonCode, anchor.IdentitySha256,
            ProviderRoutingAttemptLedgerModule.ClaimLimitations, payloadDigest, authenticator);
    }

    internal static ProviderRoutingAttemptRecord ValidateRecord(
        ReadOnlySpan<byte> canonicalUtf8,
        IProviderRoutingAttemptIntegrityAnchor anchor)
    {
        JsonDocument document = Parse(canonicalUtf8,
            ProviderRoutingAttemptLedgerModule.MaximumRecordJsonDepth, "attempt_record");
        using (document)
        {
            JsonElement root = document.RootElement;
            RequireOrder(root, RecordNames, "attempt_record_shape_invalid");
            RequireString(root, "schema_version", ProviderRoutingAttemptLedgerModule.RecordSchemaVersion);
            string stateCode = RequireClosed(root, "state", ["not_started", "dispatch_started", "submission_unknown"]);
            ProviderRoutingAttemptState state = stateCode switch
            {
                "not_started" => ProviderRoutingAttemptState.NotStarted,
                "dispatch_started" => ProviderRoutingAttemptState.DispatchStarted,
                _ => ProviderRoutingAttemptState.SubmissionUnknown
            };
            RequireString(root, "contract_schema_version", ProviderRoutingAttemptLedgerModule.ContractSchemaVersion);
            RequireString(root, "contract_digest_sha256", ProviderRoutingAttemptLedgerModule.ContractDigestSha256);
            string attemptId = RequireDigest(root, "attempt_id");
            int sequence = RequireInt32(root, "sequence");
            string? previous = RequireNullableDigest(root, "previous_record_digest_sha256");
            long createdAt = RequireInt64(root, "created_at_unix_ms");
            long expiresAt = RequireInt64(root, "expires_at_unix_ms");
            string comparison = RequireDigest(root, "comparison_artifact_digest_sha256");
            string assessment = RequireDigest(root, "readiness_assessment_digest_sha256");
            string assessmentSchema = RequireClosed(root, "readiness_assessment_schema_version",
                [ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion]);
            string openRouterReadiness = RequireClosed(root, "openrouter_readiness",
                ["ready", "not_ready", "unknown"]);
            string ollamaReadiness = RequireClosed(root, "ollama_readiness",
                ["ready", "not_ready", "unknown"]);
            string intent = RequireClosed(root, "intent", ["preferred_online", "local_only"]);
            string? providerCode = RequireNullableClosed(root, "selected_provider", ["openrouter", "ollama"]);
            ProviderRoutingSelectedProvider? provider = providerCode switch
            {
                "openrouter" => ProviderRoutingSelectedProvider.OpenRouter,
                "ollama" => ProviderRoutingSelectedProvider.Ollama,
                _ => null
            };
            string? decision = RequireNullableDigest(root, "routing_decision_digest_sha256");
            long? claimedAt = RequireNullableInt64(root, "claimed_at_unix_ms");
            string reason = RequireClosed(root, "terminal_reason_code",
                ["none", "dispatch_claimed", "claim_outcome_ambiguous"]);
            string anchorIdentity = RequireDigest(root, "integrity_anchor_identity_sha256");
            RequireStrings(root.GetProperty("claim_limitation_codes"),
                ProviderRoutingAttemptLedgerModule.ClaimLimitations);
            string payloadDigest = RequireDigest(root, "record_payload_digest_sha256");
            string authenticator = RequireDigest(root, "record_authenticator_sha256");
            ValidateRecordBindings(state, attemptId, sequence, previous, createdAt, expiresAt,
                comparison, assessment, assessmentSchema, openRouterReadiness, ollamaReadiness,
                intent, provider, decision, claimedAt, reason, anchorIdentity);
            if (anchorIdentity != anchor.IdentitySha256)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_binding_invalid");

            byte[] payload = CanonicalizeWithoutLast(root, 2);
            byte[] authenticated = CanonicalizeWithoutLast(root, 1);
            try
            {
                if (CognitionQualityHash.Sha256(payload) != payloadDigest)
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_payload_digest_invalid");
                if (!anchor.Verify(authenticated, authenticator))
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_binding_invalid");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
                CryptographicOperations.ZeroMemory(authenticated);
            }
            ProviderRoutingAttemptRecord recreated = CreateRecord(anchor, state, attemptId, sequence,
                previous, createdAt, expiresAt, comparison, assessment, assessmentSchema,
                openRouterReadiness, ollamaReadiness, intent, provider, decision, claimedAt, reason);
            if (!recreated.CanonicalUtf8.Span.SequenceEqual(canonicalUtf8))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_noncanonical");
            return recreated;
        }
    }

    internal static ProviderRoutingAttemptClaimTombstone CreateTombstone(
        IProviderRoutingAttemptIntegrityAnchor anchor,
        ProviderRoutingAttemptRecord current,
        string providerCode,
        string decisionDigest,
        long claimedAt)
    {
        if (current.State != ProviderRoutingAttemptState.NotStarted
            || providerCode is not ("openrouter" or "ollama")
            || !IsDigest(decisionDigest)
            || claimedAt < current.CreatedAtUnixMilliseconds
            || claimedAt > current.ExpiresAtUnixMilliseconds)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_claim_binding_invalid");
        byte[] payload = WriteTombstone(current.AttemptId, current.CanonicalDigestSha256,
            providerCode, decisionDigest, claimedAt, anchor.IdentitySha256, null, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        byte[] authenticated = WriteTombstone(current.AttemptId, current.CanonicalDigestSha256,
            providerCode, decisionDigest, claimedAt, anchor.IdentitySha256, payloadDigest, null);
        string authenticator;
        try { authenticator = anchor.Authenticate(authenticated); }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(authenticated);
        }
        byte[] canonical = WriteTombstone(current.AttemptId, current.CanonicalDigestSha256,
            providerCode, decisionDigest, claimedAt, anchor.IdentitySha256, payloadDigest, authenticator);
        if (canonical.Length > MaximumTombstoneBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_size_invalid");
        }
        return new(canonical, current.AttemptId, current.CanonicalDigestSha256, providerCode,
            decisionDigest, claimedAt, anchor.IdentitySha256, payloadDigest, authenticator);
    }

    internal static ProviderRoutingAttemptClaimTombstone ValidateTombstone(
        ReadOnlySpan<byte> canonicalUtf8,
        IProviderRoutingAttemptIntegrityAnchor anchor)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumTombstoneBytes)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
        JsonDocument document = Parse(canonicalUtf8, 4, "attempt_record");
        using (document)
        {
            JsonElement root = document.RootElement;
            RequireOrder(root, TombstoneNames, "attempt_record_shape_invalid");
            RequireString(root, "schema_version", TombstoneSchemaVersion);
            RequireString(root, "contract_digest_sha256", ProviderRoutingAttemptLedgerModule.ContractDigestSha256);
            string attempt = RequireDigest(root, "attempt_id");
            string expected = RequireDigest(root, "expected_record_digest_sha256");
            string provider = RequireClosed(root, "selected_provider", ["openrouter", "ollama"]);
            string decision = RequireDigest(root, "routing_decision_digest_sha256");
            long claimedAt = RequireInt64(root, "claimed_at_unix_ms");
            string anchorIdentity = RequireDigest(root, "integrity_anchor_identity_sha256");
            string payloadDigest = RequireDigest(root, "tombstone_payload_digest_sha256");
            string authenticator = RequireDigest(root, "tombstone_authenticator_sha256");
            if (anchorIdentity != anchor.IdentitySha256)
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            byte[] payload = CanonicalizeWithoutLast(root, 2);
            byte[] authenticated = CanonicalizeWithoutLast(root, 1);
            try
            {
                if (CognitionQualityHash.Sha256(payload) != payloadDigest
                    || !anchor.Verify(authenticated, authenticator))
                    throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
                CryptographicOperations.ZeroMemory(authenticated);
            }
            ProviderRoutingAttemptClaimTombstone recreated = CreateTombstoneFromValues(
                anchor, attempt, expected, provider, decision, claimedAt);
            if (!recreated.CanonicalUtf8.Span.SequenceEqual(canonicalUtf8))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
            return recreated;
        }
    }

    internal static ProviderRoutingAttemptRecord CreateUnknownFromTombstone(
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingAttemptClaimTombstone tombstone,
        IProviderRoutingAttemptIntegrityAnchor anchor)
    {
        if (initial.State != ProviderRoutingAttemptState.NotStarted
            || initial.AttemptId != tombstone.AttemptId
            || initial.CanonicalDigestSha256 != tombstone.ExpectedRecordDigestSha256)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_poisoned");
        ProviderRoutingSelectedProvider provider = tombstone.SelectedProviderCode == "openrouter"
            ? ProviderRoutingSelectedProvider.OpenRouter
            : ProviderRoutingSelectedProvider.Ollama;
        return CreateRecord(anchor, ProviderRoutingAttemptState.SubmissionUnknown,
            initial.AttemptId, 1, initial.CanonicalDigestSha256,
            initial.CreatedAtUnixMilliseconds, initial.ExpiresAtUnixMilliseconds,
            initial.ComparisonArtifactDigestSha256, initial.ReadinessAssessmentDigestSha256,
            initial.ReadinessAssessmentSchemaVersion, initial.OpenRouterReadinessCode,
            initial.OllamaReadinessCode, initial.IntentCode, provider,
            tombstone.RoutingDecisionDigestSha256, tombstone.ClaimedAtUnixMilliseconds,
            "claim_outcome_ambiguous");
    }

    private static ProviderRoutingAttemptClaimTombstone CreateTombstoneFromValues(
        IProviderRoutingAttemptIntegrityAnchor anchor,
        string attempt,
        string expected,
        string provider,
        string decision,
        long claimedAt)
    {
        byte[] payload = WriteTombstone(attempt, expected, provider, decision, claimedAt,
            anchor.IdentitySha256, null, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        byte[] authenticated = WriteTombstone(attempt, expected, provider, decision, claimedAt,
            anchor.IdentitySha256, payloadDigest, null);
        string authenticator;
        try { authenticator = anchor.Authenticate(authenticated); }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(authenticated);
        }
        byte[] canonical = WriteTombstone(attempt, expected, provider, decision, claimedAt,
            anchor.IdentitySha256, payloadDigest, authenticator);
        return new(canonical, attempt, expected, provider, decision, claimedAt,
            anchor.IdentitySha256, payloadDigest, authenticator);
    }

    private static void ValidateRecordBindings(
        ProviderRoutingAttemptState state,
        string attemptId,
        int sequence,
        string? previous,
        long createdAt,
        long expiresAt,
        string comparison,
        string assessment,
        string assessmentSchema,
        string openRouterReadiness,
        string ollamaReadiness,
        string intent,
        ProviderRoutingSelectedProvider? provider,
        string? decision,
        long? claimedAt,
        string reason,
        string anchorIdentity)
    {
        bool common = Enum.IsDefined(state) && IsDigest(attemptId)
            && createdAt > 0 && expiresAt > createdAt
            && expiresAt <= DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
            && comparison == ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256
            && IsDigest(assessment)
            && assessmentSchema == ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion
            && IsReadiness(openRouterReadiness)
            && IsReadiness(ollamaReadiness)
            && intent is "preferred_online" or "local_only"
            && IsDigest(anchorIdentity);
        bool stateValid = state switch
        {
            ProviderRoutingAttemptState.NotStarted => sequence == 0 && previous is null
                && provider is null && decision is null && claimedAt is null && reason == "none",
            ProviderRoutingAttemptState.DispatchStarted => sequence == 1 && IsDigest(previous)
                && IsProvider(provider) && IsDigest(decision) && claimedAt >= createdAt
                && claimedAt <= expiresAt && reason == "dispatch_claimed",
            ProviderRoutingAttemptState.SubmissionUnknown => sequence == 1 && IsDigest(previous)
                && IsProvider(provider) && IsDigest(decision) && claimedAt >= createdAt
                && claimedAt <= expiresAt && reason == "claim_outcome_ambiguous",
            _ => false
        };
        if (!common || !stateValid)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_binding_invalid");
    }

    private static bool IsProvider(ProviderRoutingSelectedProvider? provider) => provider is
        ProviderRoutingSelectedProvider.OpenRouter or ProviderRoutingSelectedProvider.Ollama;

    private static bool IsReadiness(string readiness) => readiness is
        "ready" or "not_ready" or "unknown";

    private static byte[] WriteRecord(
        ProviderRoutingAttemptState state,
        string attemptId,
        int sequence,
        string? previous,
        long createdAt,
        long expiresAt,
        string comparison,
        string assessment,
        string assessmentSchema,
        string openRouterReadiness,
        string ollamaReadiness,
        string intent,
        ProviderRoutingSelectedProvider? provider,
        string? decision,
        long? claimedAt,
        string reason,
        string anchorIdentity,
        string? payloadDigest,
        string? authenticator)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", ProviderRoutingAttemptLedgerModule.RecordSchemaVersion);
        writer.WriteString("state", state switch
        {
            ProviderRoutingAttemptState.NotStarted => "not_started",
            ProviderRoutingAttemptState.DispatchStarted => "dispatch_started",
            ProviderRoutingAttemptState.SubmissionUnknown => "submission_unknown",
            _ => throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_binding_invalid")
        });
        writer.WriteString("contract_schema_version", ProviderRoutingAttemptLedgerModule.ContractSchemaVersion);
        writer.WriteString("contract_digest_sha256", ProviderRoutingAttemptLedgerModule.ContractDigestSha256);
        writer.WriteString("attempt_id", attemptId);
        writer.WriteNumber("sequence", sequence);
        WriteNullable(writer, "previous_record_digest_sha256", previous);
        writer.WriteNumber("created_at_unix_ms", createdAt);
        writer.WriteNumber("expires_at_unix_ms", expiresAt);
        writer.WriteString("comparison_artifact_digest_sha256", comparison);
        writer.WriteString("readiness_assessment_digest_sha256", assessment);
        writer.WriteString("readiness_assessment_schema_version", assessmentSchema);
        writer.WriteString("openrouter_readiness", openRouterReadiness);
        writer.WriteString("ollama_readiness", ollamaReadiness);
        writer.WriteString("intent", intent);
        WriteNullable(writer, "selected_provider", provider switch
        {
            ProviderRoutingSelectedProvider.OpenRouter => "openrouter",
            ProviderRoutingSelectedProvider.Ollama => "ollama",
            null => null,
            _ => throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_binding_invalid")
        });
        WriteNullable(writer, "routing_decision_digest_sha256", decision);
        if (claimedAt.HasValue) writer.WriteNumber("claimed_at_unix_ms", claimedAt.Value);
        else writer.WriteNull("claimed_at_unix_ms");
        writer.WriteString("terminal_reason_code", reason);
        writer.WriteString("integrity_anchor_identity_sha256", anchorIdentity);
        WriteStrings(writer, "claim_limitation_codes", ProviderRoutingAttemptLedgerModule.ClaimLimitations);
        if (payloadDigest is not null) writer.WriteString("record_payload_digest_sha256", payloadDigest);
        if (authenticator is not null) writer.WriteString("record_authenticator_sha256", authenticator);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] WriteTombstone(
        string attempt,
        string expected,
        string provider,
        string decision,
        long claimedAt,
        string anchorIdentity,
        string? payloadDigest,
        string? authenticator)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", TombstoneSchemaVersion);
        writer.WriteString("contract_digest_sha256", ProviderRoutingAttemptLedgerModule.ContractDigestSha256);
        writer.WriteString("attempt_id", attempt);
        writer.WriteString("expected_record_digest_sha256", expected);
        writer.WriteString("selected_provider", provider);
        writer.WriteString("routing_decision_digest_sha256", decision);
        writer.WriteNumber("claimed_at_unix_ms", claimedAt);
        writer.WriteString("integrity_anchor_identity_sha256", anchorIdentity);
        if (payloadDigest is not null) writer.WriteString("tombstone_payload_digest_sha256", payloadDigest);
        if (authenticator is not null) writer.WriteString("tombstone_authenticator_sha256", authenticator);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static JsonDocument Parse(ReadOnlySpan<byte> bytes, int maxDepth, string prefix)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            RejectDuplicates(bytes, maxDepth);
            Utf8JsonReader reader = new(bytes, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maxDepth
            });
            JsonDocument document = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
            {
                document.Dispose();
                throw ProviderRoutingAttemptLedgerModule.Failure(prefix + "_json_invalid");
            }
            return document;
        }
        catch (DecoderFallbackException)
        {
            throw ProviderRoutingAttemptLedgerModule.Failure(prefix + "_utf8_invalid");
        }
        catch (ProviderRoutingAttemptLedgerException) { throw; }
        catch (JsonException)
        {
            throw ProviderRoutingAttemptLedgerModule.Failure(prefix + "_json_invalid");
        }
    }

    private static void RejectDuplicates(ReadOnlySpan<byte> bytes, int maxDepth)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions
        {
            MaxDepth = maxDepth,
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        Stack<HashSet<string>> stack = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
                stack.Push(new HashSet<string>(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject)
                stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName
                && (stack.Count == 0 || !stack.Peek().Add(reader.GetString()!)))
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_shape_invalid");
        }
    }

    private static void RequireOrder(JsonElement root, IReadOnlyList<string> names, string error)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw ProviderRoutingAttemptLedgerModule.Failure(error);
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length != names.Count)
            throw ProviderRoutingAttemptLedgerModule.Failure(error);
        for (int index = 0; index < names.Count; index++)
            if (properties[index].Name != names[index])
                throw ProviderRoutingAttemptLedgerModule.Failure(error);
    }

    private static void RequireString(JsonElement root, string name, string expected)
    {
        if (root.GetProperty(name).ValueKind != JsonValueKind.String
            || root.GetProperty(name).GetString() != expected)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_value_invalid");
    }

    private static string RequireClosed(JsonElement root, string name, IReadOnlyList<string> values)
    {
        JsonElement value = root.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || !values.Contains(text, StringComparer.Ordinal))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_value_invalid");
        return text;
    }

    private static string? RequireNullableClosed(JsonElement root, string name, IReadOnlyList<string> values)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        return RequireClosed(root, name, values);
    }

    private static string RequireDigest(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!IsDigest(text))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_digest_invalid");
        return text!;
    }

    private static string? RequireNullableDigest(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        return RequireDigest(root, name);
    }

    private static int RequireInt32(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_value_invalid");
        return result;
    }

    private static long RequireInt64(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_value_invalid");
        return result;
    }

    private static long? RequireNullableInt64(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        return RequireInt64(root, name);
    }

    private static void RequireStrings(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count)
            throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_value_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || item.GetString() != expected[index++])
                throw ProviderRoutingAttemptLedgerModule.Failure("attempt_record_value_invalid");
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement root, int count)
    {
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        foreach (JsonProperty property in properties[..^count])
        {
            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (string value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    internal static bool IsDigest(string? value) => value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
