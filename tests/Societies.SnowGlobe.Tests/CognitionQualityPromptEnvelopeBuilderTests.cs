using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.SnowGlobe;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class CognitionQualityPromptEnvelopeBuilderTests
{
    private const string Revision = "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void PublicationIsCanonicalCompleteAndHasOnlyEmptyResponseSlots()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");

        Assert.Equal("complete", publication.Status);
        Assert.Equal("offline_canonical_prompt_publication_only", publication.Semantics);
        Assert.Equal(12, publication.Slots.Count);
        Assert.Equal("d879faa5af02e5b95108d7b9355a763acee1e120a1c68986c62c0e3b8907ce87", publication.PayloadDigestSha256);
        Assert.Equal("966727433db3095e804148bba18e23da368d5fbbf58e7b0e2e58de349b47e9ae", publication.CanonicalDigestSha256);
        Assert.Equal("f9baf35ff43fbd4977d050488f0bb1ebfb37bb9b1fb98ddbd2fa83384e9bbcbb", publication.PromptSetDigestSha256);
        Assert.All(publication.Slots, slot => Assert.InRange(slot.PromptByteCount, 1, CognitionQualityPromptEnvelopeBuilderModule.MaximumPromptBytes));
        Assert.InRange(publication.CanonicalUtf8.Length, 1, CognitionQualityPromptEnvelopeBuilderModule.MaximumPublicationBytes);
        using JsonDocument document = JsonDocument.Parse(publication.CanonicalUtf8);
        Assert.Equal(new[] { "schema_version", "status", "semantics", "builder_identity", "prompt_schema_version", "prompt_revision", "corpus_digest_sha256", "scoring_digest_sha256", "validator_identity", "runner_identity", "parser_identity", "proposal_schema_version", "scenario_count", "limits", "slots", "prompt_set_digest_sha256", "claim_limitation_codes", "publication_payload_digest_sha256" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.All(document.RootElement.GetProperty("slots").EnumerateArray(), slot =>
        {
            Assert.Equal(0, slot.GetProperty("response_byte_count").GetInt32());
            Assert.Equal(JsonValueKind.Null, slot.GetProperty("response_digest_sha256").ValueKind);
        });
        int payloadProperty = publication.CanonicalJson.LastIndexOf(",\"publication_payload_digest_sha256\":", StringComparison.Ordinal);
        Assert.True(payloadProperty > 0);
        Assert.Equal(publication.PayloadDigestSha256, Digest(Encoding.UTF8.GetBytes(publication.CanonicalJson[..payloadProperty] + "}")));
        Assert.Equal(publication.CanonicalDigestSha256, Digest(publication.CanonicalUtf8.Span));
    }

    [Fact]
    public void ExactRepresentativePromptIsPolicyCompleteAndOmitsHiddenCorpusData()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        string prompt = Encoding.UTF8.GetString(publication.Slots[0].PromptUtf8.Span);
        using JsonDocument document = JsonDocument.Parse(prompt);

        Assert.Equal(new[] { "schema_version", "prompt_revision", "task", "rules", "observation", "response_contract" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("prompt-v1", document.RootElement.GetProperty("prompt_revision").GetString());
        Assert.Contains("shelter then storage then Idle", prompt, StringComparison.Ordinal);
        Assert.Contains("12 wood and 6 stone", prompt, StringComparison.Ordinal);
        Assert.Contains("8 wood and 4 stone", prompt, StringComparison.Ordinal);
        Assert.Contains("cross-products", prompt, StringComparison.Ordinal);
        Assert.Contains("tie wood first", prompt, StringComparison.Ordinal);
        Assert.Contains("unavailable gather wood", prompt, StringComparison.Ordinal);
        Assert.Contains("capped by available resource and 64", prompt, StringComparison.Ordinal);
        Assert.Contains("Durability is unobserved", prompt, StringComparison.Ordinal);
        Assert.Equal("agent-00", document.RootElement.GetProperty("observation").GetProperty("agent_id").GetString());
        Assert.Equal("agent_id,action,quantity", document.RootElement.GetProperty("response_contract").GetProperty("properties").GetString());
        Assert.Equal("{\"agent_id\":\"<exact_observation_agent_id>\",\"action\":\"<legal_action_name>\",\"quantity\":<integer>}", document.RootElement.GetProperty("response_contract").GetProperty("json").GetString());

        foreach (CognitionQualityPromptEnvelopeSlot slot in publication.Slots)
        {
            string modelVisible = Encoding.UTF8.GetString(slot.PromptUtf8.Span);
            Assert.DoesNotContain(slot.ScenarioId, modelVisible, StringComparison.Ordinal);
            Assert.DoesNotContain(slot.ObservationDigestSha256, modelVisible, StringComparison.Ordinal);
            foreach (string forbidden in new[] { "category", "score", "point", "preferred", "setup", "state_digest", "event_digest", "model", "provider", "credential", "pricing" })
                Assert.DoesNotContain(forbidden, modelVisible, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RevisionChangesEveryPromptDigestAndInvalidIdentityIsClosed()
    {
        CognitionQualityPromptEnvelopePublication first = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        CognitionQualityPromptEnvelopePublication changed = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v2");
        Assert.NotEqual(first.CanonicalDigestSha256, changed.CanonicalDigestSha256);
        Assert.All(first.Slots.Zip(changed.Slots), pair => Assert.NotEqual(pair.First.PromptDigestSha256, pair.Second.PromptDigestSha256));

        foreach (string value in new[] { "", "Prompt-v1", "prompt v1", new string('a', 129) })
            Assert.Equal("prompt_revision_invalid", Assert.Throws<CognitionQualityPromptEnvelopeException>(() => CognitionQualityPromptEnvelopeBuilderModule.Create(value)).Code);
    }

    [Fact]
    public void RecordedResponseBindingIsDetachedAndRunsThroughExistingRunner()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        byte[][] source = Enumerable.Range(1, 12).Select(index => Encoding.UTF8.GetBytes(ResponseJson(index))).ToArray();
        IReadOnlyList<CognitionQualityRecordedResponseFixture> fixtures = publication.BindRecordedResponses(Provenance(), source.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
        source[0][0] = (byte)'!';
        CognitionQualityRecordedResponseRun run = CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), fixtures);

        Assert.Equal("f03577c7d6d34f18c8a6c25c61bb3f1ac8f5d0a90ab3c1c208745fd11cb61ffd", run.CanonicalDigestSha256);
        Assert.Equal(1200, run.ExecutionEvidence.Score.RawPoints);
        Assert.All(run.ProposalBatch, submission => Assert.NotNull(submission.Proposal));
        Assert.Equal("response_utf8_invalid", CognitionQualityRecordedResponseRunnerModule.Run(Provenance(), publication.BindRecordedResponses(Provenance(), Replace(source, 0, new byte[] { 0xc3, 0x28 }))).ResponseBindings[0].ParseOutcome);
    }

    [Fact]
    public void BindingRejectsMismatchedProvenanceAndClosedBounds()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        ReadOnlyMemory<byte>[] responses = Enumerable.Range(1, 12).Select(index => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(ResponseJson(index))).ToArray();
        Assert.Equal("provenance_prompt_revision_invalid", Assert.Throws<CognitionQualityPromptEnvelopeException>(() => publication.BindRecordedResponses(CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v2", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1"), responses)).Code);
        Assert.Equal("provenance_proposal_schema_invalid", Assert.Throws<CognitionQualityPromptEnvelopeException>(() => publication.BindRecordedResponses(CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", "proposal-v1", "adapter-v1"), responses)).Code);
        Assert.Equal("response_count_invalid", Assert.Throws<CognitionQualityPromptEnvelopeException>(() => publication.BindRecordedResponses(Provenance(), responses[..^1])).Code);
        ReadOnlyMemory<byte>[] empty = responses.ToArray(); empty[0] = Array.Empty<byte>();
        Assert.Equal("response_size_invalid", Assert.Throws<CognitionQualityPromptEnvelopeException>(() => publication.BindRecordedResponses(Provenance(), empty)).Code);
        ReadOnlyMemory<byte>[] longResponse = responses.ToArray(); longResponse[0] = new byte[1025];
        Assert.Equal("response_size_invalid", Assert.Throws<CognitionQualityPromptEnvelopeException>(() => publication.BindRecordedResponses(Provenance(), longResponse)).Code);
        ReadOnlyMemory<byte>[] exactAggregate = Enumerable.Range(0, 12).Select(_ => (ReadOnlyMemory<byte>)new byte[1024]).ToArray();
        Assert.Equal(12, publication.BindRecordedResponses(Provenance(), exactAggregate).Count);
    }

    [Fact]
    public async Task OutputsAreDetachedCultureIndependentConcurrentAndHaveNoAuthoritySurface()
    {
        CognitionQualityPromptEnvelopePublication publication = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1");
        ReadOnlyMemory<byte> prompt = publication.Slots[0].PromptUtf8;
        Assert.True(MemoryMarshal.TryGetArray(prompt, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = (byte)'!';
        Assert.Equal((byte)'{', publication.Slots[0].PromptUtf8.Span[0]);
        ReadOnlyMemory<byte> canonical = publication.CanonicalUtf8;
        Assert.True(MemoryMarshal.TryGetArray(canonical, out ArraySegment<byte> canonicalSegment));
        canonicalSegment.Array![canonicalSegment.Offset] = 0;
        Assert.Equal((byte)'{', publication.CanonicalUtf8.Span[0]);

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(publication.CanonicalDigestSha256, CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1").CanonicalDigestSha256);
        }
        finally { CultureInfo.CurrentCulture = previous; }
        string[] digests = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(() => CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1").CanonicalDigestSha256)));
        Assert.All(digests, digest => Assert.Equal(publication.CanonicalDigestSha256, digest));

        MethodInfo operation = Assert.Single(typeof(CognitionQualityPromptEnvelopeBuilderModule).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Create", operation.Name);
        string members = string.Join('|', new[] { typeof(CognitionQualityPromptEnvelopeBuilderModule), typeof(CognitionQualityPromptEnvelopePublication), typeof(CognitionQualityPromptEnvelopeSlot) }
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Select(member => member.ToString()));
        foreach (string forbidden in new[] { "Http", "Socket", "File", "Stream", "Provider", "Credential", "Payment", "Journal", "Task", "Delegate", "World", "Commit" })
            Assert.DoesNotContain(forbidden, members, StringComparison.OrdinalIgnoreCase);
    }

    private static CognitionQualityExecutionProvenance Provenance() => CognitionQualityExecutionProvenance.ForLocal("model-v1", Revision, PolicyDigest, "prompt-v1", CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, "adapter-v1");
    private static ReadOnlyMemory<byte>[] Replace(byte[][] values, int index, byte[] replacement)
    {
        ReadOnlyMemory<byte>[] result = values.Select(value => (ReadOnlyMemory<byte>)value).ToArray();
        result[index] = replacement;
        return result;
    }
    private static string ResponseJson(int index)
    {
        (string action, int quantity) = index switch
        {
            1 => ("GatherWood", 12),
            2 => ("GatherStone", 6),
            3 => ("GatherStone", 2),
            4 or 5 or 6 => ("BuildShelter", 0),
            7 => ("GatherWood", 8),
            8 or 9 => ("BuildStorage", 0),
            _ => ("Idle", 0)
        };
        return $"{{\"agent_id\":\"agent-00\",\"action\":\"{action}\",\"quantity\":{quantity}}}";
    }
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
