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
        bool receiptPresent,
        string? receiptDigestSha256,
        string? nestedRecordingEvidenceDigestSha256)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
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
        ReceiptPresent = receiptPresent;
        ReceiptDigestSha256 = receiptDigestSha256;
        NestedRecordingEvidenceDigestSha256 = nestedRecordingEvidenceDigestSha256;
    }

    public string SchemaVersion => OllamaRecordingExecutionArtifactModule.SchemaVersion;
    public string Semantics => OllamaRecordingExecutionArtifactModule.Semantics;
    public string RelativeArtifactPath => OllamaRecordingExecutionArtifactModule.RelativeArtifactPath;
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
    public bool ReceiptPresent { get; }
    public string? ReceiptDigestSha256 { get; }
    public string? NestedRecordingEvidenceDigestSha256 { get; }
    public bool AdditionalAttemptAuthorized => false;
    public bool HasIndependentArtifactLoadedProof => false;
    public bool HasIndependentModelExecutionProof => false;
    public bool HasLiveCompatibilityProof => false;
    public bool HasBenchmarkOrQualityClaim => false;
    public bool HasWorldOrSimulationAuthority => false;
    public bool HasRetryAuthority => false;
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
    ReadOnlyMemory<byte>? ReceiptCanonicalUtf8,
    string? ReceiptDigestSha256,
    string? NestedRecordingEvidenceDigestSha256);

