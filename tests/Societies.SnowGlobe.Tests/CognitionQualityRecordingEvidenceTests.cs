using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityRecordingEvidenceTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void LocalRecording_ProducesExactDetachedCanonicalEvidence()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityRecordingEvidence evidence = CognitionQualityRecordingEvidenceModule.Create(publication, Local(), Responses());

        Assert.Equal(CognitionQualityRecordingEvidenceModule.SchemaVersion, evidence.SchemaVersion);
        Assert.Equal("complete", evidence.Status);
        Assert.Equal("offline_recording_evidence_binding_only", evidence.Semantics);
        Assert.Equal("0c9ce26bf5f078e3cdcb85a2115f59f9a3e8d191736e8ab8e87c0c113b67e80c", evidence.ResponseSetDigestSha256);
        Assert.Equal("069aa258c0a6870aa6d8c60f14aed800cbb46923564d3b62f36a41ba3159a7fd", evidence.PayloadDigestSha256);
        Assert.Equal("61d0f7150b4b1cde5fba3f693e1a60eec6410deb83b6a371b62189f59a2115a4", evidence.CanonicalDigestSha256);
        using JsonDocument document = JsonDocument.Parse(evidence.CanonicalUtf8);
        Assert.Equal(new[] { "schema_version", "status", "semantics", "prompt_publication", "response_set_digest_sha256", "provenance", "recorded_response_run", "claim_limitation_codes", "recording_evidence_payload_digest_sha256" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(publication.CanonicalJson, document.RootElement.GetProperty("prompt_publication").GetRawText());
        Assert.Equal(evidence.RecordedResponseRun.CanonicalJson, document.RootElement.GetProperty("recorded_response_run").GetRawText());
        Assert.Equal(evidence.ResponseSetDigestSha256, Digest(ResponseSetDocument(publication, Responses())));
        Assert.Equal(evidence.CanonicalDigestSha256, Digest(evidence.CanonicalUtf8.Span));
        Assert.Equal(evidence.PayloadDigestSha256, Digest(WithoutFinalDigest(document.RootElement, "recording_evidence_payload_digest_sha256")));
        CognitionQualityNormalizedProposalEvidence normalized = CognitionQualityNormalizedProposalEvidenceCodec.CreateFromRecording(evidence);
        Assert.Equal(CognitionQualityRecordingEvidenceModule.SchemaVersion, normalized.SourceEvidenceSchemaVersion);
        Assert.Equal(evidence.CanonicalDigestSha256, normalized.SourceEvidenceDigestSha256);
        Assert.Equal(evidence.RecordedResponseRun.ProposalBatch.Select(item => item.Proposal), normalized.Proposals.Select(item => item.Proposal));
    }

    [Fact]
    public void PremiumAndMalformedInputsRemainOfflineBoundAndMalformedMapsToRunnerOutcome()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        ReadOnlyMemory<byte>[] responses = Responses();
        responses[0] = new byte[] { 0xc3, 0x28 };
        CognitionQualityRecordingEvidence local = CognitionQualityRecordingEvidenceModule.Create(publication, Local(), responses);
        CognitionQualityRecordingEvidence premium = CognitionQualityRecordingEvidenceModule.Create(publication, Premium(), Responses());
        Assert.Equal("response_utf8_invalid", local.RecordedResponseRun.ResponseBindings[0].ParseOutcome);
        Assert.NotEqual(local.CanonicalDigestSha256, premium.CanonicalDigestSha256);
        Assert.Contains("response_binding_is_caller_attested", premium.ClaimLimitationCodes);
        Assert.Contains("no_transport_delivery_attestation", premium.ClaimLimitationCodes);
    }

    [Fact]
    public void MalformedRecordedResponseProjectsCanonicalNullableNoProposalSlot()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        ReadOnlyMemory<byte>[] responses = Responses();
        responses[0] = "{bad"u8.ToArray();
        CognitionQualityRecordingEvidence recording = CognitionQualityRecordingEvidenceModule.Create(publication, Local(), responses);
        Assert.Equal("complete", recording.Status);
        Assert.Equal("response_json_invalid", recording.RecordedResponseRun.ResponseBindings[0].ParseOutcome);
        Assert.Null(recording.RecordedResponseRun.ProposalBatch[0].Proposal);

        CognitionQualityNormalizedProposalEvidence normalized = CognitionQualityNormalizedProposalEvidenceCodec.CreateFromRecording(recording);
        CognitionQualityNormalizedProposalEvidence validated = CognitionQualityNormalizedProposalEvidenceCodec.Validate(normalized.CanonicalUtf8);
        Assert.Null(validated.Proposals[0].Proposal);
        using JsonDocument document = JsonDocument.Parse(validated.CanonicalUtf8);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("proposals")[0].GetProperty("proposal").ValueKind);
        Assert.Equal(recording.CanonicalDigestSha256, validated.SourceEvidenceDigestSha256);
    }

    [Fact]
    public void ClosedFailuresCoverNullsProvenanceCountsAndBounds()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        Assert.Throws<ArgumentNullException>(() => CognitionQualityRecordingEvidenceModule.Create(null!, Local(), Responses()));
        Assert.Throws<ArgumentNullException>(() => CognitionQualityRecordingEvidenceModule.Create(publication, null!, Responses()));
        Assert.Throws<ArgumentNullException>(() => CognitionQualityRecordingEvidenceModule.Create(publication, Local(), null!));
        Assert.Equal("provenance_prompt_revision_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(publication, CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v2", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1"), Responses())).Code);
        Assert.Equal("response_count_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(publication, Local(), Responses()[..^1])).Code);
        ReadOnlyMemory<byte>[] oversized = Responses(); oversized[0] = new byte[CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes + 1];
        Assert.Equal("response_size_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(publication, Local(), oversized)).Code);
        ReadOnlyMemory<byte>[] atAggregateBound = Enumerable.Range(0, 12).Select(_ => (ReadOnlyMemory<byte>)Enumerable.Repeat((byte)'x', CognitionQualityRecordedResponseRunnerModule.MaximumResponseBytes).ToArray()).ToArray();
        Assert.Equal(CognitionQualityRecordedResponseRunnerModule.MaximumAggregateResponseBytes, CognitionQualityRecordingEvidenceModule.Create(publication, Local(), atAggregateBound).RecordedResponseRun.ResponseBindings.Sum(binding => binding.ResponseByteCount));
    }

    [Fact]
    public async Task EvidenceIsRawFreeDetachedCultureAndConcurrencyStable()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        ReadOnlyMemory<byte>[] responses = Responses();
        responses[0] = Encoding.UTF8.GetBytes("{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":1,\"x\":\"RAW_SENTINEL_DO_NOT_ECHO\"}");
        CognitionQualityRecordingEvidence evidence = CognitionQualityRecordingEvidenceModule.Create(publication, Local(), responses);
        Assert.DoesNotContain("RAW_SENTINEL_DO_NOT_ECHO", evidence.CanonicalJson, StringComparison.Ordinal);
        ReadOnlyMemory<byte> bytes = evidence.CanonicalUtf8; Assert.True(MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment)); segment.Array![segment.Offset] = 0;
        Assert.Equal((byte)'{', evidence.CanonicalUtf8.Span[0]);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Assert.Equal(evidence.CanonicalDigestSha256, CognitionQualityRecordingEvidenceModule.Create(publication, Local(), responses).CanonicalDigestSha256); }
        finally { CultureInfo.CurrentCulture = previous; }
        string[] digests = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => CognitionQualityRecordingEvidenceModule.Create(publication, Local(), responses).CanonicalDigestSha256)));
        Assert.All(digests, digest => Assert.Equal(evidence.CanonicalDigestSha256, digest));
    }

    [Fact]
    public void OneByteSensitivityAndPurePublicSurfaceHold()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        ReadOnlyMemory<byte>[] first = Responses(); ReadOnlyMemory<byte>[] changed = Responses(); changed[0] = Encoding.UTF8.GetBytes("{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":2}");
        CognitionQualityRecordingEvidence baseline = CognitionQualityRecordingEvidenceModule.Create(publication, Local(), first);
        CognitionQualityRecordingEvidence alternate = CognitionQualityRecordingEvidenceModule.Create(publication, Local(), changed);
        Assert.NotEqual(baseline.ResponseSetDigestSha256, alternate.ResponseSetDigestSha256);
        MethodInfo operation = Assert.Single(typeof(CognitionQualityRecordingEvidenceModule).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", operation.Name);
        string members = string.Join('|', new[] { typeof(CognitionQualityRecordingEvidenceModule), typeof(CognitionQualityRecordingEvidence) }.SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Select(member => member.ToString()));
        foreach (string forbidden in new[] { "Http", "Socket", "File", "Stream", "Provider", "Credential", "Payment", "Journal", "World", "Delegate", "Task", "Cancellation" }) Assert.DoesNotContain(forbidden, members, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SplitBrainPublicationAndSerializedDigestMismatchesFailBeforeResponseAccess()
    {
        CognitionQualityPromptEnvelopePublication promptV1 = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityPromptEnvelopePublication promptV2 = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v2");
        CognitionQualityPromptEnvelopePublication splitBrain = new(promptV1.CanonicalUtf8.ToArray(), promptV1.PayloadDigestSha256, promptV2.PromptRevision, promptV2.Slots, promptV2.ClaimLimitationCodes);
        Assert.Equal("prompt_publication_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(splitBrain, Local("prompt-v2"), new ThrowingResponses())).Code);

        byte[] alteredPublicationBytes = Encoding.UTF8.GetBytes(promptV1.CanonicalJson.Replace(promptV1.PayloadDigestSha256, new string('0', promptV1.PayloadDigestSha256.Length), StringComparison.Ordinal));
        CognitionQualityPromptEnvelopePublication alteredPublication = new(alteredPublicationBytes, promptV1.PayloadDigestSha256, promptV1.PromptRevision, promptV1.Slots, promptV1.ClaimLimitationCodes);
        Assert.Equal("prompt_publication_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(alteredPublication, Local(), Responses())).Code);

        CognitionQualityExecutionProvenance alteredProvenance = Local();
        FieldInfo canonical = typeof(CognitionQualityExecutionProvenance).GetField("_canonicalUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!;
        canonical.SetValue(alteredProvenance, Encoding.UTF8.GetBytes(alteredProvenance.CanonicalJson.Replace(alteredProvenance.ProvenanceDigestSha256, new string('0', alteredProvenance.ProvenanceDigestSha256.Length), StringComparison.Ordinal)));
        Assert.Equal("recording_binding_integrity_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(promptV1, alteredProvenance, Responses())).Code);

        CognitionQualityExecutionProvenance invalidLane = Premium();
        FieldInfo lane = typeof(CognitionQualityExecutionProvenance).GetField("<Lane>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        lane.SetValue(invalidLane, (CognitionLane)99);
        Assert.Equal("recording_binding_integrity_invalid", Assert.Throws<CognitionQualityRecordingEvidenceException>(() => CognitionQualityRecordingEvidenceModule.Create(promptV1, invalidLane, new ThrowingResponses())).Code);
    }

    private static CognitionQualityExecutionProvenance Local(string promptRevision = "prompt-v1") => CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, promptRevision, CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1");
    private static CognitionQualityExecutionProvenance Premium() => CognitionQualityExecutionProvenance.ForPremium(new ModelPolicySnapshot("policy-v1", "premium.example", "v1/propose", "premium-model-v1", Revision, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1", "usd", 1_000_000, 1_000_000, 10, 10, 20, 1000));
    private static ReadOnlyMemory<byte>[] Responses() => Enumerable.Range(1, 12).Select(index => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes($"{{\"agent_id\":\"agent-00\",\"action\":\"{Action(index)}\",\"quantity\":{Quantity(index)}}}")).ToArray();
    private static string Action(int index) => index switch { 1 or 7 => "GatherWood", 2 or 3 => "GatherStone", 4 or 5 or 6 => "BuildShelter", 8 or 9 => "BuildStorage", _ => "Idle" };
    private static int Quantity(int index) => index switch { 1 => 12, 2 => 6, 3 => 2, 7 => 8, _ => 0 };
    private static byte[] ResponseSetDocument(CognitionQualityPromptEnvelopePublication publication, IReadOnlyList<ReadOnlyMemory<byte>> responses)
    { using MemoryStream stream = new(); using Utf8JsonWriter writer = new(stream); writer.WriteStartArray(); for (int index = 0; index < 12; index++) { CognitionQualityPromptEnvelopeSlot slot = publication.Slots[index]; writer.WriteStartObject(); writer.WriteString("scenario_id", slot.ScenarioId); writer.WriteString("observation_digest_sha256", slot.ObservationDigestSha256); writer.WriteNumber("response_byte_count", responses[index].Length); writer.WriteString("response_digest_sha256", Digest(responses[index].Span)); writer.WriteEndObject(); } writer.WriteEndArray(); writer.Flush(); return stream.ToArray(); }
    private static byte[] WithoutFinalDigest(JsonElement root, string digestName)
    { using MemoryStream stream = new(); using Utf8JsonWriter writer = new(stream); writer.WriteStartObject(); foreach (JsonProperty property in root.EnumerateObject()) { if (property.Name == digestName) break; writer.WritePropertyName(property.Name); writer.WriteRawValue(property.Value.GetRawText()); } writer.WriteEndObject(); writer.Flush(); return stream.ToArray(); }
    private sealed class ThrowingResponses : IReadOnlyList<ReadOnlyMemory<byte>>
    { public int Count => throw new InvalidOperationException("response access"); public ReadOnlyMemory<byte> this[int index] => throw new InvalidOperationException("response access"); public IEnumerator<ReadOnlyMemory<byte>> GetEnumerator() => throw new InvalidOperationException("response access"); System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator(); }
    private static string Digest(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
