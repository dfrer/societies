using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Societies.SnowGlobe;

/// <summary>Detached, raw-response-free record binding one canonical prompt publication to one recorded-response run.</summary>
public sealed class CognitionQualityRecordingEvidence
{
    private readonly byte[] _canonicalUtf8;
    private readonly string[] _claimLimitationCodes;
    internal CognitionQualityRecordingEvidence(byte[] canonicalUtf8, string payloadDigestSha256, CognitionQualityPromptEnvelopePublication promptPublication, CognitionQualityRecordedResponseRun recordedResponseRun, string responseSetDigestSha256, IEnumerable<string> claimLimitationCodes)
    { _canonicalUtf8 = canonicalUtf8.ToArray(); PayloadDigestSha256 = payloadDigestSha256; PromptPublication = promptPublication; RecordedResponseRun = recordedResponseRun; ResponseSetDigestSha256 = responseSetDigestSha256; _claimLimitationCodes = claimLimitationCodes.ToArray(); CanonicalDigestSha256 = CognitionQualityHash.Sha256(_canonicalUtf8); }
    public string SchemaVersion => CognitionQualityRecordingEvidenceModule.SchemaVersion;
    public string Status => "complete";
    public string Semantics => "offline_recording_evidence_binding_only";
    public CognitionQualityPromptEnvelopePublication PromptPublication { get; }
    public CognitionQualityRecordedResponseRun RecordedResponseRun { get; }
    public string ResponseSetDigestSha256 { get; }
    public IReadOnlyList<string> ClaimLimitationCodes => Array.AsReadOnly(_claimLimitationCodes.ToArray());
    public string PayloadDigestSha256 { get; }
    public string CanonicalDigestSha256 { get; }
    public string CanonicalJson => Encoding.UTF8.GetString(_canonicalUtf8);
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8.ToArray();
}

/// <summary>Closed, non-echoing failure surface for recording-evidence binding.</summary>
public sealed class CognitionQualityRecordingEvidenceException : Exception
{
    internal CognitionQualityRecordingEvidenceException(string code) : base(code) { if (!CognitionQualityRecordingEvidenceErrors.IsAllowlisted(code)) throw new ArgumentOutOfRangeException(nameof(code)); Code = code; }
    public string Code { get; }
}

/// <summary>Pure synchronous module that binds exact offline recorded bytes to an existing prompt publication and runner result.</summary>
public static class CognitionQualityRecordingEvidenceModule
{
    public const string SchemaVersion = "snow_globe_cognition_quality_recording_evidence/v1";
    public const int MaximumEvidenceBytes = 192 * 1024;
    private static readonly string[] ClaimLimitations = ["offline_recording_evidence_only", "identity_attribution_is_caller_attested", "response_binding_is_caller_attested", "no_transport_delivery_attestation", "no_execution_attestation", "raw_response_not_retained", "no_provider_status_retry_or_charge_evidence", "offline_fixed_corpus", "observable_action_utility_only", "no_general_quality_claim", "no_cost_claim", "no_winner_claim"];