/// <summary>Pure canonical writer/validator for the fixed raw-free recording artifact.</summary>
public static class OllamaRecordingExecutionArtifactModule
{
    public const string SchemaVersion = "snow_globe_ollama_recording_execution_artifact/v1";
    public const string Semantics = "raw_free_local_loopback_recording_execution_binding_only";
    public const string RelativeArtifactPath = "artifacts/snowglobe/local-model/qwen3.5-4b-recording-execution-v1.json";
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
        "result", "receipt", "claim_limitation_codes", "artifact_payload_digest_sha256"
    ];

    private static readonly string[] ResultNames =
    [
        "composition_outcome_code", "composition_failure_code", "recording_result_present",
        "repository_root_digest_sha256", "recording_outcome_code", "recording_failure_code",
        "completed_slot_count", "terminal_slot_ordinal",
        "terminal_submission_state", "terminal_charge_state", "terminal_status_code",
        "additional_attempt_authorized", "automatic_retry_count", "fallback_count",
        "alternate_endpoint_or_model_count", "receipt_present", "receipt_digest_sha256",
        "nested_recording_evidence_digest_sha256"
    ];

    private static readonly string[] ReceiptNames =
    [
        "schema_version", "status", "outcome", "failure_code", "registered_cell_digest_sha256",
        "profile_digest_sha256", "adapter_identity", "adapter_contract_digest_sha256",
        "codec_contract_digest_sha256", "transport_contract_digest_sha256", "runtime_process_id",
        "runtime_process_start_utc_ticks", "runtime_executable_path_digest_sha256", "runtime_executable_sha256",
        "endpoint_identity", "endpoint_owner_process_id", "prompt_publication_digest_sha256",
        "prompt_set_digest_sha256", "provenance_digest_sha256", "capability_digest_sha256",
        "runtime_binding_digest_sha256", "slots", "completed_slot_count", "terminal_slot_ordinal",
        "automatic_retry_count", "fallback_count", "alternate_endpoint_or_model_count",
        "nested_recording_evidence_digest_sha256", "claim_limitation_codes", "receipt_payload_digest_sha256"
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
        "no_independent_artifact_loaded_proof",
        "no_independent_model_execution_proof",
        "no_live_compatibility_proof",
        "digests_provide_integrity_not_authenticity",
        "nested_scoring_evidence_not_embedded_or_revalidated",
        "no_benchmark_quality_intelligence_winner_cost_or_commercial_claim",
        "no_world_or_simulation_authority",
        "no_retry_fallback_or_alternate_authority",
        "model_execution_and_file_publication_are_not_transactional"
    ];

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
            RequireObjectAndOrder(root, OuterNames, "artifact_shape_invalid");
            RequireString(root, "schema_version", SchemaVersion);
            RequireString(root, "semantics", Semantics);
            RequireString(root, "artifact_status", "structurally_complete");
            RequireString(root, "relative_artifact_path", RelativeArtifactPath);
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
            RequireString(root, "plan_digest_sha256", SnowGlobeOllamaRecordingCompositionModule.ComputePlanDigest(expectedPublication, expectedProvenance, expectedBinding, repositoryRootDigest, root.GetProperty("authorization_nonce_digest_sha256").GetString()!));

            JsonElement result = root.GetProperty("result");
            RequireObjectAndOrder(result, ResultNames, "artifact_result_shape_invalid");
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
            RequireBoolean(result, "additional_attempt_authorized", false);
            RequireInt32(result, "automatic_retry_count", 0);
            RequireInt32(result, "fallback_count", 0);
            RequireInt32(result, "alternate_endpoint_or_model_count", 0);
            bool receiptPresent = RequireBoolean(result, "receipt_present");
            string? receiptDigest = RequireNullableDigest(result, "receipt_digest_sha256");
            string? nestedDigest = RequireNullableDigest(result, "nested_recording_evidence_digest_sha256");
            ValidateResultCoherence(compositionOutcome, compositionFailure, recordingResultPresent, recordingOutcome,
                recordingFailure, completed, terminalSlot, submission, charge, statusCode,
                receiptPresent, receiptDigest, nestedDigest);

            JsonElement receipt = root.GetProperty("receipt");
            if (receiptPresent)
            {
                if (receipt.ValueKind != JsonValueKind.Object) throw Failure("artifact_receipt_shape_invalid");
                byte[] receiptBytes = Canonicalize(receipt);
                try
                {
                    if (!string.Equals(CognitionQualityHash.Sha256(receiptBytes), receiptDigest, StringComparison.Ordinal)) throw Failure("artifact_receipt_digest_invalid");
                    ValidateReceipt(receipt, recordingOutcome!, recordingFailure!, completed!.Value, terminalSlot, submission!, statusCode, processId, startTicks,
                        root.GetProperty("runtime_executable_path_digest_sha256").GetString()!,
                        root.GetProperty("prompt_publication_digest_sha256").GetString()!,
                        root.GetProperty("prompt_set_digest_sha256").GetString()!,
                        root.GetProperty("provenance_digest_sha256").GetString()!,
                        root.GetProperty("runtime_binding_digest_sha256").GetString()!, nestedDigest);
                }
                finally { CryptographicOperations.ZeroMemory(receiptBytes); }
            }
            else if (receipt.ValueKind != JsonValueKind.Null) throw Failure("artifact_receipt_nullability_invalid");

            RequireExactStringArray(root.GetProperty("claim_limitation_codes"), Claims, "artifact_claims_invalid");
            string payloadDigest = root.GetProperty("artifact_payload_digest_sha256").GetString() ?? string.Empty;
            if (!IsDigest(payloadDigest)) throw Failure("artifact_payload_digest_invalid");
            byte[] payload = CanonicalizeWithoutLast(root, OuterNames[^1]);
            try { if (!string.Equals(CognitionQualityHash.Sha256(payload), payloadDigest, StringComparison.Ordinal)) throw Failure("artifact_payload_digest_invalid"); }
            finally { CryptographicOperations.ZeroMemory(payload); }
            byte[] canonical = Canonicalize(root);
            try { if (!canonical.AsSpan().SequenceEqual(canonicalUtf8.Span)) throw Failure("artifact_noncanonical"); }
            catch { CryptographicOperations.ZeroMemory(canonical); throw; }
            return new OllamaRecordingExecutionArtifact(canonical, payloadDigest, repositoryRootDigest,
                compositionOutcome, compositionFailure, recordingResultPresent, recordingOutcome, recordingFailure,
                completed, terminalSlot, submission, charge, statusCode, receiptPresent, receiptDigest, nestedDigest);
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
        writer.WriteBoolean("additional_attempt_authorized", false);
        writer.WriteNumber("automatic_retry_count", 0);
        writer.WriteNumber("fallback_count", 0);
        writer.WriteNumber("alternate_endpoint_or_model_count", 0);
        bool receiptPresent = snapshot.ReceiptCanonicalUtf8.HasValue;
        writer.WriteBoolean("receipt_present", receiptPresent);
        WriteNullableString(writer, "receipt_digest_sha256", snapshot.ReceiptDigestSha256);
        WriteNullableString(writer, "nested_recording_evidence_digest_sha256", snapshot.NestedRecordingEvidenceDigestSha256);
        writer.WriteEndObject();
        writer.WritePropertyName("receipt");
        if (snapshot.ReceiptCanonicalUtf8 is { } receipt) writer.WriteRawValue(receipt.Span, skipInputValidation: false); else writer.WriteNullValue();
        writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray(); foreach (string claim in Claims) writer.WriteStringValue(claim); writer.WriteEndArray();
        if (payloadDigest is not null) writer.WriteString("artifact_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject(); writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void ValidateReceipt(JsonElement receipt, string outcome, string failure, int completed, int? terminalSlot, string terminalSubmission, int? terminalStatus, int processId, long startTicks, string pathDigest, string publicationDigest, string promptSetDigest, string provenanceDigest, string runtimeBindingDigest, string? nestedDigest)
    {
        RequireObjectAndOrder(receipt, ReceiptNames, "artifact_receipt_shape_invalid");
        RequireString(receipt, "schema_version", SnowGlobePinnedOllamaRecordingModule.ReceiptSchemaVersion);
        RequireString(receipt, "status", outcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete.ToString() ? "complete" : "terminal");
        RequireString(receipt, "outcome", outcome);
        RequireString(receipt, "failure_code", failure);
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
                if (index != completed || terminalSlot != completed + 1 || slotValues.Length != completed + 1)
                    throw Failure("artifact_receipt_terminal_slot_invalid");
                if (slotSubmission != terminalSubmission || slotStatus != terminalStatus)
                    throw Failure("artifact_receipt_result_binding_invalid");
                if (slotSubmission == SubmissionState.ResponseReceived.ToString())
                {
                    if (failure == SnowGlobeOllamaLoopbackRecordingFailureCode.WrapperRejected.ToString() && wrapper is null)
                        throw Failure("artifact_receipt_terminal_slot_invalid");
                }
                else if (wrapper is not null || slotStatus is not null)
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
        RequireExactStringArray(receipt.GetProperty("claim_limitation_codes"), SnowGlobePinnedOllamaRecordingModule.ClaimLimitations, "artifact_receipt_claims_invalid");
        string receiptPayload = receipt.GetProperty("receipt_payload_digest_sha256").GetString() ?? string.Empty;
        if (!IsDigest(receiptPayload)) throw Failure("artifact_receipt_payload_digest_invalid");
        byte[] payload = CanonicalizeWithoutLast(receipt, ReceiptNames[^1]);
        try { if (!string.Equals(CognitionQualityHash.Sha256(payload), receiptPayload, StringComparison.Ordinal)) throw Failure("artifact_receipt_payload_digest_invalid"); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static void ValidateResultCoherence(
        string compositionOutcome,
        string compositionFailure,
        bool recordingResultPresent,
        string? recordingOutcome,
        string? recordingFailure,
        int? completed,
        int? terminalSlot,
        string? submission,
        string? charge,
        int? statusCode,
        bool receiptPresent,
        string? receiptDigest,
        string? nestedDigest)
    {
        if (receiptPresent != (receiptDigest is not null)) throw Failure("artifact_receipt_nullability_invalid");
        if (!recordingResultPresent)
        {
            bool exactCompositionFailure = compositionOutcome == OllamaRecordingCompositionOutcomeCode.AuthorizationRejected.ToString()
                && compositionFailure == OllamaRecordingCompositionFailureCode.AuthorizationRejected.ToString()
                || compositionOutcome == OllamaRecordingCompositionOutcomeCode.CompositionFailed.ToString()
                && compositionFailure == OllamaRecordingCompositionFailureCode.CompositionFailed.ToString();
            if (!exactCompositionFailure || recordingOutcome is not null || recordingFailure is not null
                || completed is not null || terminalSlot is not null || submission is not null || charge is not null
                || statusCode is not null || receiptPresent || nestedDigest is not null)
                throw Failure("artifact_composition_failure_coherence_invalid");
            return;
        }

        if (recordingOutcome is null || recordingFailure is null || completed is null || submission is null
            || charge != ChargeState.NotApplicable.ToString()
            || completed is < 0 or > CognitionQualityCorpusV1.ScenarioCount
            || terminalSlot is < 1 or > CognitionQualityCorpusV1.ScenarioCount
            || statusCode is < 100 or > 599)
            throw Failure("artifact_result_coherence_invalid");

        if (recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Complete.ToString())
        {
            if (compositionOutcome != OllamaRecordingCompositionOutcomeCode.Complete.ToString()
                || compositionFailure != OllamaRecordingCompositionFailureCode.None.ToString()
                || recordingFailure != SnowGlobeOllamaLoopbackRecordingFailureCode.None.ToString()
                || completed != CognitionQualityCorpusV1.ScenarioCount || terminalSlot is not null
                || submission != SubmissionState.ResponseReceived.ToString() || statusCode != 200
                || !receiptPresent || nestedDigest is null)
                throw Failure("artifact_complete_result_invalid");
            return;
        }

        if (recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed.ToString()
            && recordingFailure == SnowGlobeOllamaLoopbackRecordingFailureCode.CapabilityExpired.ToString())
        {
            if (compositionOutcome != OllamaRecordingCompositionOutcomeCode.Failed.ToString()
                || compositionFailure != OllamaRecordingCompositionFailureCode.CapabilityExpired.ToString()
                || completed != 0 || terminalSlot is not null
                || submission != SubmissionState.DefinitelyNotSubmitted.ToString()
                || statusCode is not null || receiptPresent || nestedDigest is not null)
                throw Failure("artifact_failed_result_invalid");
            return;
        }

        if (recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed.ToString()
            && recordingFailure == SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected.ToString())
        {
            if (compositionOutcome != OllamaRecordingCompositionOutcomeCode.Failed.ToString()
                || compositionFailure != OllamaRecordingCompositionFailureCode.EvidenceRejected.ToString()
                || completed != CognitionQualityCorpusV1.ScenarioCount
                || terminalSlot != CognitionQualityCorpusV1.ScenarioCount
                || submission != SubmissionState.ResponseReceived.ToString()
                || statusCode != 200 || !receiptPresent || nestedDigest is not null)
                throw Failure("artifact_failed_result_invalid");
            return;
        }

        if (completed == CognitionQualityCorpusV1.ScenarioCount || nestedDigest is not null || terminalSlot != completed + 1)
            throw Failure("artifact_terminal_result_invalid");
        ValidateTerminalSubmission(submission, statusCode);

        if (recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled.ToString()
            || recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.TimedOut.ToString())
        {
            bool cancelled = recordingOutcome == SnowGlobeOllamaLoopbackRecordingOutcomeCode.Cancelled.ToString();
            string expectedOutcome = cancelled ? OllamaRecordingCompositionOutcomeCode.Cancelled.ToString() : OllamaRecordingCompositionOutcomeCode.TimedOut.ToString();
            string expectedFailure = cancelled ? OllamaRecordingCompositionFailureCode.Cancelled.ToString() : OllamaRecordingCompositionFailureCode.TimedOut.ToString();
            if (compositionOutcome != expectedOutcome || compositionFailure != expectedFailure
                || recordingFailure != SnowGlobeOllamaLoopbackRecordingFailureCode.None.ToString()
                || (!receiptPresent && (!cancelled || completed != 0 || terminalSlot != 1
                    || submission != SubmissionState.DefinitelyNotSubmitted.ToString() || statusCode is not null)))
                throw Failure("artifact_terminal_result_invalid");
            return;
        }

        if (recordingOutcome != SnowGlobeOllamaLoopbackRecordingOutcomeCode.Failed.ToString()
            || compositionOutcome != OllamaRecordingCompositionOutcomeCode.Failed.ToString()
            || compositionFailure != recordingFailure
            || !Enum.TryParse(recordingFailure, false, out OllamaRecordingCompositionFailureCode mappedFailure)
            || mappedFailure is OllamaRecordingCompositionFailureCode.None
                or OllamaRecordingCompositionFailureCode.Cancelled
                or OllamaRecordingCompositionFailureCode.TimedOut
                or OllamaRecordingCompositionFailureCode.AuthorizationRejected
                or OllamaRecordingCompositionFailureCode.CompositionFailed)
            throw Failure("artifact_failed_result_invalid");

        if (!receiptPresent) throw Failure("artifact_failed_result_invalid");
        if (mappedFailure == OllamaRecordingCompositionFailureCode.RuntimeBindingInvalid
            && (completed != 0 || terminalSlot != 1 || submission != SubmissionState.DefinitelyNotSubmitted.ToString() || statusCode is not null))
            throw Failure("artifact_failed_result_invalid");
        if ((mappedFailure is OllamaRecordingCompositionFailureCode.RuntimeChanged or OllamaRecordingCompositionFailureCode.TransportPoisoned)
            && (submission != SubmissionState.DefinitelyNotSubmitted.ToString() || statusCode is not null))
            throw Failure("artifact_failed_result_invalid");
        if (mappedFailure == OllamaRecordingCompositionFailureCode.TransportFailure
            && (submission != SubmissionState.SubmissionUnknown.ToString() || statusCode is not null))
            throw Failure("artifact_failed_result_invalid");
        if (mappedFailure == OllamaRecordingCompositionFailureCode.HttpResponseRejected
            && (submission != SubmissionState.ResponseReceived.ToString() || statusCode is null or 200))
            throw Failure("artifact_failed_result_invalid");
        if (mappedFailure is OllamaRecordingCompositionFailureCode.ResponseBodyRejected or OllamaRecordingCompositionFailureCode.WrapperRejected
            && (submission != SubmissionState.ResponseReceived.ToString() || statusCode != 200))
            throw Failure("artifact_failed_result_invalid");
    }

    private static void ValidateTerminalSubmission(string submission, int? statusCode)
    {
        if (submission == SubmissionState.ResponseReceived.ToString())
        {
            if (statusCode is < 100 or > 599) throw Failure("artifact_terminal_submission_invalid");
            return;
        }
        if (submission is not (nameof(SubmissionState.DefinitelyNotSubmitted) or nameof(SubmissionState.SubmissionUnknown))
            || statusCode is not null)
            throw Failure("artifact_terminal_submission_invalid");
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
