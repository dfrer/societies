using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OpenRouterPremiumHttpExchangeTests
{
    [Fact]
    public async Task HttpAdapterEmitsOneExactNonStreamingClosedRouteRequestWithoutAmbientFeatures()
    {
        CapturingHandler handler = new(SuccessBody());
        using OpenRouterPremiumHttpExchange exchange = OpenRouterPremiumHttpExchange.CreateForOfflineTests(handler);
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        CognitionQualityPromptEnvelopeSlot slot = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1").Slots[0];
        using OpenRouterPremiumExchangeRequest request = OpenRouterPremiumExchangeRequest.CreateForProfile(profile, slot, new string('b', 64));
        byte[] lease = Encoding.ASCII.GetBytes("offline-invalid-fixture-token");

        using OpenRouterPremiumExchangeResponse response = await exchange.ExchangeOnceAsync(request, lease, CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(OpenRouterPremiumProfile.EffectiveUri, handler.Uri!.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("enabled", handler.RouterMetadata);
        Assert.Equal("application/json", handler.ContentType);
        Assert.False(exchange.RedirectsAllowed);
        Assert.False(exchange.AutomaticRetriesAllowed);
        Assert.False(exchange.ProxyAllowed);
        Assert.False(exchange.CookiesAllowed);
        Assert.False(exchange.AmbientAuthenticationAllowed);
        Assert.False(exchange.AutomaticDecompressionAllowed);
        Assert.Equal(1, exchange.SerializationCount);
        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement root = body.RootElement;
        Assert.Equal(OpenRouterPremiumProfile.CanonicalModelSlug, root.GetProperty("model").GetString());
        Assert.Equal(handler.Body!.Length, request.CanonicalRequestByteCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(handler.Body)).ToLowerInvariant(), request.RequestDigestSha256);
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(512, root.GetProperty("max_completion_tokens").GetInt32());
        JsonElement reasoning = root.GetProperty("reasoning");
        Assert.Equal("minimal", reasoning.GetProperty("effort").GetString());
        Assert.True(reasoning.GetProperty("exclude").GetBoolean());
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
        Assert.Equal("json_schema", root.GetProperty("response_format").GetProperty("type").GetString());
        Assert.True(root.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
        JsonElement provider = root.GetProperty("provider");
        Assert.False(provider.GetProperty("allow_fallbacks").GetBoolean());
        Assert.True(provider.GetProperty("require_parameters").GetBoolean());
        Assert.Equal("deny", provider.GetProperty("data_collection").GetString());
        Assert.True(provider.GetProperty("zdr").GetBoolean());
        Assert.Equal(new[] { "azure" }, provider.GetProperty("order").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "azure" }, provider.GetProperty("only").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("price", provider.GetProperty("sort").GetString());
        JsonElement maxPrice = provider.GetProperty("max_price");
        Assert.Equal(0.2m, maxPrice.GetProperty("prompt").GetDecimal());
        Assert.Equal(1.2m, maxPrice.GetProperty("completion").GetDecimal());
    }

    [Theory]
    [InlineData("{\"model\":\"openai/gpt-5.6-luna\",\"model\":\"openai/gpt-5.6-luna\"}", "response_json_duplicate_property")]
    [InlineData("{\"model\":\"openai/gpt-5.6-luna\",\"unknown\":true}", "response_root_unknown_property")]
    [InlineData("[[[[[[[[[0]]]]]]]]]", "response_json_too_deep")]
    [InlineData("{\"cost\":1e9999}", "response_number_invalid")]
    public void StrictParserRejectsMalformedClosedShapes(string json, string expected)
    {
        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected, "cq1", "request-digest"));
        Assert.Equal(expected, exception.Code);
        Assert.Equal(expected, exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void StrictParserRejectsResponseBeyondConfiguredJsonTokenLimitWithoutEcho()
    {
        const string dynamicNamePrefix = "json-token-limit-sentinel-";
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        int propertyCount = profile.Bounds.MaximumJsonTokens / 2 + 1;
        StringBuilder body = new("{");
        for (int index = 0; index < propertyCount; index++)
        {
            if (index > 0) body.Append(',');
            body.Append('"').Append(dynamicNamePrefix)
                .Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append("\":0");
        }
        body.Append('}');
        byte[] utf8 = Encoding.UTF8.GetBytes(body.ToString());

        Assert.True(2 + propertyCount * 2 > profile.Bounds.MaximumJsonTokens);
        Assert.True(utf8.Length < profile.Bounds.MaximumResponseBytes);
        Assert.True(profile.Bounds.MaximumJsonDepth >= 1);
        Assert.True(dynamicNamePrefix.Length + propertyCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture).Length <= 128);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                utf8, 200, profile, "cq1", new string('d', 64)));

        Assert.Equal("response_json_token_limit", exception.Code);
        Assert.Equal("response_json_token_limit", exception.Message);
        Assert.DoesNotContain(dynamicNamePrefix, exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [MemberData(nameof(StrictResponseUnknownPropertyCases))]
    public void StrictResponseObjectAllowlistScopesEmitUniqueRawFreeDiagnostics(
        string scope,
        string json,
        int statusCode,
        string expectedCode)
    {
        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(json), statusCode, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64)));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedCode, exception.Message);
        Assert.Equal("provider_response_rejected_" + expectedCode,
            OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(exception));
        Assert.Contains(scope, expectedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-provider-sentinel-must-not-leak", exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void ProposalUnknownPropertyDiagnosticRemainsUnchanged()
    {
        string body = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\\\"quantity\\\":12}",
            "\\\"quantity\\\":12,\\\"raw-provider-sentinel-must-not-leak\\\":0}",
            StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(body), 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64)));

        Assert.Equal("proposal_unknown_property", exception.Code);
        Assert.Equal("proposal_unknown_property", exception.Message);
        Assert.Equal("provider_response_rejected_proposal_unknown_property",
            OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(exception));
        Assert.DoesNotContain("raw-provider-sentinel-must-not-leak", exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [InlineData("\"index\":0", "\"index\":1", "response_choice_index_invalid")]
    [InlineData(",\"finish_reason\":\"stop\"", "", "response_finish_reason_missing")]
    [InlineData("\"finish_reason\":\"stop\"", "\"finish_reason\":42", "response_finish_reason_type_invalid")]
    [InlineData("\"finish_reason\":\"stop\"", "\"finish_reason\":\"raw-provider-sentinel-must-not-leak\"", "response_finish_reason_not_stop")]
    [InlineData("\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"error\":{\"message\":\"raw-provider-sentinel-must-not-leak\"}", "response_choice_error_present")]
    [InlineData("\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"native_finish_reason\":42", "response_native_finish_reason_type_invalid")]
    [InlineData("\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"native_finish_reason\":null", "response_native_finish_reason_type_invalid")]
    [InlineData("\"finish_reason\":\"stop\"", "\"finish_reason\":\"stop\",\"logprobs\":{\"marker\":\"raw-provider-sentinel-must-not-leak\"}", "response_logprobs_non_null")]
    [InlineData("\"message\":{\"role\":\"assistant\"", "\"message\":{\"role\":\"assistant\",\"refusal\":\"raw-provider-sentinel-must-not-leak\"", "response_refusal_non_null")]
    public void StrictParserDistinguishesEachClosedFinishAdmissionFailureWithoutProviderValues(
        string exact,
        string mutation,
        string expectedCode)
    {
        string body = Encoding.UTF8.GetString(SuccessBody());
        Assert.Contains(exact, body, StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(body.Replace(exact, mutation, StringComparison.Ordinal)),
                200,
                OpenRouterPremiumProfileRegistry.Selected,
                "cq1",
                new string('d', 64)));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedCode, exception.Message);
        Assert.Equal("provider_response_rejected_" + expectedCode,
            OpenRouterPremiumResponseParser.ToRejectedOutcomeCode(exception));
        Assert.DoesNotContain("raw-provider-sentinel-must-not-leak", exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void StrictParserAcceptsBoundedNonAuthoritativeNativeFinishMetadataWhenNormalizedFinishIsStop()
    {
        string body = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"finish_reason\":\"stop\"",
            "\"finish_reason\":\"stop\",\"native_finish_reason\":\"bounded-provider-metadata\"",
            StringComparison.Ordinal);

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(
            Encoding.UTF8.GetBytes(body), 200, OpenRouterPremiumProfileRegistry.Selected,
            "cq1", new string('d', 64));

        Assert.Equal("premium_evidence_success", receipt.OutcomeCode);
        Assert.Equal(SubmissionState.ResponseReceived, receipt.SubmissionState);
        Assert.Equal(ChargeState.Settled, receipt.ChargeState);
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 12), receipt.Proposal);
        Assert.DoesNotContain("bounded-provider-metadata", receipt.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StrictParserRejectsOversizedNativeFinishMetadataWithoutEcho()
    {
        string marker = new('x', OpenRouterPremiumProfileRegistry.Selected.Bounds.MaximumStringCharacters + 1);
        string body = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"finish_reason\":\"stop\"",
            $"\"finish_reason\":\"stop\",\"native_finish_reason\":\"{marker}\"",
            StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(body), 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64)));

        Assert.Equal("response_string_too_long", exception.Code);
        Assert.DoesNotContain(marker, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StrictParserRejectsInvalidUtf8AndOversizeWithoutEcho()
    {
        byte[] invalid = [0xC3, 0x28];
        OpenRouterPremiumEvidenceException utf8 = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(invalid, 200, OpenRouterPremiumProfileRegistry.Selected, "cq1", "request-digest"));
        Assert.Equal("response_utf8_invalid", utf8.Code);
        byte[] oversized = new byte[OpenRouterPremiumProfileRegistry.Selected.Bounds.MaximumResponseBytes + 1];
        Assert.Equal("response_too_large", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(oversized, 200, OpenRouterPremiumProfileRegistry.Selected, "cq1", "request-digest")).Code);
    }

    [Fact]
    public async Task MalformedCredentialZeroesCanonicalRequestCopyWithoutSendingOrEchoing()
    {
        bool requestCopyZeroed = false;
        CapturingHandler handler = new(SuccessBody());
        using OpenRouterPremiumHttpExchange exchange = OpenRouterPremiumHttpExchange.CreateForOfflineTests(
            handler, zeroed => requestCopyZeroed = zeroed);
        using OpenRouterPremiumExchangeRequest request = Request();
        byte[] malformed = Encoding.ASCII.GetBytes("fixture\nsecret");

        OpenRouterPremiumEvidenceException exception = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            exchange.ExchangeOnceAsync(request, malformed, CancellationToken.None).AsTask());

        Assert.Equal("credential_invalid", exception.Code);
        Assert.Equal("credential_invalid", exception.Message);
        Assert.DoesNotContain("fixture", exception.ToString(), StringComparison.Ordinal);
        Assert.True(requestCopyZeroed);
        FieldInfo canonicalField = typeof(OpenRouterPremiumExchangeRequest).GetField("_canonicalRequestUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.All((byte[])canonicalField.GetValue(request)!, value => Assert.Equal(0, value));
        FieldInfo promptField = typeof(OpenRouterPremiumExchangeRequest).GetField("_promptUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.All((byte[])promptField.GetValue(request)!, value => Assert.Equal(0, value));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void PromptTokenDetailsEnvelope_AcceptsUnknownBoundedIntegerAsNonAuthoritative()
    {
        const string dynamicName = "future_cache_metric_must_not_leak";
        string json = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"cost\":0.000044",
            $"\"cost\":0.000044,\"prompt_tokens_details\":{{\"cached_tokens\":10,\"{dynamicName}\":7}}",
            StringComparison.Ordinal);

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(
            Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected,
            "cq1", new string('d', 64));

        Assert.Equal("premium_evidence_success", receipt.OutcomeCode);
        Assert.Equal(100, receipt.PromptTokens);
        Assert.Equal(20, receipt.CompletionTokens);
        Assert.Equal(120, receipt.TotalTokens);
        Assert.Equal(44, receipt.SettledMicrousd);
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 12), receipt.Proposal);
        Assert.DoesNotContain(dynamicName, receipt.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"cached_tokens\":null}")]
    [InlineData("{\"cached_tokens\":0.5}")]
    [InlineData("{\"cached_tokens\":\"1\"}")]
    [InlineData("{\"cached_tokens\":-1}")]
    [InlineData("{\"cached_tokens\":4097}")]
    public void PromptTokenDetailsEnvelope_RejectsMalformedOrOutOfRangeValues(string details)
    {
        string json = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"cost\":0.000044", $"\"cost\":0.000044,\"prompt_tokens_details\":{details}", StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64)));

        Assert.Equal("response_usage_invalid", exception.Code);
        Assert.Equal("response_usage_invalid", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void PromptTokenDetailsEnvelope_DoesNotRelaxCompletionDetailsUnknownFields()
    {
        const string dynamicName = "future_completion_metric_must_not_leak";
        string json = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"cost\":0.000044",
            $"\"cost\":0.000044,\"completion_tokens_details\":{{\"{dynamicName}\":0}}",
            StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64)));

        Assert.Equal("response_usage_completion_tokens_details_unknown_property", exception.Code);
        Assert.Equal("response_usage_completion_tokens_details_unknown_property", exception.Message);
        Assert.DoesNotContain(dynamicName, exception.ToString(), StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void StrictParserAcceptsDocumentedBoundedUsageAdditionsWithoutChangingSettledCost()
    {
        string json = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"cost\":0.000044",
            "\"cost\":0.000044,\"is_byok\":false," +
            "\"server_tool_use_details\":{\"tool_calls_executed\":0,\"tool_calls_requested\":0}," +
            "\"cost_details\":{\"upstream_inference_cost\":0.000040," +
            "\"upstream_inference_prompt_cost\":0.000010,\"upstream_inference_completions_cost\":0.000020}",
            StringComparison.Ordinal);

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(
            Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected,
            "cq1", new string('d', 64));

        Assert.Equal("premium_evidence_success", receipt.OutcomeCode);
        Assert.Equal(44, receipt.SettledMicrousd);
        Assert.Equal(100, receipt.PromptTokens);
        Assert.Equal(20, receipt.CompletionTokens);
    }

    [Fact]
    public void StrictParserAcceptsDocumentedNullableUpstreamCostDetails()
    {
        string json = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"cost\":0.000044",
            "\"cost\":0.000044,\"cost_details\":{\"upstream_inference_cost\":null," +
            "\"upstream_inference_prompt_cost\":null,\"upstream_inference_completions_cost\":null}",
            StringComparison.Ordinal);

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(
            Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected,
            "cq1", new string('d', 64));

        Assert.Equal("premium_evidence_success", receipt.OutcomeCode);
        Assert.Equal(44, receipt.SettledMicrousd);
    }

    [Theory]
    [InlineData("\"is_byok\":true", "response_usage_invalid")]
    [InlineData("\"is_byok\":null", "response_usage_invalid")]
    [InlineData("\"server_tool_use_details\":[]", "response_usage_invalid")]
    [InlineData("\"server_tool_use_details\":{\"tool_calls_executed\":1}", "response_usage_invalid")]
    [InlineData("\"server_tool_use_details\":{\"tool_calls_requested\":-1}", "response_usage_invalid")]
    [InlineData("\"server_tool_use_details\":{\"tool_calls_executed\":\"0\"}", "response_usage_invalid")]
    [InlineData("\"server_tool_use_details\":{\"unknown\":0}", "response_usage_server_tool_use_details_unknown_property")]
    [InlineData("\"cost_details\":[]", "response_usage_invalid")]
    [InlineData("\"cost_details\":{\"unknown\":0}", "response_usage_cost_details_unknown_property")]
    [InlineData("\"cost_details\":{\"upstream_inference_cost\":\"0\"}", "response_usage_invalid")]
    [InlineData("\"cost_details\":{\"upstream_inference_prompt_cost\":-0.000001}", "response_usage_invalid")]
    [InlineData("\"cost_details\":{\"upstream_inference_completions_cost\":0.000045}", "response_usage_invalid")]
    [InlineData("\"cost_details\":{\"upstream_inference_prompt_cost\":0.000030,\"upstream_inference_completions_cost\":0.000030}", "response_usage_invalid")]
    [InlineData("\"cost_details\":{\"upstream_inference_cost\":0.000020,\"upstream_inference_prompt_cost\":0.000030}", "response_usage_invalid")]
    public void StrictParserRejectsUnsafeDocumentedUsageAdditions(string addition, string expectedCode)
    {
        string json = Encoding.UTF8.GetString(SuccessBody()).Replace(
            "\"cost\":0.000044", $"\"cost\":0.000044,{addition}", StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(
                Encoding.UTF8.GetBytes(json), 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64)));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [InlineData(400, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(401, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(402, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(403, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(404, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(413, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(422, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(408, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(429, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(500, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(502, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    [InlineData(503, SubmissionState.SubmissionUnknown, ChargeState.Unknown)]
    public void HttpStatusClassifiesSubmissionAndChargeSeparately(int status, SubmissionState submission, ChargeState charge)
    {
        byte[] body = Encoding.UTF8.GetBytes($"{{\"error\":{{\"code\":{status},\"message\":\"closed\"}}}}");
        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(body, status,
            OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64));

        Assert.Equal(submission, receipt.SubmissionState);
        Assert.Equal(charge, receipt.ChargeState);
        Assert.Equal(0, receipt.SettledMicrousd);
        Assert.Null(receipt.Proposal);
    }

    [Theory]
    [InlineData(400, false)]
    [InlineData(400, true)]
    [InlineData(401, true)]
    [InlineData(402, true)]
    [InlineData(403, true)]
    [InlineData(404, true)]
    [InlineData(413, true)]
    [InlineData(422, true)]
    public void ProviderAttributedHttpFailuresRemainUnknownAfterDispatchMarker(int status, bool providerAttributed)
    {
        string metadata = providerAttributed ? ",\"metadata\":{\"provider_name\":\"Azure\",\"raw\":\"closed\"}" : string.Empty;
        byte[] body = Encoding.UTF8.GetBytes($"{{\"error\":{{\"code\":{status},\"message\":\"closed\"{metadata}}}}}");

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(body, status,
            OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64));

        Assert.Equal(SubmissionState.SubmissionUnknown, receipt.SubmissionState);
        Assert.Equal(ChargeState.Unknown, receipt.ChargeState);
    }

    [Fact]
    public void SuccessRequiresExactRoutingUsageAndTheSharedProposalContract()
    {
        byte[] response = SuccessBody();
        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(response, 200,
            OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64));
        Assert.Equal(SubmissionState.ResponseReceived, receipt.SubmissionState);
        Assert.Equal(ChargeState.Settled, receipt.ChargeState);
        Assert.Equal(44, receipt.SettledMicrousd);
        Assert.Equal(120, receipt.TotalTokens);
        Assert.Equal(new SnowGlobeActionProposal("agent-00", SnowGlobeActionKind.GatherWood, 12), receipt.Proposal);
        using JsonDocument document = JsonDocument.Parse(response);
        byte[] content = Encoding.UTF8.GetBytes(document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!);
        CognitionQualityProposalResponseParseResult shared = CognitionQualityProposalResponseContract.Parse(content);
        Assert.Equal(CognitionQualityRecordedResponseRunnerModule.ProposalSchemaVersion, CognitionQualityProposalResponseContract.SchemaVersion);
        Assert.Equal("proposal_parsed", shared.Outcome);
        Assert.Equal(receipt.Proposal, shared.Proposal);
    }

    [Fact]
    public void AdditiveRouterMetadataAndMultipleCandidatesPreserveExactSelectedRoute()
    {
        string body = Encoding.UTF8.GetString(SuccessBody());
        body = body.Replace(
            "\"is_byok\":false,\"endpoints\":{\"total\":1,\"available\":[{\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"selected\":true}]}",
            "\"is_byok\":false,\"params\":{\"quality_floor\":0.5},\"future_router_field\":{\"opaque\":true},"
            + "\"endpoints\":{\"total\":2,\"available\":[{\"provider\":\"Other\",\"model\":\"other/model\",\"selected\":false},"
            + "{\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"selected\":true}]}",
            StringComparison.Ordinal);

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(Encoding.UTF8.GetBytes(body), 200,
            OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64));

        Assert.Equal(SubmissionState.ResponseReceived, receipt.SubmissionState);
        Assert.Equal(ChargeState.Settled, receipt.ChargeState);
    }

    [Fact]
    public void DocumentedReasoningEnvelopeAndOptionalRouterArrays_AreAccepted()
    {
        string body = DocumentedReasoningBody();

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(Encoding.UTF8.GetBytes(body), 200,
            OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64));

        Assert.Equal(SubmissionState.ResponseReceived, receipt.SubmissionState);
        Assert.Equal(ChargeState.Settled, receipt.ChargeState);
        Assert.Equal(44, receipt.SettledMicrousd);
    }

    [Theory]
    [InlineData("\"provider\":\"Azure\"", "\"provider\":\"Other\"", "response_binding_invalid")]
    [InlineData("\"logprobs\":null", "\"logprobs\":{}", "response_logprobs_non_null")]
    [InlineData("\"format\":\"azure-openai-responses-v1\"", "\"format\":\"future\"", "response_reasoning_invalid")]
    [InlineData("\"index\":0}]", "\"index\":1}]", "response_reasoning_invalid")]
    public void DocumentedReasoningEnvelopeStillFailsClosedOnBindingAndShapeMutations(
        string exact, string mutation, string expectedCode)
    {
        string body = DocumentedReasoningBody();
        Assert.Contains(exact, body, StringComparison.Ordinal);

        OpenRouterPremiumEvidenceException exception = Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(Encoding.UTF8.GetBytes(body.Replace(exact, mutation, StringComparison.Ordinal)), 200,
                OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64)));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void DocumentedReasoningEnvelopeAlsoTreatsBoundedNativeFinishMetadataAsNonAuthoritative()
    {
        string body = DocumentedReasoningBody().Replace(
            "\"native_finish_reason\":\"stop\"", "\"native_finish_reason\":\"bounded-provider-metadata\"",
            StringComparison.Ordinal);

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(
            Encoding.UTF8.GetBytes(body), 200, OpenRouterPremiumProfileRegistry.Selected,
            "cq1", new string('d', 64));

        Assert.Equal("premium_evidence_success", receipt.OutcomeCode);
        Assert.Equal(44, receipt.SettledMicrousd);
    }

    [Fact]
    public void NonEmptyRouterPipelineRemainsForbidden()
    {
        string body = Encoding.UTF8.GetString(SuccessBody()).Replace("\"pipeline\":[]",
            "\"pipeline\":[{\"type\":\"guardrail\"}]", StringComparison.Ordinal);

        Assert.Equal("response_pipeline_forbidden", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(Encoding.UTF8.GetBytes(body), 200,
                OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64))).Code);
    }

    [Fact]
    public void ErrorEnvelopeAcceptsAdditiveTopLevelRouterMetadataWithoutRetryAuthority()
    {
        byte[] body = Encoding.UTF8.GetBytes(
            "{\"error\":{\"code\":404,\"message\":\"No allowed providers\"},\"openrouter_metadata\":{"
            + "\"requested\":\"openai/gpt-5.6-luna\",\"strategy\":\"direct\",\"attempt\":0,"
            + "\"params\":{\"max_price\":true},\"future_router_field\":{\"opaque\":true},"
            + "\"endpoints\":{\"total\":1,\"available\":[{\"provider\":\"OpenAI\","
            + "\"model\":\"openai/gpt-5.6-luna\",\"selected\":false}]}}}");

        OpenRouterPremiumSlotReceipt receipt = OpenRouterPremiumResponseParser.Parse(body, 404,
            OpenRouterPremiumProfileRegistry.Selected, "cq1", new string('d', 64));

        Assert.Equal(SubmissionState.SubmissionUnknown, receipt.SubmissionState);
        Assert.Equal(ChargeState.Unknown, receipt.ChargeState);
    }

    [Fact]
    public void DatedWebRevisionPathIsRejectedAtResponseAndRoutingBindings()
    {
        byte[] aliased = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(SuccessBody()).Replace(
            OpenRouterPremiumProfile.CanonicalModelSlug, OpenRouterPremiumProfile.ModelReleaseRevisionPathIdentity, StringComparison.Ordinal));

        Assert.Equal("response_binding_invalid", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(aliased, 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64))).Code);
    }

    [Theory]
    [InlineData("\"requested\":\"openai/gpt-5.6-luna\"", "\"requested\":\"openai/gpt-5.6-luna-20260709\"")]
    [InlineData("\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"selected\":true", "\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna-20260709\",\"selected\":true")]
    [InlineData("\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"status\":200", "\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna-20260709\",\"status\":200")]
    public void DatedWebRevisionPathIsRejectedInEachRouterEvidenceBinding(string exact, string mutation)
    {
        string body = Encoding.UTF8.GetString(SuccessBody());
        Assert.Contains(exact, body, StringComparison.Ordinal);
        byte[] mutated = Encoding.UTF8.GetBytes(body.Replace(exact, mutation, StringComparison.Ordinal));

        Assert.Equal("response_binding_invalid", Assert.Throws<OpenRouterPremiumEvidenceException>(() =>
            OpenRouterPremiumResponseParser.Parse(mutated, 200, OpenRouterPremiumProfileRegistry.Selected,
                "cq1", new string('d', 64))).Code);
    }

    [Fact]
    public async Task TamperedCanonicalSerializedRequestIsRejectedByBothBuiltInAdapters()
    {
        using OpenRouterPremiumExchangeRequest request = Request();
        FieldInfo field = typeof(OpenRouterPremiumExchangeRequest).GetField("_canonicalRequestUtf8", BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[] bytes = (byte[])field.GetValue(request)!;
        bytes[^2] ^= 1;
        byte[] lease = Encoding.ASCII.GetBytes("offline-invalid-fixture-token");
        CapturingHandler handler = new(SuccessBody());
        using OpenRouterPremiumHttpExchange http = OpenRouterPremiumHttpExchange.CreateForOfflineTests(handler);
        ScriptedOpenRouterPremiumExchange scripted = ScriptedOpenRouterPremiumExchange.CreateSuccessful();

        Assert.Equal("exchange_request_binding_invalid", (await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            http.ExchangeOnceAsync(request, lease, CancellationToken.None).AsTask())).Code);
        Assert.Equal("exchange_request_binding_invalid", (await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
            scripted.ExchangeOnceAsync(request, lease, CancellationToken.None).AsTask())).Code);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(0, scripted.CallCount);
    }

    [Fact]
    public async Task HttpAdapterDoesNotFollowRedirectOrRetryTerminalStatus()
    {
        CapturingHandler redirect = new(Encoding.UTF8.GetBytes("{\"error\":{\"code\":307,\"message\":\"redirect\"}}"), HttpStatusCode.TemporaryRedirect, new Uri("https://attacker.invalid/redirect"));
        using (OpenRouterPremiumHttpExchange exchange = OpenRouterPremiumHttpExchange.CreateForOfflineTests(redirect))
        {
            using OpenRouterPremiumExchangeRequest request = Request();
            OpenRouterPremiumEvidenceException exception = await Assert.ThrowsAsync<OpenRouterPremiumEvidenceException>(() =>
                exchange.ExchangeOnceAsync(request, Encoding.ASCII.GetBytes("offline-invalid-fixture-token"), CancellationToken.None).AsTask());
            Assert.Equal("effective_uri_mismatch", exception.Code);
            Assert.Equal(1, redirect.CallCount);
        }

        CapturingHandler terminal = new(Encoding.UTF8.GetBytes("{\"error\":{\"code\":503,\"message\":\"closed\"}}"), HttpStatusCode.ServiceUnavailable);
        using (OpenRouterPremiumHttpExchange exchange = OpenRouterPremiumHttpExchange.CreateForOfflineTests(terminal))
        using (OpenRouterPremiumExchangeRequest request = Request())
        using (OpenRouterPremiumExchangeResponse response = await exchange.ExchangeOnceAsync(request, Encoding.ASCII.GetBytes("offline-invalid-fixture-token"), CancellationToken.None))
        {
            Assert.Equal(503, response.StatusCode);
            Assert.Equal(1, terminal.CallCount);
            Assert.Equal(1, exchange.SerializationCount);
        }
    }

    private static OpenRouterPremiumExchangeRequest Request()
    {
        OpenRouterPremiumProfile profile = OpenRouterPremiumProfileRegistry.Selected;
        CognitionQualityPromptEnvelopeSlot slot = CognitionQualityPromptEnvelopeBuilderModule.Create("prompt-v1").Slots[0];
        return OpenRouterPremiumExchangeRequest.CreateForProfile(profile, slot, new string('b', 64));
    }

    public static IEnumerable<object[]> StrictResponseUnknownPropertyCases()
    {
        const string sentinel = "raw-provider-sentinel-must-not-leak";
        string success = Encoding.UTF8.GetString(SuccessBody());
        string reasoning = DocumentedReasoningBody();

        yield return ["root", success.Replace(
            "{\"id\"", $"{{\"{sentinel}\":0,\"id\"", StringComparison.Ordinal),
            200, "response_root_unknown_property"];
        yield return ["choice", success.Replace(
            "\"choices\":[{\"index\"", $"\"choices\":[{{\"{sentinel}\":0,\"index\"", StringComparison.Ordinal),
            200, "response_choice_unknown_property"];
        yield return ["message", success.Replace(
            "\"message\":{\"role\"", $"\"message\":{{\"{sentinel}\":0,\"role\"", StringComparison.Ordinal),
            200, "response_message_unknown_property"];
        yield return ["reasoning_detail", reasoning.Replace(
            "\"reasoning_details\":[{\"type\"", $"\"reasoning_details\":[{{\"{sentinel}\":0,\"type\"", StringComparison.Ordinal),
            200, "response_reasoning_detail_unknown_property"];
        yield return ["usage", success.Replace(
            "\"usage\":{\"prompt_tokens\"", $"\"usage\":{{\"{sentinel}\":0,\"prompt_tokens\"", StringComparison.Ordinal),
            200, "response_usage_unknown_property"];
        yield return ["usage_completion_tokens_details", success.Replace(
            "\"cost\":0.000044", $"\"cost\":0.000044,\"completion_tokens_details\":{{\"{sentinel}\":0}}", StringComparison.Ordinal),
            200, "response_usage_completion_tokens_details_unknown_property"];
        yield return ["usage_server_tool_use_details", success.Replace(
            "\"cost\":0.000044", $"\"cost\":0.000044,\"server_tool_use_details\":{{\"{sentinel}\":0}}", StringComparison.Ordinal),
            200, "response_usage_server_tool_use_details_unknown_property"];
        yield return ["usage_cost_details", success.Replace(
            "\"cost\":0.000044", $"\"cost\":0.000044,\"cost_details\":{{\"{sentinel}\":0}}", StringComparison.Ordinal),
            200, "response_usage_cost_details_unknown_property"];
        yield return ["routing_endpoints", success.Replace(
            "\"endpoints\":{\"total\"", $"\"endpoints\":{{\"{sentinel}\":0,\"total\"", StringComparison.Ordinal),
            200, "response_routing_endpoints_unknown_property"];
        yield return ["routing_candidate", success.Replace(
            "\"available\":[{\"provider\"", $"\"available\":[{{\"{sentinel}\":0,\"provider\"", StringComparison.Ordinal),
            200, "response_routing_candidate_unknown_property"];
        yield return ["routing_attempt", success.Replace(
            "\"attempts\":[{\"provider\"", $"\"attempts\":[{{\"{sentinel}\":0,\"provider\"", StringComparison.Ordinal),
            200, "response_routing_attempt_unknown_property"];
        yield return ["error", $"{{\"error\":{{\"{sentinel}\":0,\"code\":503,\"message\":\"closed\"}}}}",
            503, "response_error_unknown_property"];
        yield return ["error_metadata", $"{{\"error\":{{\"code\":503,\"message\":\"closed\",\"metadata\":{{\"{sentinel}\":0}}}}}}",
            503, "response_error_metadata_unknown_property"];
    }

    private static byte[] SuccessBody() => Encoding.UTF8.GetBytes("""
        {"id":"gen-offline","object":"chat.completion","created":1,"model":"openai/gpt-5.6-luna","choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{\"agent_id\":\"agent-00\",\"action\":\"GatherWood\",\"quantity\":12}"}}],"usage":{"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"cost":0.000044},"openrouter_metadata":{"requested":"openai/gpt-5.6-luna","strategy":"direct","attempt":1,"is_byok":false,"endpoints":{"total":1,"available":[{"provider":"Azure","model":"openai/gpt-5.6-luna","selected":true}]},"attempts":[{"provider":"Azure","model":"openai/gpt-5.6-luna","status":200}],"pipeline":[]}}
        """);

    private static string DocumentedReasoningBody() => Encoding.UTF8.GetString(SuccessBody())
        .Replace("\"model\":\"openai/gpt-5.6-luna\",\"choices\"",
            "\"model\":\"openai/gpt-5.6-luna\",\"provider\":\"Azure\",\"choices\"", StringComparison.Ordinal)
        .Replace("\"index\":0,\"finish_reason\":\"stop\",\"message\"",
            "\"index\":0,\"finish_reason\":\"stop\",\"native_finish_reason\":\"stop\",\"logprobs\":null,\"message\"",
            StringComparison.Ordinal)
        .Replace("\"role\":\"assistant\",\"content\":\"{\\\"agent_id\\\":\\\"agent-00\\\",\\\"action\\\":\\\"GatherWood\\\",\\\"quantity\\\":12}\"",
            "\"role\":\"assistant\",\"content\":\"{\\\"agent_id\\\":\\\"agent-00\\\",\\\"action\\\":\\\"GatherWood\\\",\\\"quantity\\\":12}\"," +
            "\"refusal\":null,\"reasoning\":null,\"reasoning_details\":[{\"type\":\"reasoning.summary\"," +
            "\"summary\":\"bounded\",\"id\":null,\"format\":\"azure-openai-responses-v1\",\"index\":0}]",
            StringComparison.Ordinal)
        .Replace(",\"attempts\":[{\"provider\":\"Azure\",\"model\":\"openai/gpt-5.6-luna\",\"status\":200}],\"pipeline\":[]",
            string.Empty, StringComparison.Ordinal);

    private sealed class CapturingHandler(byte[] responseBody, HttpStatusCode statusCode = HttpStatusCode.OK, Uri? effectiveUri = null) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? RouterMetadata { get; private set; }
        public string? ContentType { get; private set; }
        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            Uri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RouterMetadata = request.Headers.GetValues("X-OpenRouter-Metadata").Single();
            ContentType = request.Content!.Headers.ContentType!.MediaType;
            Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                RequestMessage = effectiveUri is null ? request : new HttpRequestMessage(request.Method, effectiveUri),
                Content = new ByteArrayContent(responseBody)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") }
                }
            };
        }
    }
}
