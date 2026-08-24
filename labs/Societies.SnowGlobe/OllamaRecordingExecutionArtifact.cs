using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed class OllamaRecordingExecutionArtifactException : Exception
{
    internal OllamaRecordingExecutionArtifactException(string code) : base(CloseCode(code)) => Code = CloseCode(code);
    public string Code { get; }

    private static string CloseCode(string code) => code switch
    {
        "artifact_size_invalid" or "artifact_utf8_bom_rejected" or "artifact_utf8_invalid" or
        "artifact_json_invalid" or "artifact_trailing_content_rejected" or "artifact_noncanonical" or
        "artifact_shape_invalid" or "artifact_result_shape_invalid" or "artifact_receipt_shape_invalid" or
        "artifact_receipt_slot_shape_invalid" or "artifact_value_invalid" or "artifact_digest_invalid" or
        "artifact_binding_invalid" or "artifact_runtime_binding_invalid" or "artifact_repository_root_binding_invalid" or
        "artifact_enum_invalid" or "artifact_number_invalid" or "artifact_boolean_invalid" or
        "artifact_result_coherence_invalid" or "artifact_complete_result_invalid" or "artifact_terminal_result_invalid" or
        "artifact_terminal_submission_invalid" or "artifact_failed_result_invalid" or
        "artifact_composition_failure_coherence_invalid" or "artifact_receipt_nullability_invalid" or
        "artifact_receipt_digest_invalid" or "artifact_receipt_binding_invalid" or
        "artifact_receipt_slots_invalid" or "artifact_receipt_slot_status_invalid" or
        "artifact_receipt_completed_slot_invalid" or "artifact_receipt_terminal_slot_invalid" or
        "artifact_receipt_result_binding_invalid" or "artifact_receipt_payload_digest_invalid" or
        "artifact_score_summary_nullability_invalid" or "artifact_score_summary_shape_invalid" or
        "artifact_score_summary_invalid" or "artifact_score_summary_binding_invalid" or "artifact_score_summary_terminal_invalid" or
        "artifact_normalized_proposals_nullability_invalid" or "artifact_normalized_proposals_shape_invalid" or
        "artifact_normalized_proposals_invalid" or "artifact_normalized_proposals_binding_invalid" or "artifact_normalized_proposals_terminal_invalid" or
        "artifact_claims_invalid" or "artifact_receipt_claims_invalid" or "artifact_payload_digest_invalid" => code,
        _ => "artifact_validation_failed"
    };
}

/// <summary>Detached, canonical, raw-free execution binding. Digests provide integrity, not authenticity.</summary>
public sealed class OllamaRecordingExecutionArtifact
{
    private readonly byte[] _canonicalUtf8;

    internal OllamaRecordingExecutionArtifact(
        byte[] canonicalUtf8,
        string payloadDigestSha256,
        string schemaVersion,
        string relativeArtifactPath,
        string repositoryRootDigestSha256,
        string compositionOutcomeCode,
        string compositionFailureCode,
        bool recordingResultPresent,
        string? recordingOutcomeCode,
        string? recordingFailureCode,
        int? completedSlotCount,
        int? terminalSlotOrdinal,
        string? terminalSubmissionState,
        string? terminalChargeState,
        int? terminalStatusCode,
        string terminalCheckpointCode,
        string terminalPolicyCode,
        bool receiptPresent,
        bool terminalReceiptRowPresent,
        bool terminalWrapperDigestPresent,
        string? receiptDigestSha256,
        string? nestedRecordingEvidenceDigestSha256,
        CognitionQualityScoreSummary? scoreSummary,
        CognitionQualityNormalizedProposalEvidence? normalizedProposalEvidence)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
        SchemaVersion = schemaVersion;
        RelativeArtifactPath = relativeArtifactPath;
        RepositoryRootDigestSha256 = repositoryRootDigestSha256;
        OutcomeCode = compositionOutcomeCode;
        FailureCode = compositionFailureCode;
        RecordingResultPresent = recordingResultPresent;
        RecordingOutcomeCode = recordingOutcomeCode;
        RecordingFailureCode = recordingFailureCode;
        CompletedSlotCount = completedSlotCount;
        TerminalSlotOrdinal = terminalSlotOrdinal;
        TerminalSubmissionState = terminalSubmissionState;
        TerminalChargeState = terminalChargeState is null ? null : Enum.Parse<ChargeState>(terminalChargeState, ignoreCase: false);
        TerminalStatusCode = terminalStatusCode;
        TerminalCheckpointCode = terminalCheckpointCode;
        TerminalPolicyCode = terminalPolicyCode;
        ReceiptPresent = receiptPresent;
        TerminalReceiptRowPresent = terminalReceiptRowPresent;
        TerminalWrapperDigestPresent = terminalWrapperDigestPresent;
        ReceiptDigestSha256 = receiptDigestSha256;
        NestedRecordingEvidenceDigestSha256 = nestedRecordingEvidenceDigestSha256;
        ScoreSummary = scoreSummary is null ? null : CognitionQualityScoreSummaryCodec.Validate(scoreSummary.CanonicalUtf8);
        ScoreSummaryDigestSha256 = ScoreSummary?.CanonicalDigestSha256;
        NormalizedProposalEvidence = normalizedProposalEvidence is null ? null : CognitionQualityNormalizedProposalEvidenceCodec.Validate(normalizedProposalEvidence.CanonicalUtf8);
        NormalizedProposalEvidenceDigestSha256 = NormalizedProposalEvidence?.CanonicalDigestSha256;
    }

    public string SchemaVersion { get; }
    public string Semantics => OllamaRecordingExecutionArtifactModule.Semantics;
    public string RelativeArtifactPath { get; }
    public string RepositoryRootDigestSha256 { get; }
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
    public string OutcomeCode { get; }
    public string FailureCode { get; }
    public bool RecordingResultPresent { get; }
    public string? RecordingOutcomeCode { get; }
    public string? RecordingFailureCode { get; }
    public int? CompletedSlotCount { get; }
    public int? TerminalSlotOrdinal { get; }
    public string? TerminalSubmissionState { get; }
    public ChargeState? TerminalChargeState { get; }
    public int? TerminalStatusCode { get; }
    public string TerminalCheckpointCode { get; }
    public string TerminalPolicyCode { get; }
    public bool ReceiptPresent { get; }
    public string? ReceiptDigestSha256 { get; }
    public string? NestedRecordingEvidenceDigestSha256 { get; }
    public CognitionQualityScoreSummary? ScoreSummary { get; }
    public string? ScoreSummaryDigestSha256 { get; }
    public CognitionQualityNormalizedProposalEvidence? NormalizedProposalEvidence { get; }
    public string? NormalizedProposalEvidenceDigestSha256 { get; }
    public bool AdditionalAttemptAuthorized => false;
    public bool HasIndependentArtifactLoadedProof => false;
    public bool HasIndependentModelExecutionProof => false;
    public bool HasLiveCompatibilityProof => false;
    public bool HasBenchmarkOrQualityClaim => false;
    public bool HasWorldOrSimulationAuthority => false;
    public bool HasRetryAuthority => false;
    internal bool TerminalReceiptRowPresent { get; }
    internal bool TerminalWrapperDigestPresent { get; }
}