    public static CognitionQualityRecordingEvidence Create(CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, IReadOnlyList<ReadOnlyMemory<byte>> recordedResponses)
    {
        ArgumentNullException.ThrowIfNull(publication); ArgumentNullException.ThrowIfNull(provenance); ArgumentNullException.ThrowIfNull(recordedResponses);
        ValidatePublication(publication);
        if (!string.Equals(provenance.PromptRevision, publication.PromptRevision, StringComparison.Ordinal)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.ProvenancePromptRevisionInvalid);
        if (!string.Equals(provenance.ProposalSchemaVersion, CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, StringComparison.Ordinal)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.ProvenanceProposalSchemaInvalid);
        if (!ProvenanceCoherent(provenance)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordingBindingIntegrityInvalid);
        if (recordedResponses.Count != CognitionQualityCorpusV1.ScenarioCount) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.ResponseCountInvalid);
        byte[][] snapshots = SnapshotResponses(recordedResponses); IReadOnlyList<CognitionQualityRecordedResponseFixture>? fixtures = null;
        try
        {
            string responseSetDigest = ComputeResponseSetDigest(publication.Slots, snapshots);
            try { fixtures = publication.BindRecordedResponses(provenance, snapshots.Select(snapshot => (ReadOnlyMemory<byte>)snapshot).ToArray()); } catch (CognitionQualityPromptEnvelopeException exception) { throw new CognitionQualityRecordingEvidenceException(MapPromptError(exception.Code)); }
            CognitionQualityRecordedResponseRun run;
            try { run = CognitionQualityRecordedResponseRunnerModule.Run(provenance, fixtures); } catch (CognitionQualityRecordedResponseRunnerException) { throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordedResponseRunInvalid); }
            ValidateRun(publication, provenance, snapshots, responseSetDigest, run);
            byte[] payload = WriteEvidence(publication.CanonicalUtf8.Span, provenance.CanonicalUtf8.Span, run.CanonicalUtf8.Span, responseSetDigest, null);
            string payloadDigest = CognitionQualityHash.Sha256(payload); CryptographicOperations.ZeroMemory(payload);
            byte[] canonical = WriteEvidence(publication.CanonicalUtf8.Span, provenance.CanonicalUtf8.Span, run.CanonicalUtf8.Span, responseSetDigest, payloadDigest);
            if (canonical.Length is 0 or > MaximumEvidenceBytes) { CryptographicOperations.ZeroMemory(canonical); throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordingEvidenceSizeInvalid); }
            return new CognitionQualityRecordingEvidence(canonical, payloadDigest, publication, run, responseSetDigest, ClaimLimitations);
        }
        finally { if (fixtures is not null) foreach (CognitionQualityRecordedResponseFixture fixture in fixtures) fixture.ClearResponseUtf8(); foreach (byte[] snapshot in snapshots) CryptographicOperations.ZeroMemory(snapshot); }
    }

    private static byte[][] SnapshotResponses(IReadOnlyList<ReadOnlyMemory<byte>> responses)
    {
        byte[][] snapshots = new byte[CognitionQualityCorpusV1.ScenarioCount][];
        try { int aggregate = 0; for (int index = 0; index < snapshots.Length; index++) { byte[] snapshot = responses[index].ToArray(); if (snapshot.Length is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes) { CryptographicOperations.ZeroMemory(snapshot); throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.ResponseSizeInvalid); } aggregate = checked(aggregate + snapshot.Length); if (aggregate > CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes) { CryptographicOperations.ZeroMemory(snapshot); throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.ResponseAggregateSizeInvalid); } snapshots[index] = snapshot; } return snapshots; }
        catch { foreach (byte[]? snapshot in snapshots) if (snapshot is not null) CryptographicOperations.ZeroMemory(snapshot); throw; }
    }

    private static void ValidatePublication(CognitionQualityPromptEnvelopePublication publication)
    {
        if (!string.Equals(publication.SchemaVersion, CognitionQualityPromptEnvelopeBuilderModule.SchemaVersion, StringComparison.Ordinal) || !string.Equals(publication.Status, "complete", StringComparison.Ordinal) || !string.Equals(publication.BuilderIdentity, CognitionQualityPromptEnvelopeBuilderModule.BuilderIdentity, StringComparison.Ordinal) || !string.Equals(publication.PromptSchemaVersion, CognitionQualityPromptEnvelopeBuilderModule.PromptSchemaVersion, StringComparison.Ordinal) || !SnowGlobeInferenceIdentity.IsCanonical(publication.PromptRevision) || publication.Slots.Count != CognitionQualityCorpusV1.ScenarioCount || publication.CanonicalUtf8.Length is < 1 or > CognitionQualityPromptEnvelopeBuilderModule.MaximumPublicationBytes) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.PromptPublicationInvalid);
        CognitionQualityPromptEnvelopePublication expected;
        try { expected = CognitionQualityPromptEnvelopeBuilderModule.Create(publication.PromptRevision); } catch (CognitionQualityPromptEnvelopeException) { throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.PromptPublicationInvalid); }
        if (!publication.CanonicalUtf8.Span.SequenceEqual(expected.CanonicalUtf8.Span) || !string.Equals(publication.PayloadDigestSha256, expected.PayloadDigestSha256, StringComparison.Ordinal) || !string.Equals(publication.CanonicalDigestSha256, expected.CanonicalDigestSha256, StringComparison.Ordinal) || !string.Equals(publication.PromptSetDigestSha256, expected.PromptSetDigestSha256, StringComparison.Ordinal) || !publication.ClaimLimitationCodes.SequenceEqual(expected.ClaimLimitationCodes, StringComparer.Ordinal)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.PromptPublicationInvalid);
        CognitionQualityCorpusSnapshot corpus; try { corpus = CognitionQualityCorpusV1.CreateSnapshot(); } catch (CognitionQualityException) { throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.PromptPublicationInvalid); }
        IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots = publication.Slots;
        for (int index = 0; index < slots.Count; index++) { CognitionQualityPromptEnvelopeSlot slot = slots[index]; CognitionQualityPromptEnvelopeSlot expectedSlot = expected.Slots[index]; CognitionQualityScenario scenario = corpus.Scenarios[index]; if (!string.Equals(slot.ScenarioId, scenario.ScenarioId, StringComparison.Ordinal) || !string.Equals(slot.ScenarioId, expectedSlot.ScenarioId, StringComparison.Ordinal) || !string.Equals(slot.ObservationDigestSha256, expectedSlot.ObservationDigestSha256, StringComparison.Ordinal) || slot.PromptByteCount != expectedSlot.PromptByteCount || !string.Equals(slot.PromptDigestSha256, expectedSlot.PromptDigestSha256, StringComparison.Ordinal) || !slot.PromptUtf8.Span.SequenceEqual(expectedSlot.PromptUtf8.Span)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.PromptPublicationInvalid); }
    }

    private static void ValidateRun(CognitionQualityPromptEnvelopePublication publication, CognitionQualityExecutionProvenance provenance, IReadOnlyList<byte[]> responses, string responseSetDigest, CognitionQualityRecordedResponseRun run)
    {
        if (!string.Equals(run.SchemaVersion, CognitionQualityRecordedResponseRunnerModule.SchemaVersion, StringComparison.Ordinal) || !string.Equals(run.Status, "complete", StringComparison.Ordinal) || !string.Equals(run.RunnerIdentity, CognitionQualityRecordedResponseRunnerModule.RunnerIdentity, StringComparison.Ordinal) || !string.Equals(run.ParserIdentity, CognitionQualityRecordedResponseRunnerModule.ParserIdentity, StringComparison.Ordinal) || run.ResponseBindings.Count != CognitionQualityCorpusV1.ScenarioCount || run.ProposalBatch.Count != CognitionQualityCorpusV1.ScenarioCount || run.CanonicalUtf8.Length is < 1 or > CognitionQualityRecordedResponseRunnerModule.MaximumRunBytes || !DigestCoherent(run.CanonicalUtf8.Span, run.CanonicalDigestSha256, "run_payload_digest_sha256", run.PayloadDigestSha256)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordedResponseRunInvalid);
        CognitionQualityExecutionEvidence evidence = run.ExecutionEvidence;
        CognitionQualityCorpusSnapshot corpus;
        try { corpus = CognitionQualityCorpusV1.CreateSnapshot(); } catch (CognitionQualityException) { throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordingBindingIntegrityInvalid); }
        if (!ProvenanceCoherent(evidence.Provenance) || !string.Equals(evidence.Provenance.ProvenanceDigestSha256, provenance.ProvenanceDigestSha256, StringComparison.Ordinal) || !string.Equals(evidence.Provenance.PromptRevision, publication.PromptRevision, StringComparison.Ordinal) || !string.Equals(evidence.Provenance.ProposalSchemaVersion, CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, StringComparison.Ordinal) || evidence.ScenarioCount != CognitionQualityCorpusV1.ScenarioCount || !string.Equals(evidence.QualityContract.CorpusDigestSha256, corpus.CanonicalDigestSha256, StringComparison.Ordinal) || !string.Equals(evidence.QualityContract.ScoringDigestSha256, CognitionQualityCorpusV1.ScoringDigestSha256, StringComparison.Ordinal) || !string.Equals(evidence.QualityContract.ValidatorIdentity, CognitionQualityCorpusV1.ValidatorIdentity, StringComparison.Ordinal) || !string.Equals(evidence.QualityContract.SubmissionDigestSha256, CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(evidence.RecordedSubmissionCanonicalJson)), StringComparison.Ordinal) || !string.Equals(evidence.QualityContract.ReportDigestSha256, CognitionQualityHash.Sha256(Encoding.UTF8.GetBytes(evidence.QualityReportCanonicalJson)), StringComparison.Ordinal) || evidence.CanonicalUtf8.Length is < 1 or > CognitionQualityExecutionEvidenceModule.MaximumEvidenceBytes || !DigestCoherent(evidence.CanonicalUtf8.Span, evidence.CanonicalDigestSha256, "evidence_payload_digest_sha256", evidence.PayloadDigestSha256)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordingBindingIntegrityInvalid);
        IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots = publication.Slots;
        for (int index = 0; index < slots.Count; index++) { CognitionQualityRecordedResponseBinding binding = run.ResponseBindings[index]; if (!string.Equals(binding.ScenarioId, slots[index].ScenarioId, StringComparison.Ordinal) || !string.Equals(binding.ObservationDigestSha256, slots[index].ObservationDigestSha256, StringComparison.Ordinal) || binding.ResponseByteCount != responses[index].Length || !string.Equals(binding.ResponseDigestSha256, CognitionQualityHash.Sha256(responses[index]), StringComparison.Ordinal) || !string.Equals(run.ProposalBatch[index].ScenarioId, slots[index].ScenarioId, StringComparison.Ordinal)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordingBindingIntegrityInvalid); }
        if (!string.Equals(responseSetDigest, ComputeResponseSetDigest(slots, responses), StringComparison.Ordinal)) throw new CognitionQualityRecordingEvidenceException(CognitionQualityRecordingEvidenceErrors.RecordingBindingIntegrityInvalid);
    }

    private static bool DigestCoherent(ReadOnlySpan<byte> canonical, string canonicalDigest, string payloadProperty, string payloadDigest)
    {
        if (!string.Equals(CognitionQualityHash.Sha256(canonical), canonicalDigest, StringComparison.Ordinal)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(canonical.ToArray()); if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            JsonProperty[] properties = document.RootElement.EnumerateObject().ToArray(); if (properties.Length == 0 || properties[^1].Name != payloadProperty || properties[^1].Value.ValueKind != JsonValueKind.String || !string.Equals(properties[^1].Value.GetString(), payloadDigest, StringComparison.Ordinal) || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) return false;
            ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer); writer.WriteStartObject(); foreach (JsonProperty property in properties[..^1]) { writer.WritePropertyName(property.Name); writer.WriteRawValue(property.Value.GetRawText(), skipInputValidation: false); } writer.WriteEndObject(); writer.Flush(); return string.Equals(CognitionQualityHash.Sha256(buffer.WrittenSpan), payloadDigest, StringComparison.Ordinal);
        }
        catch (JsonException) { return false; }
    }
    private static bool ProvenanceCoherent(CognitionQualityExecutionProvenance provenance)
    {
        if (!Enum.IsDefined(provenance.Lane)) return false;
        byte[] payload = CognitionQualityExecutionProvenance.WriteCanonical(provenance.Lane, provenance.ModelIdentity, provenance.ModelRevisionIdentity, provenance.ExecutionPolicyDigestSha256, provenance.PromptRevision, provenance.ProposalSchemaVersion, provenance.LocalAdapterIdentity, null);
        byte[] canonical = CognitionQualityExecutionProvenance.WriteCanonical(provenance.Lane, provenance.ModelIdentity, provenance.ModelRevisionIdentity, provenance.ExecutionPolicyDigestSha256, provenance.PromptRevision, provenance.ProposalSchemaVersion, provenance.LocalAdapterIdentity, provenance.ProvenanceDigestSha256);
        try { return string.Equals(CognitionQualityHash.Sha256(payload), provenance.ProvenanceDigestSha256, StringComparison.Ordinal) && canonical.AsSpan().SequenceEqual(provenance.CanonicalUtf8.Span); }
        finally { CryptographicOperations.ZeroMemory(payload); CryptographicOperations.ZeroMemory(canonical); }
    }
    private static string ComputePromptSetDigest(IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots)
    { ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer); writer.WriteStartArray(); foreach (CognitionQualityPromptEnvelopeSlot slot in slots) { writer.WriteStartObject(); writer.WriteString("scenario_id", slot.ScenarioId); writer.WriteString("observation_digest_sha256", slot.ObservationDigestSha256); writer.WriteNumber("prompt_byte_count", slot.PromptByteCount); writer.WriteString("prompt_digest_sha256", slot.PromptDigestSha256); writer.WriteEndObject(); } writer.WriteEndArray(); writer.Flush(); return CognitionQualityHash.Sha256(buffer.WrittenSpan); }
    private static string ComputeResponseSetDigest(IReadOnlyList<CognitionQualityPromptEnvelopeSlot> slots, IReadOnlyList<byte[]> responses)
    { ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer); writer.WriteStartArray(); for (int index = 0; index < slots.Count; index++) { writer.WriteStartObject(); writer.WriteString("scenario_id", slots[index].ScenarioId); writer.WriteString("observation_digest_sha256", slots[index].ObservationDigestSha256); writer.WriteNumber("response_byte_count", responses[index].Length); writer.WriteString("response_digest_sha256", CognitionQualityHash.Sha256(responses[index])); writer.WriteEndObject(); } writer.WriteEndArray(); writer.Flush(); return CognitionQualityHash.Sha256(buffer.WrittenSpan); }
    private static byte[] WriteEvidence(ReadOnlySpan<byte> publication, ReadOnlySpan<byte> provenance, ReadOnlySpan<byte> run, string responseSetDigest, string? payloadDigest)
    { ArrayBufferWriter<byte> buffer = new(); using Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false, SkipValidation = false }); writer.WriteStartObject(); writer.WriteString("schema_version", SchemaVersion); writer.WriteString("status", "complete"); writer.WriteString("semantics", "offline_recording_evidence_binding_only"); writer.WritePropertyName("prompt_publication"); writer.WriteRawValue(publication, skipInputValidation: false); writer.WriteString("response_set_digest_sha256", responseSetDigest); writer.WritePropertyName("provenance"); writer.WriteRawValue(provenance, skipInputValidation: false); writer.WritePropertyName("recorded_response_run"); writer.WriteRawValue(run, skipInputValidation: false); writer.WritePropertyName("claim_limitation_codes"); writer.WriteStartArray(); foreach (string limitation in ClaimLimitations) writer.WriteStringValue(limitation); writer.WriteEndArray(); if (payloadDigest is not null) writer.WriteString("recording_evidence_payload_digest_sha256", payloadDigest); writer.WriteEndObject(); writer.Flush(); return buffer.WrittenSpan.ToArray(); }
    private static string MapPromptError(string code) => code switch { CognitionQualityPromptEnvelopeErrors.ProvenancePromptRevisionInvalid => CognitionQualityRecordingEvidenceErrors.ProvenancePromptRevisionInvalid, CognitionQualityPromptEnvelopeErrors.ProvenanceProposalSchemaInvalid => CognitionQualityRecordingEvidenceErrors.ProvenanceProposalSchemaInvalid, CognitionQualityPromptEnvelopeErrors.ResponseCountInvalid => CognitionQualityRecordingEvidenceErrors.ResponseCountInvalid, CognitionQualityPromptEnvelopeErrors.ResponseSizeInvalid => CognitionQualityRecordingEvidenceErrors.ResponseSizeInvalid, CognitionQualityPromptEnvelopeErrors.ResponseAggregateSizeInvalid => CognitionQualityRecordingEvidenceErrors.ResponseAggregateSizeInvalid, _ => CognitionQualityRecordingEvidenceErrors.PromptPublicationInvalid };
}

internal static class CognitionQualityRecordingEvidenceErrors
{
    internal const string PromptPublicationInvalid = "prompt_publication_invalid"; internal const string ProvenancePromptRevisionInvalid = "provenance_prompt_revision_invalid"; internal const string ProvenanceProposalSchemaInvalid = "provenance_proposal_schema_invalid"; internal const string ResponseCountInvalid = "response_count_invalid"; internal const string ResponseSizeInvalid = "response_size_invalid"; internal const string ResponseAggregateSizeInvalid = "response_aggregate_size_invalid"; internal const string RecordedResponseRunInvalid = "recorded_response_run_invalid"; internal const string RecordingBindingIntegrityInvalid = "recording_binding_integrity_invalid"; internal const string RecordingEvidenceSizeInvalid = "recording_evidence_size_invalid";
    internal static bool IsAllowlisted(string code) => code is PromptPublicationInvalid or ProvenancePromptRevisionInvalid or ProvenanceProposalSchemaInvalid or ResponseCountInvalid or ResponseSizeInvalid or ResponseAggregateSizeInvalid or RecordedResponseRunInvalid or RecordingBindingIntegrityInvalid or RecordingEvidenceSizeInvalid;
}
