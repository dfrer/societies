using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

public sealed record ProviderRoutingOrchestrationInput(
    ReadOnlyMemory<byte>? ComparisonArtifactCanonicalUtf8,
    ReadOnlyMemory<byte>? OpenRouterReadinessObservationCanonicalUtf8,
    ReadOnlyMemory<byte>? OllamaReadinessObservationCanonicalUtf8,
    ProviderRoutingIntent Intent,
    long CurrentUnixMilliseconds);

/// <summary>Detached pre-transport evidence. It grants no provider execution capability.</summary>
public sealed class ProviderRoutingOrchestrationResult
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _claimLimitationCodes;

    internal ProviderRoutingOrchestrationResult(
        byte[] canonicalUtf8,
        string status,
        string comparisonArtifactDigestSha256,
        string assessmentDigestSha256,
        string initialAttemptRecordDigestSha256,
        string routingDecisionDigestSha256,
        string? claimedAttemptRecordDigestSha256,
        string attemptId,
        ProviderRoutingSelectedProvider? selectedProvider,
        string intentCode,
        long expiresAtUnixMilliseconds,
        string reasonCode,
        IReadOnlyList<string> claimLimitationCodes,
        string payloadDigestSha256)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        _claimLimitationCodes = claimLimitationCodes.ToArray();
        Status = status;
        ComparisonArtifactDigestSha256 = comparisonArtifactDigestSha256;
        AssessmentDigestSha256 = assessmentDigestSha256;
        InitialAttemptRecordDigestSha256 = initialAttemptRecordDigestSha256;
        RoutingDecisionDigestSha256 = routingDecisionDigestSha256;
        ClaimedAttemptRecordDigestSha256 = claimedAttemptRecordDigestSha256;
        AttemptId = attemptId;
        SelectedProvider = selectedProvider;
        IntentCode = intentCode;
        ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        ReasonCode = reasonCode;
        PayloadDigestSha256 = payloadDigestSha256;
        CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8);
    }

    public string SchemaVersion => ProviderRoutingOrchestrationModule.ResultSchemaVersion;
    public string Status { get; }
    public string ComparisonArtifactDigestSha256 { get; }
    public string AssessmentDigestSha256 { get; }
    public string InitialAttemptRecordDigestSha256 { get; }
    public string RoutingDecisionDigestSha256 { get; }
    public string? ClaimedAttemptRecordDigestSha256 { get; }
    public string AttemptId { get; }
    public ProviderRoutingSelectedProvider? SelectedProvider { get; }
    public string IntentCode { get; }
    public long ExpiresAtUnixMilliseconds { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> ClaimLimitationCodes =>
        Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

public sealed class ProviderRoutingOrchestrationException : Exception
{
    internal ProviderRoutingOrchestrationException(string code) : base(Close(code)) => Code = Close(code);
    public string Code { get; }

    private static string Close(string code) => code switch
    {
        "orchestration_already_used" or "orchestration_input_invalid" or
        "orchestration_input_size_invalid" or "orchestration_assessment_invalid" or
        "orchestration_comparison_unaccepted" or "orchestration_ledger_create_failed" or
        "orchestration_policy_failed" or "orchestration_ledger_claim_failed" or
        "orchestration_claim_terminal_or_ambiguous" or
        "orchestration_result_size_invalid" or "orchestration_result_utf8_invalid" or
        "orchestration_result_json_invalid" or "orchestration_result_shape_invalid" or
        "orchestration_result_value_invalid" or "orchestration_result_digest_invalid" or
        "orchestration_result_payload_digest_invalid" or
        "orchestration_result_binding_invalid" or "orchestration_result_noncanonical" => code,
        _ => "orchestration_failed"
    };
}

internal interface IProviderRoutingOrchestrationLedger
{
    ProviderRoutingAttemptRecord Create(ProviderRoutingAttemptCreateInput input);
    ProviderRoutingAttemptRecord ClaimDispatch(ProviderRoutingDispatchClaimInput input);
    ProviderRoutingAttemptRecord Validate(ReadOnlyMemory<byte> canonicalUtf8);
}

internal sealed class ExistingProviderRoutingAttemptLedger(
    ProviderRoutingAttemptLedgerModule ledger) : IProviderRoutingOrchestrationLedger
{
    public ProviderRoutingAttemptRecord Create(ProviderRoutingAttemptCreateInput input) =>
        ledger.Create(input);
    public ProviderRoutingAttemptRecord ClaimDispatch(ProviderRoutingDispatchClaimInput input) =>
        ledger.ClaimDispatch(input);
    public ProviderRoutingAttemptRecord Validate(ReadOnlyMemory<byte> canonicalUtf8) =>
        ledger.Validate(canonicalUtf8);
}

/// <summary>
/// Deep synchronous provider-neutral orchestration Module. It stops before provider transport.
/// </summary>
public sealed class ProviderRoutingOrchestrationModule
{
    public const string ResultSchemaVersion = "snow_globe_provider_routing_orchestration_result/v1";
    public const string ContractSchemaVersion = "snow_globe_provider_routing_orchestration_contract/v1";
    public const int MaximumResultBytes = 48 * 1024;
    public const int MaximumResultJsonDepth = 12;

    internal static readonly string[] ClaimLimitations =
    [
        "evidence_only_no_provider_transport_execution_authority",
        "prepared_means_durable_dispatch_claim_not_provider_submission",
        "one_shot_no_retry_fallback_or_alternate_attempt",
        "current_readiness_bounded_until_assessment_expiry",
        "provider_output_untrusted_deterministic_validation_authoritative",
        "local_integrity_anchor_does_not_prevent_whole_volume_rollback",
        "no_credential_payment_network_gameplay_or_world_authority"
    ];

    public static string ContractDigestSha256 { get; } = CognitionQualityHash.Sha256(
        Encoding.UTF8.GetBytes(
            ContractSchemaVersion + "|result_schema=" + ResultSchemaVersion +
            "|comparison_schema=" + CognitionQualityComparisonModule.SchemaVersion +
            "|accepted_comparison_digest=" +
                ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256 +
            "|readiness_schema=" + ProviderRoutingReadinessEvidenceModule.CurrentSchemaVersion +
            "|readiness_contract_digest=" +
                ProviderRoutingReadinessEvidenceModule.CurrentContractDigestSha256 +
            "|policy_schema=" + ProviderRoutingPolicyModule.PolicySchemaVersion +
            "|policy_digest=" + ProviderRoutingPolicyModule.PolicyDigestSha256 +
            "|ledger_record_schema=" + ProviderRoutingAttemptLedgerModule.RecordSchemaVersion +
            "|ledger_contract_digest=" + ProviderRoutingAttemptLedgerModule.ContractDigestSha256 +
            "|flow=assess_current,ledger_create,policy_decide,conditional_ledger_claim_dispatch" +
            "|prepared=authenticated_dispatch_started|not_prepared=authenticated_not_started_and_no_selection" +
            "|decision_inputs=authenticated_initial_intent_and_readiness" +
            "|claim_bindings=expected_initial_digest,selected_provider,routing_decision_digest" +
            "|single_time=assessment_assessed_at_equals_initial_created_at_equals_terminal_claimed_at" +
            "|terminal_validity=created_at_and_expires_at_exactly_carried_forward_from_initial" +
            "|unaccepted_comparison=no_attempt|no_selection=retained_authenticated_not_started" +
            "|post_claim_failure=validation,coherence,result_construction,canonical_embedding," +
                "size,unexpected_all_terminal_or_ambiguous_never_retryable" +
            "|caller_evidence=bounded_single_snapshot_zeroed|module_instance=one_shot" +
            "|result_limits=49152_bytes,depth_12|module_interface=prepare,validate" +
            "|limitations=" + string.Join(',', ClaimLimitations) +
            "|provider_transport=absent|retry_fallback_alternate_attempt=absent|world_authority=none"));

    private static readonly string[] RootNames =
    [
        "schema_version", "status", "contract_schema_version", "contract_digest_sha256",
        "comparison_artifact_digest_sha256", "comparison_schema_version",
        "comparison_recommendation", "intent", "assessed_at_unix_ms", "expires_at_unix_ms",
        "assessment", "initial_attempt_record", "routing_decision", "claimed_attempt_record",
        "selected_provider", "reason_code", "claim_limitation_codes",
        "orchestration_payload_digest_sha256"
    ];

    private readonly IProviderRoutingOrchestrationLedger _ledger;
    private int _used;

    internal ProviderRoutingOrchestrationModule(ProviderRoutingAttemptLedgerModule ledger)
        : this(new ExistingProviderRoutingAttemptLedger(
            ledger ?? throw new ArgumentNullException(nameof(ledger)))) { }

    internal ProviderRoutingOrchestrationModule(IProviderRoutingOrchestrationLedger ledger) =>
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

    public ProviderRoutingOrchestrationResult Prepare(ProviderRoutingOrchestrationInput input)
    {
        if (Interlocked.Exchange(ref _used, 1) != 0)
            throw Failure("orchestration_already_used");
        if (input is null) throw Failure("orchestration_input_invalid");
        if (!Enum.IsDefined(input.Intent)
            || input.CurrentUnixMilliseconds <= 0
            || input.CurrentUnixMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
                - ProviderReadinessObservationModule.ObservationLifetimeMilliseconds)
            throw Failure("orchestration_input_invalid");

        byte[]? comparison = null;
        byte[]? openRouter = null;
        byte[]? ollama = null;
        try
        {
            comparison = SnapshotOptional(input.ComparisonArtifactCanonicalUtf8,
                CognitionQualityComparisonModule.MaximumArtifactBytes);
            openRouter = SnapshotOptional(input.OpenRouterReadinessObservationCanonicalUtf8,
                ProviderReadinessObservationModule.MaximumObservationBytes);
            ollama = SnapshotOptional(input.OllamaReadinessObservationCanonicalUtf8,
                ProviderReadinessObservationModule.MaximumObservationBytes);

            ProviderRoutingCurrentReadinessAssessment assessment;
            try
            {
                assessment = ProviderRoutingReadinessEvidenceModule.AssessCurrent(
                    new ProviderRoutingReadinessEvidenceInput(comparison),
                    openRouter, ollama, input.CurrentUnixMilliseconds);
            }
            catch (ProviderRoutingReadinessEvidenceException)
            {
                throw Failure("orchestration_assessment_invalid");
            }
            catch { throw Failure("orchestration_assessment_invalid"); }
            if (assessment.GapCodes.Contains(
                "accepted_comparison_evidence_unproven", StringComparer.Ordinal))
                throw Failure("orchestration_comparison_unaccepted");

            ProviderRoutingAttemptRecord initial;
            try
            {
                initial = _ledger.Create(new ProviderRoutingAttemptCreateInput(
                    assessment.CanonicalUtf8, input.Intent, input.CurrentUnixMilliseconds));
                initial = _ledger.Validate(initial.CanonicalUtf8);
            }
            catch (ProviderRoutingAttemptLedgerException)
            {
                throw Failure("orchestration_ledger_create_failed");
            }
            catch { throw Failure("orchestration_ledger_create_failed"); }
            ValidateInitial(initial, assessment, input.Intent, input.CurrentUnixMilliseconds);

            ProviderRoutingDecision decision;
            try
            {
                decision = ProviderRoutingPolicyModule.Decide(new ProviderRoutingPolicyInput(
                    input.Intent,
                    Readiness(initial.OpenRouterReadinessCode),
                    Readiness(initial.OllamaReadinessCode),
                    ProviderPrimaryAttemptState.NotStarted), comparison);
                decision = ProviderRoutingPolicyModule.Validate(decision.CanonicalUtf8);
            }
            catch (ProviderRoutingPolicyException)
            {
                throw Failure("orchestration_policy_failed");
            }
            catch (ProviderRoutingOrchestrationException) { throw; }
            catch { throw Failure("orchestration_policy_failed"); }
            ValidateDecision(decision, initial);

            if (decision.SelectedProvider is null)
                return Build("not_prepared", assessment, initial, decision, null);

            ProviderRoutingAttemptRecord claimed;
            try
            {
                claimed = _ledger.ClaimDispatch(new ProviderRoutingDispatchClaimInput(
                    initial.AttemptId, initial.CanonicalDigestSha256,
                    decision.SelectedProvider.Value, decision.CanonicalUtf8,
                    input.CurrentUnixMilliseconds));
            }
            catch (ProviderRoutingAttemptLedgerException exception)
            {
                throw Failure(exception.Code is "attempt_claim_outcome_ambiguous"
                    or "attempt_already_terminal" or "attempt_poisoned"
                    ? "orchestration_claim_terminal_or_ambiguous"
                    : "orchestration_ledger_claim_failed");
            }
            catch { throw Failure("orchestration_claim_terminal_or_ambiguous"); }
            try
            {
                claimed = _ledger.Validate(claimed.CanonicalUtf8);
                ValidateClaimed(claimed, initial, decision, input.CurrentUnixMilliseconds);
                return Build("prepared", assessment, initial, decision, claimed);
            }
            catch
            {
                // ClaimDispatch returning means terminal material may already be durable. Any
                // later validation, coherence, or result-construction failure is therefore
                // terminal/ambiguous, never retryable.
                throw Failure("orchestration_claim_terminal_or_ambiguous");
            }
        }
        catch (ProviderRoutingOrchestrationException) { throw; }
        catch { throw Failure("orchestration_failed"); }
        finally
        {
            Zero(comparison);
            Zero(openRouter);
            Zero(ollama);
        }
    }

    public ProviderRoutingOrchestrationResult Validate(ReadOnlyMemory<byte> canonicalUtf8)
    {
        if (canonicalUtf8.Length is < 1 or > MaximumResultBytes)
            throw Failure("orchestration_result_size_invalid");
        byte[] snapshot = canonicalUtf8.Span.ToArray();
        try
        {
            JsonDocument document = Parse(snapshot);
            using (document)
            {
                JsonElement root = document.RootElement;
                RequireOrder(root, RootNames);
                RequireString(root, "schema_version", ResultSchemaVersion);
                string status = RequireClosed(root, "status", ["prepared", "not_prepared"]);
                RequireString(root, "contract_schema_version", ContractSchemaVersion);
                RequireString(root, "contract_digest_sha256", ContractDigestSha256);
                string comparisonDigest = RequireDigest(root, "comparison_artifact_digest_sha256");
                if (comparisonDigest != ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256)
                    throw Failure("orchestration_result_binding_invalid");
                RequireString(root, "comparison_schema_version",
                    CognitionQualityComparisonModule.SchemaVersion);
                RequireString(root, "comparison_recommendation", "openrouter_default");
                string intentCode = RequireClosed(root, "intent", ["preferred_online", "local_only"]);
                long assessedAt = RequireTime(root, "assessed_at_unix_ms");
                long expiresAt = RequireTime(root, "expires_at_unix_ms");

                byte[] assessmentBytes = Canonicalize(root.GetProperty("assessment"));
                byte[] initialBytes = Canonicalize(root.GetProperty("initial_attempt_record"));
                byte[] decisionBytes = Canonicalize(root.GetProperty("routing_decision"));
                byte[]? claimedBytes = root.GetProperty("claimed_attempt_record").ValueKind ==
                    JsonValueKind.Null ? null : Canonicalize(root.GetProperty("claimed_attempt_record"));
                try
                {
                    ProviderRoutingCurrentReadinessAssessment assessment;
                    ProviderRoutingAttemptRecord initial;
                    ProviderRoutingDecision decision;
                    ProviderRoutingAttemptRecord? claimed;
                    try
                    {
                        assessment = ProviderRoutingReadinessEvidenceModule.ValidateCurrent(
                            assessmentBytes, assessedAt);
                        initial = _ledger.Validate(initialBytes);
                        decision = ProviderRoutingPolicyModule.Validate(decisionBytes);
                        claimed = claimedBytes is null ? null : _ledger.Validate(claimedBytes);
                    }
                    catch
                    {
                        throw Failure("orchestration_result_binding_invalid");
                    }

                    string? providerCode = RequireNullableClosed(root, "selected_provider",
                        ["openrouter", "ollama"]);
                    ProviderRoutingSelectedProvider? selectedProvider = providerCode switch
                    {
                        "openrouter" => ProviderRoutingSelectedProvider.OpenRouter,
                        "ollama" => ProviderRoutingSelectedProvider.Ollama,
                        _ => null
                    };
                    string reason = RequireText(root, "reason_code");
                    RequireStrings(root.GetProperty("claim_limitation_codes"), ClaimLimitations);
                    string payloadDigest = RequireDigest(root,
                        "orchestration_payload_digest_sha256");
                    byte[] payload = CanonicalizeWithoutLast(root);
                    try
                    {
                        if (CognitionQualityHash.Sha256(payload) != payloadDigest)
                            throw Failure("orchestration_result_payload_digest_invalid");
                    }
                    finally { CryptographicOperations.ZeroMemory(payload); }

                    ProviderRoutingIntent intent = intentCode == "preferred_online"
                        ? ProviderRoutingIntent.PreferredOnline
                        : ProviderRoutingIntent.LocalOnly;
                    ValidateInitial(initial, assessment, intent, assessedAt);
                    ValidateDecision(decision, initial);
                    if (assessment.ExpiresAtUnixMilliseconds != expiresAt)
                        throw Failure("orchestration_result_binding_invalid");
                    if (selectedProvider != decision.SelectedProvider || reason != decision.ReasonCode)
                        throw Failure("orchestration_result_binding_invalid");
                    if (status == "prepared")
                    {
                        if (claimed is null || selectedProvider is null)
                            throw Failure("orchestration_result_binding_invalid");
                        ValidateClaimed(claimed, initial, decision, assessedAt);
                    }
                    else if (claimed is not null || selectedProvider is not null)
                        throw Failure("orchestration_result_binding_invalid");

                    ProviderRoutingOrchestrationResult recreated =
                        Build(status, assessment, initial, decision, claimed);
                    if (!recreated.CanonicalUtf8.Span.SequenceEqual(snapshot))
                        throw Failure("orchestration_result_noncanonical");
                    return recreated;
                }
                finally
                {
                    Zero(assessmentBytes);
                    Zero(initialBytes);
                    Zero(decisionBytes);
                    Zero(claimedBytes);
                }
            }
        }
        catch (ProviderRoutingOrchestrationException) { throw; }
        catch { throw Failure("orchestration_result_binding_invalid"); }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private ProviderRoutingOrchestrationResult Build(
        string status,
        ProviderRoutingCurrentReadinessAssessment assessment,
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingDecision decision,
        ProviderRoutingAttemptRecord? claimed)
    {
        byte[] payload = Write(status, assessment, initial, decision, claimed, null);
        string payloadDigest = CognitionQualityHash.Sha256(payload);
        CryptographicOperations.ZeroMemory(payload);
        byte[] canonical = Write(status, assessment, initial, decision, claimed, payloadDigest);
        if (canonical.Length is < 1 or > MaximumResultBytes)
        {
            CryptographicOperations.ZeroMemory(canonical);
            throw Failure("orchestration_result_size_invalid");
        }
        return new ProviderRoutingOrchestrationResult(
            canonical, status, ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256,
            assessment.CanonicalDigestSha256, initial.CanonicalDigestSha256,
            decision.CanonicalDigestSha256, claimed?.CanonicalDigestSha256,
            initial.AttemptId, decision.SelectedProvider, initial.IntentCode,
            assessment.ExpiresAtUnixMilliseconds, decision.ReasonCode,
            ClaimLimitations, payloadDigest);
    }

    private static void ValidateInitial(
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingCurrentReadinessAssessment assessment,
        ProviderRoutingIntent intent,
        long assessedAt)
    {
        string intentCode = intent == ProviderRoutingIntent.PreferredOnline
            ? "preferred_online" : "local_only";
        if (initial.State != ProviderRoutingAttemptState.NotStarted
            || initial.Sequence != 0 || initial.PreviousRecordDigestSha256 is not null
            || initial.SelectedProvider is not null || initial.RoutingDecisionDigestSha256 is not null
            || initial.CreatedAtUnixMilliseconds != assessedAt
            || initial.ExpiresAtUnixMilliseconds != assessment.ExpiresAtUnixMilliseconds
            || initial.ComparisonArtifactDigestSha256 !=
                ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256
            || initial.ReadinessAssessmentDigestSha256 != assessment.CanonicalDigestSha256
            || initial.ReadinessAssessmentSchemaVersion != assessment.SchemaVersion
            || initial.OpenRouterReadinessCode != assessment.OpenRouterCurrentReadiness
            || initial.OllamaReadinessCode != assessment.OllamaCurrentReadiness
            || initial.IntentCode != intentCode)
            throw Failure("orchestration_result_binding_invalid");
    }

    private static void ValidateDecision(
        ProviderRoutingDecision decision,
        ProviderRoutingAttemptRecord initial)
    {
        if (decision.PrimaryAttemptStateCode != "not_started"
            || decision.ComparisonStatus != "accepted"
            || decision.ComparisonArtifactDigestSha256 !=
                ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256
            || decision.ComparisonSchemaVersion != CognitionQualityComparisonModule.SchemaVersion
            || decision.ComparisonRecommendation != "openrouter_default"
            || decision.IntentCode != initial.IntentCode
            || decision.OpenRouterReadinessCode != initial.OpenRouterReadinessCode
            || decision.OllamaReadinessCode != initial.OllamaReadinessCode)
            throw Failure("orchestration_result_binding_invalid");
    }

    private static void ValidateClaimed(
        ProviderRoutingAttemptRecord claimed,
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingDecision decision,
        long claimedAt)
    {
        if (decision.SelectedProvider is null
            || claimed.State != ProviderRoutingAttemptState.DispatchStarted
            || claimed.Sequence != 1
            || claimed.AttemptId != initial.AttemptId
            || claimed.PreviousRecordDigestSha256 != initial.CanonicalDigestSha256
            || claimed.CreatedAtUnixMilliseconds != initial.CreatedAtUnixMilliseconds
            || claimed.ExpiresAtUnixMilliseconds != initial.ExpiresAtUnixMilliseconds
            || claimed.ComparisonArtifactDigestSha256 != initial.ComparisonArtifactDigestSha256
            || claimed.ReadinessAssessmentDigestSha256 != initial.ReadinessAssessmentDigestSha256
            || claimed.ReadinessAssessmentSchemaVersion != initial.ReadinessAssessmentSchemaVersion
            || claimed.OpenRouterReadinessCode != initial.OpenRouterReadinessCode
            || claimed.OllamaReadinessCode != initial.OllamaReadinessCode
            || claimed.IntentCode != initial.IntentCode
            || claimed.SelectedProvider != decision.SelectedProvider
            || claimed.RoutingDecisionDigestSha256 != decision.CanonicalDigestSha256
            || claimed.ClaimedAtUnixMilliseconds != claimedAt
            || claimed.TerminalReasonCode != "dispatch_claimed")
            throw Failure("orchestration_result_binding_invalid");
    }

    private static byte[] Write(
        string status,
        ProviderRoutingCurrentReadinessAssessment assessment,
        ProviderRoutingAttemptRecord initial,
        ProviderRoutingDecision decision,
        ProviderRoutingAttemptRecord? claimed,
        string? payloadDigest)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        writer.WriteString("schema_version", ResultSchemaVersion);
        writer.WriteString("status", status);
        writer.WriteString("contract_schema_version", ContractSchemaVersion);
        writer.WriteString("contract_digest_sha256", ContractDigestSha256);
        writer.WriteString("comparison_artifact_digest_sha256",
            ProviderRoutingPolicyModule.AcceptedComparisonArtifactDigestSha256);
        writer.WriteString("comparison_schema_version", CognitionQualityComparisonModule.SchemaVersion);
        writer.WriteString("comparison_recommendation", "openrouter_default");
        writer.WriteString("intent", initial.IntentCode);
        writer.WriteNumber("assessed_at_unix_ms", assessment.AssessedAtUnixMilliseconds);
        writer.WriteNumber("expires_at_unix_ms", assessment.ExpiresAtUnixMilliseconds);
        WriteEmbedded(writer, "assessment", assessment.CanonicalUtf8.Span);
        WriteEmbedded(writer, "initial_attempt_record", initial.CanonicalUtf8.Span);
        WriteEmbedded(writer, "routing_decision", decision.CanonicalUtf8.Span);
        if (claimed is null) writer.WriteNull("claimed_attempt_record");
        else WriteEmbedded(writer, "claimed_attempt_record", claimed.CanonicalUtf8.Span);
        if (decision.SelectedProvider is null) writer.WriteNull("selected_provider");
        else writer.WriteString("selected_provider", decision.SelectedProvider ==
            ProviderRoutingSelectedProvider.OpenRouter ? "openrouter" : "ollama");
        writer.WriteString("reason_code", decision.ReasonCode);
        WriteStrings(writer, "claim_limitation_codes", ClaimLimitations);
        if (payloadDigest is not null)
            writer.WriteString("orchestration_payload_digest_sha256", payloadDigest);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[]? SnapshotOptional(ReadOnlyMemory<byte>? memory, int maximumBytes)
    {
        if (!memory.HasValue || memory.Value.Length == 0) return null;
        if (memory.Value.Length > maximumBytes)
            throw Failure("orchestration_input_size_invalid");
        return memory.Value.Span.ToArray();
    }

    private static ProviderReadiness Readiness(string code) => code switch
    {
        "ready" => ProviderReadiness.Ready,
        "not_ready" => ProviderReadiness.NotReady,
        "unknown" => ProviderReadiness.Unknown,
        _ => throw Failure("orchestration_result_binding_invalid")
    };

    private static JsonDocument Parse(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            RejectDuplicates(bytes);
            Utf8JsonReader reader = new(bytes, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumResultJsonDepth
            });
            JsonDocument document = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
            {
                document.Dispose();
                throw Failure("orchestration_result_json_invalid");
            }
            return document;
        }
        catch (DecoderFallbackException) { throw Failure("orchestration_result_utf8_invalid"); }
        catch (ProviderRoutingOrchestrationException) { throw; }
        catch (JsonException) { throw Failure("orchestration_result_json_invalid"); }
    }

    private static void RejectDuplicates(ReadOnlySpan<byte> bytes)
    {
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumResultJsonDepth
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
                throw Failure("orchestration_result_shape_invalid");
        }
    }

    private static void RequireOrder(JsonElement root, IReadOnlyList<string> names)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw Failure("orchestration_result_shape_invalid");
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        if (properties.Length != names.Count)
            throw Failure("orchestration_result_shape_invalid");
        for (int index = 0; index < names.Count; index++)
            if (properties[index].Name != names[index])
                throw Failure("orchestration_result_shape_invalid");
    }

    private static string RequireText(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (text is null || JsonSerializer.Serialize(text) != value.GetRawText())
            throw Failure("orchestration_result_value_invalid");
        return text;
    }

    private static string RequireClosed(
        JsonElement root,
        string name,
        IReadOnlyList<string> allowed)
    {
        string text = RequireText(root, name);
        if (!allowed.Contains(text, StringComparer.Ordinal))
            throw Failure("orchestration_result_value_invalid");
        return text;
    }

    private static string? RequireNullableClosed(
        JsonElement root,
        string name,
        IReadOnlyList<string> allowed)
    {
        if (root.GetProperty(name).ValueKind == JsonValueKind.Null) return null;
        return RequireClosed(root, name, allowed);
    }

    private static void RequireString(JsonElement root, string name, string expected)
    {
        if (RequireText(root, name) != expected)
            throw Failure("orchestration_result_value_invalid");
    }

    private static string RequireDigest(JsonElement root, string name)
    {
        string value = RequireText(root, name);
        if (!ProviderRoutingAttemptLedgerCodec.IsDigest(value))
            throw Failure("orchestration_result_digest_invalid");
        return value;
    }

    private static long RequireTime(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long parsed)
            || parsed <= 0 || parsed > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
            || value.GetRawText() != parsed.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw Failure("orchestration_result_value_invalid");
        return parsed;
    }

    private static void RequireStrings(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expected.Count)
            throw Failure("orchestration_result_value_invalid");
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String
                || item.GetString() != expected[index++]
                || JsonSerializer.Serialize(item.GetString()) != item.GetRawText())
                throw Failure("orchestration_result_value_invalid");
    }

    private static byte[] Canonicalize(JsonElement element)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        element.WriteTo(writer);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] CanonicalizeWithoutLast(JsonElement root)
    {
        JsonProperty[] properties = root.EnumerateObject().ToArray();
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartObject();
        foreach (JsonProperty property in properties[..^1])
        {
            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteEmbedded(Utf8JsonWriter writer, string name, ReadOnlySpan<byte> canonical)
    {
        byte[] snapshot = canonical.ToArray();
        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot);
            writer.WritePropertyName(name);
            document.RootElement.WriteTo(writer);
        }
        finally { CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static void WriteStrings(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (string value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void Zero(byte[]? bytes)
    {
        if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }

    private static ProviderRoutingOrchestrationException Failure(string code) => new(code);
}