internal sealed record OllamaRecordingArtifactSnapshot(
    string CompositionOutcomeCode,
    string CompositionFailureCode,
    bool RecordingResultPresent,
    string? RecordingOutcomeCode,
    string? RecordingFailureCode,
    int? CompletedSlotCount,
    int? TerminalSlotOrdinal,
    string? TerminalSubmissionState,
    string? TerminalChargeState,
    int? TerminalStatusCode,
    string? TerminalCheckpointCode,
    string? TerminalPolicyCode,
    ReadOnlyMemory<byte>? ReceiptCanonicalUtf8,
    string? ReceiptDigestSha256,
    string? NestedRecordingEvidenceDigestSha256,
    ReadOnlyMemory<byte>? ScoreSummaryCanonicalUtf8 = null,
    string? ScoreSummaryDigestSha256 = null,
    ReadOnlyMemory<byte>? NormalizedProposalEvidenceCanonicalUtf8 = null,
    string? NormalizedProposalEvidenceDigestSha256 = null);

/// <summary>Pure canonical writer/validator for the fixed raw-free recording artifact.</summary>
public static class OllamaRecordingExecutionArtifactModule
{
    public const string SchemaVersion = "snow_globe_ollama_recording_execution_artifact/v6";
    public const string PreviousSchemaVersion = "snow_globe_ollama_recording_execution_artifact/v5";
    public const string LegacySchemaVersion = "snow_globe_ollama_recording_execution_artifact/v4";
    public const string Semantics = "raw_free_local_loopback_recording_execution_binding_only";
    public const string RelativeArtifactPath = "artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v6.json";
    public const string PreviousRelativeArtifactPath = "artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v5.json";
    public const string LegacyRelativeArtifactPath = "artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v4.json";
    public const int MaximumArtifactBytes = 128 * 1024;
    public const int MaximumJsonDepth = 8;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] OuterNames =
    [
        "schema_version", "semantics", "artifact_status", "relative_artifact_path",
        "repository_root_digest_sha256",
        "registered_cell_digest_sha256", "profile_digest_sha256", "adapter_identity",
        "adapter_contract_digest_sha256", "codec_contract_digest_sha256", "transport_contract_digest_sha256",
        "prompt_publication_digest_sha256", "prompt_set_digest_sha256", "provenance_digest_sha256",
        "plan_digest_sha256", "runtime_binding_digest_sha256", "runtime_process_id",
        "runtime_process_start_utc_ticks", "endpoint_owner_process_id", "runtime_executable_path_digest_sha256",
        "runtime_executable_sha256", "endpoint_identity", "authorization_nonce_digest_sha256",
        "result", "receipt", "score_summary", "normalized_proposal_evidence", "claim_limitation_codes", "artifact_payload_digest_sha256"
    ];

    private static readonly string[] HistoricalOuterNames =
    [
        "schema_version", "semantics", "artifact_status", "relative_artifact_path",
        "repository_root_digest_sha256",
        "registered_cell_digest_sha256", "profile_digest_sha256", "adapter_identity",
        "adapter_contract_digest_sha256", "codec_contract_digest_sha256", "transport_contract_digest_sha256",
        "prompt_publication_digest_sha256", "prompt_set_digest_sha256", "provenance_digest_sha256",
        "plan_digest_sha256", "runtime_binding_digest_sha256", "runtime_process_id",
        "runtime_process_start_utc_ticks", "endpoint_owner_process_id", "runtime_executable_path_digest_sha256",
        "runtime_executable_sha256", "endpoint_identity", "authorization_nonce_digest_sha256",
        "result", "receipt", "score_summary", "claim_limitation_codes", "artifact_payload_digest_sha256"
    ];

    private static readonly string[] ResultNames =
    [
        "composition_outcome_code", "composition_failure_code", "recording_result_present",
        "repository_root_digest_sha256", "recording_outcome_code", "recording_failure_code",
        "completed_slot_count", "terminal_slot_ordinal",
        "terminal_submission_state", "terminal_charge_state", "terminal_status_code",
        "terminal_checkpoint_code", "terminal_policy_code",
        "additional_attempt_authorized", "automatic_retry_count", "fallback_count",
        "alternate_endpoint_or_model_count", "receipt_present", "receipt_digest_sha256",
        "nested_recording_evidence_digest_sha256", "score_summary_digest_sha256", "normalized_proposal_evidence_digest_sha256"
    ];

    private static readonly string[] HistoricalResultNames =
    [
        "composition_outcome_code", "composition_failure_code", "recording_result_present",
        "repository_root_digest_sha256", "recording_outcome_code", "recording_failure_code",
        "completed_slot_count", "terminal_slot_ordinal",
        "terminal_submission_state", "terminal_charge_state", "terminal_status_code",
        "terminal_checkpoint_code", "terminal_policy_code",
        "additional_attempt_authorized", "automatic_retry_count", "fallback_count",
        "alternate_endpoint_or_model_count", "receipt_present", "receipt_digest_sha256",
        "nested_recording_evidence_digest_sha256", "score_summary_digest_sha256"
    ];

    private static readonly string[] ReceiptNames =
    [
        "schema_version", "status", "outcome", "failure_code", "terminal_checkpoint_code", "terminal_policy_code", "registered_cell_digest_sha256",
        "profile_digest_sha256", "adapter_identity", "adapter_contract_digest_sha256",
        "codec_contract_digest_sha256", "transport_contract_digest_sha256", "runtime_process_id",
        "runtime_process_start_utc_ticks", "runtime_executable_path_digest_sha256", "runtime_executable_sha256",
        "endpoint_identity", "endpoint_owner_process_id", "prompt_publication_digest_sha256",
        "prompt_set_digest_sha256", "provenance_digest_sha256", "capability_digest_sha256",
        "runtime_binding_digest_sha256", "slots", "completed_slot_count", "terminal_slot_ordinal",
        "automatic_retry_count", "fallback_count", "alternate_endpoint_or_model_count",
        "nested_recording_evidence_digest_sha256", "score_summary_digest_sha256", "claim_limitation_codes", "receipt_payload_digest_sha256"
    ];

    private static readonly string[] ReceiptSlotNames =
    [
        "slot_ordinal", "request_digest_sha256", "wrapper_digest_sha256", "status_code",
        "submission_state", "charge_state", "additional_attempt_authorized"
    ];

    private static readonly string[] Claims =
    [
        "process_local_observation_and_nonce_only",
        "repository_root_digest_only",
        "returned_model_field_only",
        "httpclient_exposed_framing_only_no_raw_wire_proof",
        "no_independent_artifact_loaded_proof",
        "no_independent_model_execution_proof",
        "no_live_compatibility_proof",
        "digests_provide_integrity_not_authenticity",
        "raw_free_score_summary_embedded_and_revalidated_only_after_complete_evidence",
        "normalized_proposals_embedded_and_revalidated_only_after_complete_evidence",
        "bounded_offline_corpus_score_not_general_quality_or_intelligence",
        "no_cost_latency_price_winner_or_commercial_claim",
        "no_world_or_simulation_authority",
        "no_retry_fallback_or_alternate_authority",
        "model_execution_and_file_publication_are_not_transactional"
    ];

    private static readonly string[] HistoricalClaims = Claims.Where(static claim =>
        !string.Equals(claim, "normalized_proposals_embedded_and_revalidated_only_after_complete_evidence", StringComparison.Ordinal)).ToArray();

    public static OllamaRecordingExecutionArtifact Validate(ReadOnlyMemory<byte> canonicalUtf8) => Validate(canonicalUtf8, null);

    internal static OllamaRecordingExecutionArtifact Validate(ReadOnlyMemory<byte> canonicalUtf8, string? expectedRepositoryRootDigestSha256)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumArtifactBytes)
            throw Failure("artifact_size_invalid");
        ReadOnlySpan<byte> utf8Bom = [0xef, 0xbb, 0xbf];
        if (canonicalUtf8.Span.StartsWith(utf8Bom))
            throw Failure("artifact_utf8_bom_rejected");
        try { _ = StrictUtf8.GetString(canonicalUtf8.Span); }
        catch (DecoderFallbackException) { throw Failure("artifact_utf8_invalid"); }

        JsonDocument document;
        try
        {
            Utf8JsonReader reader = new(canonicalUtf8.Span, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth
            });
            document = JsonDocument.ParseValue(ref reader);
            if (reader.Read()) { document.Dispose(); throw Failure("artifact_trailing_content_rejected"); }
        }
        catch (OllamaRecordingExecutionArtifactException) { throw; }
        catch (JsonException) { throw Failure("artifact_json_invalid"); }

        using (document)
        {
            JsonElement root = document.RootElement;
            RequireCanonicalScalarEncoding(root);
            string schemaVersion = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("schema_version", out JsonElement schemaVersionValue)
                && schemaVersionValue.ValueKind == JsonValueKind.String
                ? schemaVersionValue.GetString() ?? string.Empty
                : string.Empty;
            (string relativeArtifactPath, string planSchemaVersion) = schemaVersion switch
            {
                SchemaVersion => (RelativeArtifactPath, SnowGlobeOllamaRecordingCompositionModule.PlanSchemaVersion),
                PreviousSchemaVersion => (PreviousRelativeArtifactPath, SnowGlobeOllamaRecordingCompositionModule.PreviousPlanSchemaVersion),
                LegacySchemaVersion => (LegacyRelativeArtifactPath, SnowGlobeOllamaRecordingCompositionModule.LegacyPlanSchemaVersion),
                _ => throw Failure("artifact_value_invalid")
            };
            bool retainsNormalizedProposals = string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal);
            RequireObjectAndOrder(root, retainsNormalizedProposals ? OuterNames : HistoricalOuterNames, "artifact_shape_invalid");
            RequireString(root, "schema_version", schemaVersion);
            RequireString(root, "semantics", Semantics);
            RequireString(root, "artifact_status", "structurally_complete");
            RequireString(root, "relative_artifact_path", relativeArtifactPath);
            RequireDigest(root, "repository_root_digest_sha256");
            string repositoryRootDigest = root.GetProperty("repository_root_digest_sha256").GetString()!;
            if (expectedRepositoryRootDigestSha256 is not null
                && !string.Equals(repositoryRootDigest, expectedRepositoryRootDigestSha256, StringComparison.Ordinal))
                throw Failure("artifact_repository_root_binding_invalid");
            RequireString(root, "registered_cell_digest_sha256", SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256);
            RequireString(root, "profile_digest_sha256", SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256);
            RequireString(root, "adapter_identity", SnowGlobePinnedOllamaRecordingModule.AdapterIdentity);
            RequireString(root, "adapter_contract_digest_sha256", SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256);
            RequireString(root, "codec_contract_digest_sha256", SnowGlobePinnedOllamaRecordingModule.CodecContractDigestSha256);
            RequireString(root, "transport_contract_digest_sha256", OllamaLoopbackRecordingTransportAdapter.ContractDigestSha256);
            foreach (string digestName in new[] { "prompt_publication_digest_sha256", "prompt_set_digest_sha256", "provenance_digest_sha256", "plan_digest_sha256", "runtime_binding_digest_sha256", "runtime_executable_path_digest_sha256", "runtime_executable_sha256", "authorization_nonce_digest_sha256" })
                RequireDigest(root, digestName);
            CognitionQualityPromptEnvelopePublication expectedPublication = CognitionQualityPromptEnvelopeBuilderModule.Create(SnowGlobeOllamaRecordingCompositionModule.PromptRevision);
            CognitionQualityExecutionProvenance expectedProvenance = CognitionQualityExecutionProvenance.ForLocal(
                SnowGlobePinnedOllamaRecordingModule.NormalizedModelIdentity,
                "sha256-" + SnowGlobePinnedOllamaRecordingModule.ArtifactDigestSha256,
                SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256,
                expectedPublication.PromptRevision,
                CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion,
                SnowGlobePinnedOllamaRecordingModule.AdapterIdentity);
            RequireString(root, "prompt_publication_digest_sha256", expectedPublication.CanonicalDigestSha256);
            RequireString(root, "prompt_set_digest_sha256", expectedPublication.PromptSetDigestSha256);
            RequireString(root, "provenance_digest_sha256", expectedProvenance.ProvenanceDigestSha256);
            if (!string.Equals(root.GetProperty("runtime_executable_sha256").GetString(), SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256, StringComparison.Ordinal)) throw Failure("artifact_binding_invalid");
            RequireString(root, "endpoint_identity", SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity);
            int processId = RequirePositiveInt32(root, "runtime_process_id");
            long startTicks = RequirePositiveInt64(root, "runtime_process_start_utc_ticks");
            if (startTicks > DateTime.MaxValue.Ticks || RequirePositiveInt32(root, "endpoint_owner_process_id") != processId) throw Failure("artifact_runtime_binding_invalid");
            OllamaLoopbackRuntimeBinding expectedBinding = new(processId, startTicks, SnowGlobePinnedOllamaRecordingModule.RuntimeExecutablePath, SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256, SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity, processId);
            RequireString(root, "runtime_executable_path_digest_sha256", CognitionQualityRecordingSessionCanonical.Digest(expectedBinding.CanonicalExecutablePath));
            RequireString(root, "runtime_binding_digest_sha256", SnowGlobePinnedOllamaRecordingModule.DigestRuntimeBinding(expectedBinding));
            RequireString(root, "plan_digest_sha256", SnowGlobeOllamaRecordingCompositionModule.ComputePlanDigest(
                expectedPublication, expectedProvenance, expectedBinding, repositoryRootDigest,
                root.GetProperty("authorization_nonce_digest_sha256").GetString()!, planSchemaVersion, relativeArtifactPath));

            JsonElement result = root.GetProperty("result");
            RequireObjectAndOrder(result, retainsNormalizedProposals ? ResultNames : HistoricalResultNames, "artifact_result_shape_invalid");
            string compositionOutcome = RequireEnum<OllamaRecordingCompositionOutcomeCode>(result, "composition_outcome_code");
            string compositionFailure = RequireEnum<OllamaRecordingCompositionFailureCode>(result, "composition_failure_code");
            bool recordingResultPresent = RequireBoolean(result, "recording_result_present");
            RequireString(result, "repository_root_digest_sha256", repositoryRootDigest);
            string? recordingOutcome = RequireNullableEnum<SnowGlobeOllamaLoopbackRecordingOutcomeCode>(result, "recording_outcome_code");
            string? recordingFailure = RequireNullableEnum<SnowGlobeOllamaLoopbackRecordingFailureCode>(result, "recording_failure_code");
            int? completed = RequireNullableInt32(result, "completed_slot_count");
            int? terminalSlot = RequireNullableInt32(result, "terminal_slot_ordinal");
            string? submission = RequireNullableEnum<SubmissionState>(result, "terminal_submission_state");
            string? charge = RequireNullableEnum<ChargeState>(result, "terminal_charge_state");
            int? statusCode = RequireNullableInt32(result, "terminal_status_code");
            string checkpoint = RequireEnum<OllamaRecordingTerminalCheckpointCode>(result, "terminal_checkpoint_code");
            string policy = RequireEnum<OllamaRecordingTerminalPolicyCode>(result, "terminal_policy_code");
            RequireBoolean(result, "additional_attempt_authorized", false);
            RequireInt32(result, "automatic_retry_count", 0);
            RequireInt32(result, "fallback_count", 0);
            RequireInt32(result, "alternate_endpoint_or_model_count", 0);
            bool receiptPresent = RequireBoolean(result, "receipt_present");
            string? receiptDigest = RequireNullableDigest(result, "receipt_digest_sha256");
            string? nestedDigest = RequireNullableDigest(result, "nested_recording_evidence_digest_sha256");
            string? scoreSummaryDigest = RequireNullableDigest(result, "score_summary_digest_sha256");
            string? normalizedProposalEvidenceDigest = retainsNormalizedProposals
                ? RequireNullableDigest(result, "normalized_proposal_evidence_digest_sha256")
                : null;
            if (receiptPresent != (receiptDigest is not null)) throw Failure("artifact_receipt_nullability_invalid");
            if (recordingResultPresent ? charge != ChargeState.NotApplicable.ToString() : charge is not null)
                throw Failure("artifact_result_coherence_invalid");

            JsonElement receipt = root.GetProperty("receipt");
            ReceiptTerminalFacts receiptFacts = default;
            if (receiptPresent)
            {
                if (recordingOutcome is null || recordingFailure is null || completed is null || submission is null)
                    throw Failure("artifact_result_coherence_invalid");
                if (receipt.ValueKind != JsonValueKind.Object) throw Failure("artifact_receipt_shape_invalid");
                byte[] receiptBytes = Canonicalize(receipt);
                try
                {
                    if (!string.Equals(CognitionQualityHash.Sha256(receiptBytes), receiptDigest, StringComparison.Ordinal)) throw Failure("artifact_receipt_digest_invalid");
                    receiptFacts = ValidateReceipt(receipt, recordingOutcome, recordingFailure, completed.Value, terminalSlot, submission, statusCode, checkpoint, policy, processId, startTicks,
                        root.GetProperty("runtime_executable_path_digest_sha256").GetString()!,
                        root.GetProperty("prompt_publication_digest_sha256").GetString()!,
                        root.GetProperty("prompt_set_digest_sha256").GetString()!,
                        root.GetProperty("provenance_digest_sha256").GetString()!,
                        root.GetProperty("runtime_binding_digest_sha256").GetString()!, nestedDigest, scoreSummaryDigest);
                }
                finally { CryptographicOperations.ZeroMemory(receiptBytes); }
            }
            else if (receipt.ValueKind != JsonValueKind.Null) throw Failure("artifact_receipt_nullability_invalid");

            JsonElement scoreSummaryValue = root.GetProperty("score_summary");
            CognitionQualityScoreSummary? scoreSummary = null;
            if (scoreSummaryDigest is null)
            {
                if (scoreSummaryValue.ValueKind != JsonValueKind.Null) throw Failure("artifact_score_summary_nullability_invalid");
            }
            else
            {
                if (scoreSummaryValue.ValueKind != JsonValueKind.Object) throw Failure("artifact_score_summary_shape_invalid");
                byte[] scoreSummaryBytes = Canonicalize(scoreSummaryValue);
                try
                {
                    scoreSummary = CognitionQualityScoreSummaryCodec.Validate(scoreSummaryBytes);
                    if (!string.Equals(scoreSummary.CanonicalDigestSha256, scoreSummaryDigest, StringComparison.Ordinal)
                        || !string.Equals(scoreSummary.RecordingEvidenceDigestSha256, nestedDigest, StringComparison.Ordinal)
                        || !string.Equals(scoreSummary.ProvenanceDigestSha256, root.GetProperty("provenance_digest_sha256").GetString(), StringComparison.Ordinal))
                        throw Failure("artifact_score_summary_binding_invalid");
                }
                catch (CognitionQualityScoreSummaryException) { throw Failure("artifact_score_summary_invalid"); }
                finally { CryptographicOperations.ZeroMemory(scoreSummaryBytes); }
            }

            CognitionQualityNormalizedProposalEvidence? normalizedProposalEvidence = null;
            if (retainsNormalizedProposals)
            {
                JsonElement normalizedValue = root.GetProperty("normalized_proposal_evidence");
                if (normalizedProposalEvidenceDigest is null)
                {
                    if (normalizedValue.ValueKind != JsonValueKind.Null) throw Failure("artifact_normalized_proposals_nullability_invalid");
                }
                else
                {
                    if (normalizedValue.ValueKind != JsonValueKind.Object) throw Failure("artifact_normalized_proposals_shape_invalid");
                    byte[] normalizedBytes = Canonicalize(normalizedValue);
                    try
                    {
                        normalizedProposalEvidence = CognitionQualityNormalizedProposalEvidenceCodec.Validate(normalizedBytes);
                        if (!string.Equals(normalizedProposalEvidence.CanonicalDigestSha256, normalizedProposalEvidenceDigest, StringComparison.Ordinal)
                            || !string.Equals(normalizedProposalEvidence.SourceEvidenceSchemaVersion, CognitionQualityRecordingEvidenceModule.SchemaVersion, StringComparison.Ordinal)
                            || !string.Equals(normalizedProposalEvidence.SourceEvidenceDigestSha256, nestedDigest, StringComparison.Ordinal))
                            throw Failure("artifact_normalized_proposals_binding_invalid");
                    }
                    catch (CognitionQualityNormalizedProposalEvidenceException) { throw Failure("artifact_normalized_proposals_invalid"); }
                    finally { CryptographicOperations.ZeroMemory(normalizedBytes); }
                }
            }

            if (!OllamaRecordingTerminalCoherenceModule.TryParseAndValidate(
                compositionOutcome, compositionFailure, recordingResultPresent, recordingOutcome, recordingFailure,
                completed, terminalSlot, submission, statusCode, receiptPresent, receiptFacts.RowPresent,
                receiptFacts.WrapperPresent, nestedDigest is not null, checkpoint, policy))
                throw Failure("artifact_result_coherence_invalid");
            bool complete = compositionOutcome == OllamaRecordingCompositionOutcomeCode.Complete.ToString()
                && compositionFailure == OllamaRecordingCompositionFailureCode.None.ToString()
                && recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete.ToString()
                && recordingFailure == SnowGlobeOllamaLoopbackRecordingFailureCode.None.ToString();
            if (complete != (scoreSummary is not null) || complete != (scoreSummaryDigest is not null))
                throw Failure("artifact_score_summary_terminal_invalid");
            if (retainsNormalizedProposals
                && (complete != (normalizedProposalEvidence is not null) || complete != (normalizedProposalEvidenceDigest is not null)))
                throw Failure("artifact_normalized_proposals_terminal_invalid");

            RequireExactStringArray(root.GetProperty("claim_limitation_codes"), retainsNormalizedProposals ? Claims : HistoricalClaims, "artifact_claims_invalid");
            JsonElement payloadDigestValue = root.GetProperty("artifact_payload_digest_sha256");
            if (payloadDigestValue.ValueKind != JsonValueKind.String) throw Failure("artifact_payload_digest_invalid");
            string payloadDigest = payloadDigestValue.GetString() ?? string.Empty;
            if (!IsDigest(payloadDigest)) throw Failure("artifact_payload_digest_invalid");
            byte[] payload = CanonicalizeWithoutLast(root, "artifact_payload_digest_sha256");
            try { if (!string.Equals(CognitionQualityHash.Sha256(payload), payloadDigest, StringComparison.Ordinal)) throw Failure("artifact_payload_digest_invalid"); }
            finally { CryptographicOperations.ZeroMemory(payload); }
            byte[] canonical = Canonicalize(root);
            try { if (!canonical.AsSpan().SequenceEqual(canonicalUtf8.Span)) throw Failure("artifact_noncanonical"); }
            catch { CryptographicOperations.ZeroMemory(canonical); throw; }
            return new OllamaRecordingExecutionArtifact(canonical, payloadDigest, schemaVersion, relativeArtifactPath, repositoryRootDigest,
                compositionOutcome, compositionFailure, recordingResultPresent, recordingOutcome, recordingFailure,
                completed, terminalSlot, submission, charge, statusCode, checkpoint, policy, receiptPresent,
                receiptFacts.RowPresent, receiptFacts.WrapperPresent, receiptDigest, nestedDigest, scoreSummary, normalizedProposalEvidence);
        }
    }

    internal static OllamaRecordingExecutionArtifact Create(OllamaRecordingCompositionPlan plan, OllamaRecordingArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] payload = Write(plan, snapshot, null);
        string digest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(plan, snapshot, digest);
        if (canonical.Length > MaximumArtifactBytes) { CryptographicOperations.ZeroMemory(canonical); throw Failure("artifact_size_invalid"); }
        try { return Validate(canonical, plan.RepositoryRootDigestSha256); }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }

    private static byte[] Write(OllamaRecordingCompositionPlan plan, OllamaRecordingArtifactSnapshot snapshot, string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false });
        writer.WriteStartObject();
        writer.WriteString("schema_version", SchemaVersion);
        writer.WriteString("semantics", Semantics);
        writer.WriteString("artifact_status", "structurally_complete");
        writer.WriteString("relative_artifact_path", RelativeArtifactPath);
        writer.WriteString("repository_root_digest_sha256", plan.RepositoryRootDigestSha256);
        writer.WriteString("registered_cell_digest_sha256", plan.RegisteredCellDigestSha256);
        writer.WriteString("profile_digest_sha256", plan.ProfileDigestSha256);
        writer.WriteString("adapter_identity", plan.AdapterIdentity);
        writer.WriteString("adapter_contract_digest_sha256", plan.AdapterContractDigestSha256);
        writer.WriteString("codec_contract_digest_sha256", plan.CodecContractDigestSha256);
        writer.WriteString("transport_contract_digest_sha256", plan.TransportContractDigestSha256);
        writer.WriteString("prompt_publication_digest_sha256", plan.PromptPublicationDigestSha256);
        writer.WriteString("prompt_set_digest_sha256", plan.PromptSetDigestSha256);
        writer.WriteString("provenance_digest_sha256", plan.ProvenanceDigestSha256);
        writer.WriteString("plan_digest_sha256", plan.PlanDigestSha256);
        writer.WriteString("runtime_binding_digest_sha256", plan.RuntimeBindingDigestSha256);
        writer.WriteNumber("runtime_process_id", plan.RuntimeProcessId);
        writer.WriteNumber("runtime_process_start_utc_ticks", plan.RuntimeProcessStartUtcTicks);
        writer.WriteNumber("endpoint_owner_process_id", plan.EndpointOwnerProcessId);
        writer.WriteString("runtime_executable_path_digest_sha256", plan.RuntimeExecutablePathDigestSha256);
        writer.WriteString("runtime_executable_sha256", plan.RuntimeExecutableSha256);
        writer.WriteString("endpoint_identity", SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity);
        writer.WriteString("authorization_nonce_digest_sha256", plan.AuthorizationNonceDigestSha256);
        writer.WritePropertyName("result"); writer.WriteStartObject();
        writer.WriteString("composition_outcome_code", snapshot.CompositionOutcomeCode);
        writer.WriteString("composition_failure_code", snapshot.CompositionFailureCode);
        writer.WriteBoolean("recording_result_present", snapshot.RecordingResultPresent);
        writer.WriteString("repository_root_digest_sha256", plan.RepositoryRootDigestSha256);
        WriteNullableString(writer, "recording_outcome_code", snapshot.RecordingOutcomeCode);
        WriteNullableString(writer, "recording_failure_code", snapshot.RecordingFailureCode);
        WriteNullableNumber(writer, "completed_slot_count", snapshot.CompletedSlotCount);
        WriteNullableNumber(writer, "terminal_slot_ordinal", snapshot.TerminalSlotOrdinal);
        WriteNullableString(writer, "terminal_submission_state", snapshot.TerminalSubmissionState);
        WriteNullableString(writer, "terminal_charge_state", snapshot.TerminalChargeState);
        WriteNullableNumber(writer, "terminal_status_code", snapshot.TerminalStatusCode);
        WriteNullableString(writer, "terminal_checkpoint_code", snapshot.TerminalCheckpointCode);
        WriteNullableString(writer, "terminal_policy_code", snapshot.TerminalPolicyCode);
        writer.WriteBoolean("additional_attempt_authorized", false);
        writer.WriteNumber("automatic_retry_count", 0);
        writer.WriteNumber("fallback_count", 0);
        writer.WriteNumber("alternate_endpoint_or_model_count", 0);
        bool receiptPresent = snapshot.ReceiptCanonicalUtf8.HasValue;
        writer.WriteBoolean("receipt_present", receiptPresent);
        WriteNullableString(writer, "receipt_digest_sha256", snapshot.ReceiptDigestSha256);
        WriteNullableString(writer, "nested_recording_evidence_digest_sha256", snapshot.NestedRecordingEvidenceDigestSha256);
        WriteNullableString(writer, "score_summary_digest_sha256", snapshot.ScoreSummaryDigestSha256);
        WriteNullableString(writer, "normalized_proposal_evidence_digest_sha256", snapshot.NormalizedProposalEvidenceDigestSha256);
        writer.WriteEndObject();
        writer.WritePropertyName("receipt");
        if (snapshot.ReceiptCanonicalUtf8 is { } receipt) writer.WriteRawValue(receipt.Span, skipInputValidation: false); else writer.WriteNullValue();
        writer.WritePropertyName("score_summary");
        if (snapshot.ScoreSummaryCanonicalUtf8 is { } summary) writer.WriteRawValue(summary.Span, skipInputValidation: false); else writer.WriteNullValue();
        writer.WritePropertyName("normalized_proposal_evidence");
        if (snapshot.NormalizedProposalEvidenceCanonicalUtf8 is { } normalized) writer.WriteRawValue(normalized.Span, skipInputValidation: false); else writer.WriteNullValue();
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray(); foreach (string claim in Claims) writer.WriteStringValue(claim); writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("artifact_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private readonly record struct ReceiptTerminalFacts(bool RowPresent, bool WrapperPresent);

    private static ReceiptTerminalFacts ValidateReceipt(JsonElement receipt, string outcome, string failure, int completed, int? terminalSlot, string terminalSubmission, int? terminalStatus, string checkpoint, string policy, int processId, long startTicks, string pathDigest, string publicationDigest, string promptSetDigest, string provenanceDigest, string runtimeBindingDigest, string? nestedDigest, string? scoreSummaryDigest)
    {
        RequireObjectAndOrder(receipt, ReceiptNames, "artifact_receipt_shape_invalid");
        RequireString(receipt, "schema_version", SnowGlobePinnedOllamaRecordingModule.ReceiptSchemaVersion);
        RequireString(receipt, "status", outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete.ToString() ? "complete" : "terminal");
        RequireString(receipt, "outcome", outcome);
        RequireString(receipt, "failure_code", failure);
        RequireString(receipt, "terminal_checkpoint_code", checkpoint);
        RequireString(receipt, "terminal_policy_code", policy);
        RequireString(receipt, "registered_cell_digest_sha256", SnowGlobePinnedOllamaRecordingModule.RegisteredCellDigestSha256);
        RequireString(receipt, "profile_digest_sha256", SnowGlobePinnedOllamaRecordingModule.ProfileDigestSha256);
        RequireString(receipt, "adapter_identity", SnowGlobePinnedOllamaRecordingModule.AdapterIdentity);
        RequireString(receipt, "adapter_contract_digest_sha256", SnowGlobePinnedOllamaRecordingModule.AdapterContractDigestSha256);
        RequireString(receipt, "codec_contract_digest_sha256", SnowGlobePinnedOllamaRecordingModule.CodecContractDigestSha256);
        RequireString(receipt, "transport_contract_digest_sha256", OllamaLoopbackRecordingTransportAdapter.ContractDigestSha256);
        RequireInt32(receipt, "runtime_process_id", processId);
        RequireInt64(receipt, "runtime_process_start_utc_ticks", startTicks);
        RequireString(receipt, "runtime_executable_path_digest_sha256", pathDigest);
        RequireString(receipt, "runtime_executable_sha256", SnowGlobePinnedOllamaRecordingModule.RuntimeExecutableSha256);
        RequireString(receipt, "endpoint_identity", SnowGlobePinnedOllamaRecordingModule.CanonicalEndpointIdentity);
        RequireInt32(receipt, "endpoint_owner_process_id", processId);
        RequireString(receipt, "prompt_publication_digest_sha256", publicationDigest);
        RequireString(receipt, "prompt_set_digest_sha256", promptSetDigest);
        RequireString(receipt, "provenance_digest_sha256", provenanceDigest);
        RequireDigest(receipt, "capability_digest_sha256");
        RequireString(receipt, "runtime_binding_digest_sha256", runtimeBindingDigest);
        JsonElement slots = receipt.GetProperty("slots");
        if (slots.ValueKind != JsonValueKind.Array || slots.GetArrayLength() > CognitionQualityCorpusV1.ScenarioCount) throw Failure("artifact_receipt_slots_invalid");
        JsonElement[] slotValues = slots.EnumerateArray().ToArray();
        int expectedOrdinal = 1;
        string? terminalWrapper = null;
        bool terminalRowPresent = false;
        for (int index = 0; index < slotValues.Length; index++)
        {
            JsonElement slot = slotValues[index];
            RequireObjectAndOrder(slot, ReceiptSlotNames, "artifact_receipt_slot_shape_invalid");
            RequireInt32(slot, "slot_ordinal", expectedOrdinal++);
            RequireDigest(slot, "request_digest_sha256");
            string? wrapper = RequireNullableDigest(slot, "wrapper_digest_sha256");
            int? slotStatus = RequireNullableInt32(slot, "status_code");
            if (slotStatus is < 100 or > 599) throw Failure("artifact_receipt_slot_status_invalid");
            string slotSubmission = RequireEnum<SubmissionState>(slot, "submission_state");
            RequireString(slot, "charge_state", ChargeState.NotApplicable.ToString());
            RequireBoolean(slot, "additional_attempt_authorized", false);

            bool isCompletedSlot = index < completed;
            if (isCompletedSlot)
            {
                if (slotSubmission != SubmissionState.ResponseReceived.ToString() || slotStatus != 200 || wrapper is null)
                    throw Failure("artifact_receipt_completed_slot_invalid");
            }
            else
            {
                terminalRowPresent = true;
                terminalWrapper = wrapper;
                if (index != completed || terminalSlot != completed + 1 || slotValues.Length != completed + 1)
                    throw Failure("artifact_receipt_terminal_slot_invalid");
                if (slotSubmission != terminalSubmission || slotStatus != terminalStatus)
                    throw Failure("artifact_receipt_result_binding_invalid");
                if (slotSubmission != SubmissionState.ResponseReceived.ToString()
                    && (wrapper is not null || slotStatus is not null))
                    throw Failure("artifact_receipt_terminal_slot_invalid");
            }
        }
        RequireInt32(receipt, "completed_slot_count", completed);
        if (RequireNullableInt32(receipt, "terminal_slot_ordinal") != terminalSlot) throw Failure("artifact_receipt_binding_invalid");
        if (outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete.ToString())
        {
            if (failure != SnowGlobeOllamaLoopbackRecordingFailureCode.None.ToString()
                || slotValues.Length != CognitionQualityCorpusV1.ScenarioCount
                || completed != CognitionQualityCorpusV1.ScenarioCount || terminalSlot is not null
                || terminalSubmission != SubmissionState.ResponseReceived.ToString() || terminalStatus != 200)
                throw Failure("artifact_receipt_result_binding_invalid");
        }
        else if (outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed.ToString()
            && failure == SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected.ToString())
        {
            if (slotValues.Length != CognitionQualityCorpusV1.ScenarioCount
                || completed != CognitionQualityCorpusV1.ScenarioCount
                || terminalSlot != CognitionQualityCorpusV1.ScenarioCount
                || terminalSubmission != SubmissionState.ResponseReceived.ToString() || terminalStatus != 200)
                throw Failure("artifact_receipt_result_binding_invalid");
        }
        else
        {
            if (completed >= CognitionQualityCorpusV1.ScenarioCount || terminalSlot != completed + 1
                || (slotValues.Length != completed && slotValues.Length != completed + 1))
                throw Failure("artifact_receipt_result_binding_invalid");
            if (slotValues.Length == completed
                && (terminalSubmission != SubmissionState.DefinitelyNotSubmitted.ToString() || terminalStatus is not null))
                throw Failure("artifact_receipt_result_binding_invalid");
        }
        RequireInt32(receipt, "automatic_retry_count", 0); RequireInt32(receipt, "fallback_count", 0); RequireInt32(receipt, "alternate_endpoint_or_model_count", 0);
        if (!string.Equals(RequireNullableDigest(receipt, "nested_recording_evidence_digest_sha256"), nestedDigest, StringComparison.Ordinal)) throw Failure("artifact_receipt_binding_invalid");
        if (!string.Equals(RequireNullableDigest(receipt, "score_summary_digest_sha256"), scoreSummaryDigest, StringComparison.Ordinal)) throw Failure("artifact_receipt_binding_invalid");
        RequireExactStringArray(receipt.GetProperty("claim_limitation_codes"), SnowGlobePinnedOllamaRecordingModule.ClaimLimitations, "artifact_receipt_claims_invalid");
        JsonElement receiptPayloadValue = receipt.GetProperty("receipt_payload_digest_sha256");
        if (receiptPayloadValue.ValueKind != JsonValueKind.String) throw Failure("artifact_receipt_payload_digest_invalid");
        string receiptPayload = receiptPayloadValue.GetString() ?? string.Empty;
        if (!IsDigest(receiptPayload)) throw Failure("artifact_receipt_payload_digest_invalid");
        byte[] payload = CanonicalizeWithoutLast(receipt, ReceiptNames[^1]);
        try { if (!string.Equals(CognitionQualityHash.Sha256(payload), receiptPayload, StringComparison.Ordinal)) throw Failure("artifact_receipt_payload_digest_invalid"); }
        finally { CryptographicOperations.ZeroMemory(payload); }
        return new ReceiptTerminalFacts(terminalRowPresent, terminalWrapper is not null);
    }

    private static void RequireObjectAndOrder(JsonElement value, IReadOnlyList<string> names, string code)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Failure(code);
        JsonProperty[] properties = value.EnumerateObject().ToArray();
        if (properties.Length != names.Count) throw Failure(code);
        for (int index = 0; index < names.Count; index++) if (!string.Equals(properties[index].Name, names[index], StringComparison.Ordinal)) throw Failure(code);
    }

    private static string RequireString(JsonElement owner, string name, string expected)
    {
        JsonElement value = owner.GetProperty(name); if (value.ValueKind != JsonValueKind.String || !string.Equals(value.GetString(), expected, StringComparison.Ordinal)) throw Failure("artifact_value_invalid"); return expected;
    }
    private static void RequireDigest(JsonElement owner, string name) { JsonElement value = owner.GetProperty(name); if (value.ValueKind != JsonValueKind.String || !IsDigest(value.GetString())) throw Failure("artifact_digest_invalid"); }
    private static string? RequireNullableDigest(JsonElement owner, string name) { JsonElement value = owner.GetProperty(name); if (value.ValueKind == JsonValueKind.Null) return null; if (value.ValueKind != JsonValueKind.String || !IsDigest(value.GetString())) throw Failure("artifact_digest_invalid"); return value.GetString(); }
    private static int RequirePositiveInt32(JsonElement owner, string name) { int value = RequireInt32(owner, name); if (value <= 0) throw Failure("artifact_number_invalid"); return value; }
    private static long RequirePositiveInt64(JsonElement owner, string name) { long value = RequireInt64(owner, name); if (value <= 0) throw Failure("artifact_number_invalid"); return value; }
    private static int RequireInt32(JsonElement owner, string name) { JsonElement element = owner.GetProperty(name); if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value)) throw Failure("artifact_number_invalid"); return value; }
    private static long RequireInt64(JsonElement owner, string name) { JsonElement element = owner.GetProperty(name); if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out long value)) throw Failure("artifact_number_invalid"); return value; }
    private static int RequireInt32(JsonElement owner, string name, int expected) { int value = RequireInt32(owner, name); if (value != expected) throw Failure("artifact_number_invalid"); return value; }
    private static long RequireInt64(JsonElement owner, string name, long expected) { long value = RequireInt64(owner, name); if (value != expected) throw Failure("artifact_number_invalid"); return value; }
    private static int? RequireNullableInt32(JsonElement owner, string name) { JsonElement value = owner.GetProperty(name); if (value.ValueKind == JsonValueKind.Null) return null; if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int parsed)) throw Failure("artifact_number_invalid"); return parsed; }
    private static bool RequireBoolean(JsonElement owner, string name) { JsonElement value = owner.GetProperty(name); if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Failure("artifact_boolean_invalid"); return value.GetBoolean(); }
    private static bool RequireBoolean(JsonElement owner, string name, bool expected) { bool value = RequireBoolean(owner, name); if (value != expected) throw Failure("artifact_boolean_invalid"); return value; }
    private static string RequireEnum<T>(JsonElement owner, string name) where T : struct, Enum
    {
        JsonElement value = owner.GetProperty(name); if (value.ValueKind != JsonValueKind.String) throw Failure("artifact_enum_invalid"); string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out T parsed) || !Enum.IsDefined(parsed)) throw Failure("artifact_enum_invalid"); return text;
    }
    private static string? RequireNullableEnum<T>(JsonElement owner, string name) where T : struct, Enum
    {
        JsonElement value = owner.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw Failure("artifact_enum_invalid");
        string text = value.GetString()!;
        if (!Enum.TryParse(text, ignoreCase: false, out T parsed) || !Enum.IsDefined(parsed)) throw Failure("artifact_enum_invalid");
        return text;
    }
    private static void RequireExactStringArray(JsonElement value, IReadOnlyList<string> expected, string code)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count) throw Failure(code); int index = 0;
        foreach (JsonElement item in value.EnumerateArray()) if (item.ValueKind != JsonValueKind.String || !string.Equals(item.GetString(), expected[index++], StringComparison.Ordinal)) throw Failure(code);
    }
    private static void RequireCanonicalScalarEncoding(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject()) RequireCanonicalScalarEncoding(property.Value);
            return;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) RequireCanonicalScalarEncoding(item);
            return;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            if (!string.Equals(JsonSerializer.Serialize(value.GetString()), value.GetRawText(), StringComparison.Ordinal)) throw Failure("artifact_noncanonical");
            return;
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt64(out long integer) || !string.Equals(integer.ToString(CultureInfo.InvariantCulture), value.GetRawText(), StringComparison.Ordinal)) throw Failure("artifact_noncanonical");
        }
    }
    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static byte[] Canonicalize(JsonElement value) { ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }); value.WriteTo(writer); writer.Flush(); return buffer.WrittenSpan.ToArray(); }
    private static byte[] CanonicalizeWithoutLast(JsonElement value, string lastName) { ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }); writer.WriteStartObject(); foreach (JsonProperty property in value.EnumerateObject()) { if (property.NameEquals(lastName)) continue; writer.WritePropertyName(property.Name); property.Value.WriteTo(writer); } writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray(); }
    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value) { if (value.HasValue) writer.WriteNumber(name, value.Value); else writer.WriteNull(name); }
    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value) { if (value is null) writer.WriteNull(name); else writer.WriteString(name, value); }
    private static OllamaRecordingExecutionArtifactException Failure(string code) => new(code);
}
