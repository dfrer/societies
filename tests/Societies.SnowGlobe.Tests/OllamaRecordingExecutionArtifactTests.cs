using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.SnowGlobe.Tests;

public sealed class OllamaRecordingExecutionArtifactTests
{
    private const string Root = @"C:\offline-artifact-validation-tests";
    private static readonly long StartTicks = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc).Ticks;

    [Fact]
    public async Task CanonicalArtifact_GoldenRoundTripsAndIsDetached()
    {
        OllamaRecordingExecutionArtifact artifact = await CreateArtifact();
        Assert.Equal("df98538e35aa6ed26e5587022c880d8347dd40283f00b51268ca6b479c2435bf", artifact.CanonicalDigestSha256);
        Assert.Equal("cee6a6629b7ac5706b4e047d79b951d37aef32ec3a2544f42d6fbea79fea2a7d", artifact.PayloadDigestSha256);
        OllamaRecordingExecutionArtifact validated = OllamaRecordingExecutionArtifactModule.Validate(artifact.CanonicalUtf8);
        Assert.Equal(artifact.CanonicalDigestSha256, validated.CanonicalDigestSha256);
        Assert.Equal(artifact.PayloadDigestSha256, validated.PayloadDigestSha256);
        Assert.InRange(artifact.CanonicalUtf8.Length, 1, OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes);
        byte[] detached = artifact.CanonicalUtf8.ToArray(); detached[0] ^= 0xff;
        Assert.Equal((byte)'{', artifact.CanonicalUtf8.Span[0]);
        Assert.Equal("snow_globe_ollama_recording_execution_artifact/v2", artifact.SchemaVersion);
        Assert.Equal("None", artifact.TerminalCheckpointCode); Assert.Equal("None", artifact.TerminalPolicyCode);
        Assert.Equal("raw_free_local_loopback_recording_execution_binding_only", artifact.Semantics);
    }

    [Fact]
    public async Task TerminalCheckpointAndPolicy_AreExactAcrossResultReceiptAndArtifact()
    {
        OllamaRecordingExecutionArtifact artifact = await CreateArtifact();
        Assert.Equal("None", artifact.TerminalCheckpointCode); Assert.Equal("None", artifact.TerminalPolicyCode);
        AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_checkpoint_code"] = "ResponseHeaders");
        AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_policy_code"] = "ContentType");
        AssertIntegrityValidForgeryRejected(artifact, (_, _, receipt) => receipt!["terminal_checkpoint_code"] = "ResponseHeaders");
        AssertIntegrityValidForgeryRejected(artifact, (_, _, receipt) => receipt!["terminal_policy_code"] = "ContentType");
        AssertIntegrityValidForgeryRejected(artifact, (_, result, receipt) =>
        {
            result["terminal_checkpoint_code"] = "ResponseHeaders"; result["terminal_policy_code"] = "ContentType";
            receipt!["terminal_checkpoint_code"] = "ResponseHeaders"; receipt["terminal_policy_code"] = "ContentType";
        });
        AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_checkpoint_code"] = "RawSentinelC:/outside/nonce");

        string json = Encoding.UTF8.GetString(artifact.CanonicalUtf8.Span);
        byte[] reordered = Encoding.UTF8.GetBytes(json.Replace(
            "\"terminal_checkpoint_code\":\"None\",\"terminal_policy_code\":\"None\"",
            "\"terminal_policy_code\":\"None\",\"terminal_checkpoint_code\":\"None\"",
            StringComparison.Ordinal));
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(reordered));
    }

    [Fact]
    public async Task PayloadDigestWrongJsonKinds_AreAlwaysClosedArtifactExceptions()
    {
        OllamaRecordingExecutionArtifact artifact = await CreateArtifact();
        Func<JsonNode?>[] wrongKinds =
        [
            static () => JsonValue.Create(7),
            static () => null,
            static () => new JsonObject(),
            static () => new JsonArray()
        ];

        foreach (Func<JsonNode?> createWrongKind in wrongKinds)
        {
            JsonObject outer = JsonNode.Parse(artifact.CanonicalUtf8.Span)!.AsObject();
            outer["artifact_payload_digest_sha256"] = createWrongKind();
            OllamaRecordingExecutionArtifactException outerFailure = Assert.Throws<OllamaRecordingExecutionArtifactException>(
                () => OllamaRecordingExecutionArtifactModule.Validate(JsonSerializer.SerializeToUtf8Bytes(outer)));
            Assert.Equal("artifact_payload_digest_invalid", outerFailure.Code); Assert.Null(outerFailure.InnerException);

            JsonObject receiptRoot = JsonNode.Parse(artifact.CanonicalUtf8.Span)!.AsObject();
            JsonObject result = receiptRoot["result"]!.AsObject(); JsonObject receipt = receiptRoot["receipt"]!.AsObject();
            receipt["receipt_payload_digest_sha256"] = createWrongKind();
            result["receipt_digest_sha256"] = CognitionQualityHash.Sha256(JsonSerializer.SerializeToUtf8Bytes(receipt));
            RecomputeLastDigest(receiptRoot, "artifact_payload_digest_sha256");
            OllamaRecordingExecutionArtifactException receiptFailure = Assert.Throws<OllamaRecordingExecutionArtifactException>(
                () => OllamaRecordingExecutionArtifactModule.Validate(JsonSerializer.SerializeToUtf8Bytes(receiptRoot)));
            Assert.Equal("artifact_receipt_payload_digest_invalid", receiptFailure.Code); Assert.Null(receiptFailure.InnerException);
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("noncanonical")]
    [InlineData("digest")]
    [InlineData("receipt")]
    [InlineData("number_encoding")]
    [InlineData("string_encoding")]
    public async Task StrictValidatorRejectsShapeCanonicalDigestAndNestedReceiptTampering(string mutation)
    {
        byte[] source = (await CreateArtifact()).CanonicalUtf8.ToArray();
        string json = Encoding.UTF8.GetString(source);
        byte[] changed = mutation switch
        {
            "unknown" => Encoding.UTF8.GetBytes(json.Replace("{\"schema_version\"", "{\"unknown\":0,\"schema_version\"", StringComparison.Ordinal)),
            "duplicate" => Encoding.UTF8.GetBytes(json.Replace("{\"schema_version\":", "{\"schema_version\":\"snow_globe_ollama_recording_execution_artifact/v2\",\"schema_version\":", StringComparison.Ordinal)),
            "missing" => Encoding.UTF8.GetBytes(json.Replace("\"semantics\":\"raw_free_local_loopback_recording_execution_binding_only\",", string.Empty, StringComparison.Ordinal)),
            "noncanonical" => Encoding.UTF8.GetBytes(json + " "),
            "digest" => Encoding.UTF8.GetBytes(ReplaceFirstDigestCharacter(json, "\"plan_digest_sha256\":\"")),
            "receipt" => Encoding.UTF8.GetBytes(json.Replace("\"receipt_payload_digest_sha256\":\"", "\"receipt_payload_digest_sha256\":\"0", StringComparison.Ordinal)),
            "number_encoding" => Encoding.UTF8.GetBytes(json.Replace("\"runtime_process_id\":777", "\"runtime_process_id\":7.77e2", StringComparison.Ordinal)),
            "string_encoding" => Encoding.UTF8.GetBytes(json.Replace("http://127.0.0.1:11435/", "http:\\u002f\\u002f127.0.0.1:11435\\u002f", StringComparison.Ordinal)),
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(changed));
    }

    [Fact]
    public async Task ValidatorRejectsBomInvalidUtf8OversizeTrailingAndDepthBeforeAcceptance()
    {
        byte[] valid = (await CreateArtifact()).CanonicalUtf8.ToArray();
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(new byte[] { 0xef, 0xbb, 0xbf }.Concat(valid).ToArray()));
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(new byte[] { 0xc3, 0x28 }));
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(new byte[OllamaRecordingExecutionArtifactModule.MaximumArtifactBytes + 1]));
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(valid.Concat("{}"u8.ToArray()).ToArray()));
        byte[] deep = Encoding.UTF8.GetBytes("{\"a\":[[[[[[[[[0]]]]]]]]]}" );
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(deep));
    }

    [Fact]
    public async Task ConcurrentPureValidationReturnsIndependentArtifacts()
    {
        byte[] bytes = (await CreateArtifact()).CanonicalUtf8.ToArray();
        OllamaRecordingExecutionArtifact[] values = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => OllamaRecordingExecutionArtifactModule.Validate(bytes))));
        Assert.Single(values.Select(static value => value.CanonicalDigestSha256).Distinct(StringComparer.Ordinal));
        byte[] first = values[0].CanonicalUtf8.ToArray(); first[0] = 0;
        Assert.Equal((byte)'{', values[1].CanonicalUtf8.Span[0]);
    }

    [Fact]
    public async Task IntegrityValidButSemanticallyImpossibleFailureTupleIsRejected()
    {
        OllamaRecordingExecutionArtifact terminal = await CreateTerminalArtifact();
        JsonObject root = JsonNode.Parse(terminal.CanonicalUtf8.Span)!.AsObject(); JsonObject result = root["result"]!.AsObject(); JsonObject receipt = root["receipt"]!.AsObject();
        result["composition_failure_code"] = "None"; result["recording_failure_code"] = "None"; receipt["failure_code"] = "None";
        RecomputeLastDigest(receipt, "receipt_payload_digest_sha256");
        result["receipt_digest_sha256"] = CognitionQualityHash.Sha256(JsonSerializer.SerializeToUtf8Bytes(receipt));
        RecomputeLastDigest(root, "artifact_payload_digest_sha256"); byte[] forged = JsonSerializer.SerializeToUtf8Bytes(root);
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(forged));
    }

    [Fact]
    public async Task IntegrityValidEnumCrossProductsAcceptOnlyExactDerivedCompleteAndFailedRows()
    {
        OllamaRecordingExecutionArtifact complete = await CreateArtifact();
        foreach (string outcome in Enum.GetNames<OllamaRecordingCompositionOutcomeCode>())
        foreach (string failure in Enum.GetNames<OllamaRecordingCompositionFailureCode>())
        {
            if (outcome == "Complete" && failure == "None") continue;
            AssertIntegrityValidForgeryRejected(complete, (_, result, _) =>
            { result["composition_outcome_code"] = outcome; result["composition_failure_code"] = failure; });
        }
        foreach (string outcome in Enum.GetNames<SnowGlobeOllamaLoopbackRecordingOutcomeCode>())
        foreach (string failure in Enum.GetNames<SnowGlobeOllamaLoopbackRecordingFailureCode>())
        {
            if (outcome == "Complete" && failure == "None") continue;
            AssertIntegrityValidForgeryRejected(complete, (_, result, _) =>
            { result["recording_outcome_code"] = outcome; result["recording_failure_code"] = failure; });
        }

        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        foreach (string outcome in Enum.GetNames<OllamaRecordingCompositionOutcomeCode>())
        foreach (string failure in Enum.GetNames<OllamaRecordingCompositionFailureCode>())
        {
            if (outcome == "Failed" && failure == "WrapperRejected") continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, _) =>
            { result["composition_outcome_code"] = outcome; result["composition_failure_code"] = failure; });
        }
        foreach (string outcome in Enum.GetNames<SnowGlobeOllamaLoopbackRecordingOutcomeCode>())
        foreach (string failure in Enum.GetNames<SnowGlobeOllamaLoopbackRecordingFailureCode>())
        {
            if (outcome == "Failed" && failure == "WrapperRejected") continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
            {
                result["recording_outcome_code"] = outcome; result["recording_failure_code"] = failure;
                receipt!["outcome"] = outcome; receipt["failure_code"] = failure;
            });
        }
        foreach (string failure in Enum.GetNames<SnowGlobeOllamaLoopbackRecordingFailureCode>())
        {
            if (failure is "WrapperRejected" or "RuntimeChanged") continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
            {
                result["composition_failure_code"] = failure;
                result["recording_failure_code"] = failure;
                receipt!["failure_code"] = failure;
            });
        }
    }

    [Fact]
    public async Task IntegrityValidResponseBodyRejectedReceiptAcceptsExactNullWrapperTerminalProvenance()
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        byte[] bodyRejected = ForgeIntegrityValid(failed, (_, result, receipt) =>
        {
            SetResponseBodyRejected(result, receipt!, wrapperDigest: null);
        });

        OllamaRecordingExecutionArtifact validated = OllamaRecordingExecutionArtifactModule.Validate(bodyRejected);
        Assert.Equal("ResponseBodyRejected", validated.FailureCode);
        Assert.Equal(SubmissionState.ResponseReceived.ToString(), validated.TerminalSubmissionState);
        Assert.Equal(200, validated.TerminalStatusCode);
    }

    [Fact]
    public async Task IntegrityValidResponseBodyRejectedReceiptRejectsNonNullWrapperDigest()
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            SetResponseBodyRejected(result, receipt!, new string('d', 64));
        });
    }

    [Fact]
    public async Task IntegrityValidRuntimeChangedReceiptRejectsImpossibleTerminalProvenance()
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            result["composition_failure_code"] = "RuntimeChanged";
            result["recording_failure_code"] = "RuntimeChanged";
            result["terminal_submission_state"] = "DefinitelyNotSubmitted";
            result["terminal_status_code"] = null;
            receipt!["failure_code"] = "RuntimeChanged";
            receipt["slots"]!.AsArray().RemoveAt(2);
        });
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            result["composition_failure_code"] = "RuntimeChanged";
            result["recording_failure_code"] = "RuntimeChanged";
            result["terminal_status_code"] = 429;
            receipt!["failure_code"] = "RuntimeChanged";
            receipt["slots"]![2]!["status_code"] = 429;
        });
    }

    [Theory]
    [InlineData((int)SubmissionState.DefinitelyNotSubmitted, null, false)]
    [InlineData((int)SubmissionState.ResponseReceived, 429, false)]
    [InlineData((int)SubmissionState.ResponseReceived, 200, false)]
    [InlineData((int)SubmissionState.ResponseReceived, 200, true)]
    public async Task IntegrityValidRuntimeChangedReceiptAcceptsExactTransportEmittableTerminalProvenance(int submissionValue, int? status, bool wrapperPresent)
    {
        SubmissionState submission = (SubmissionState)submissionValue;
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        byte[] runtimeChanged = ForgeIntegrityValid(failed, (_, result, receipt) =>
        {
            result["composition_failure_code"] = "RuntimeChanged";
            result["recording_failure_code"] = "RuntimeChanged";
            result["terminal_submission_state"] = submission.ToString();
            result["terminal_status_code"] = status;
            receipt!["failure_code"] = "RuntimeChanged";
            string checkpoint = submission == SubmissionState.DefinitelyNotSubmitted
                ? "BeforeDispatch" : wrapperPresent ? "AfterExchange" : "ResponseHeaders";
            result["terminal_checkpoint_code"] = checkpoint;
            result["terminal_policy_code"] = "RuntimeOwnership";
            receipt["terminal_checkpoint_code"] = checkpoint;
            receipt["terminal_policy_code"] = "RuntimeOwnership";
            receipt["slots"]![2]!["wrapper_digest_sha256"] = wrapperPresent ? new string('d', 64) : null;
            receipt["slots"]![2]!["status_code"] = status;
            receipt["slots"]![2]!["submission_state"] = submission.ToString();
        });

        OllamaRecordingExecutionArtifact validated = OllamaRecordingExecutionArtifactModule.Validate(runtimeChanged);
        Assert.Equal("RuntimeChanged", validated.FailureCode); Assert.Equal(submission.ToString(), validated.TerminalSubmissionState); Assert.Equal(status, validated.TerminalStatusCode);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(429)]
    public async Task IntegrityValidHttpResponseRejectedReceiptAcceptsExactTransportEmittableTerminalProvenance(int status)
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        byte[] headerRejected = ForgeIntegrityValid(failed, (_, result, receipt) =>
        {
            SetHttpResponseRejected(result, receipt!, status);
        });

        OllamaRecordingExecutionArtifact validated = OllamaRecordingExecutionArtifactModule.Validate(headerRejected);
        Assert.Equal("HttpResponseRejected", validated.FailureCode);
        Assert.Equal(SubmissionState.ResponseReceived.ToString(), validated.TerminalSubmissionState);
        Assert.Equal(status, validated.TerminalStatusCode);
    }

    [Fact]
    public async Task IntegrityValidHttpResponseRejectedReceiptRejectsImpossibleTerminalProvenance()
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            SetHttpResponseRejected(result, receipt!, 200);
            receipt!["slots"]!.AsArray().RemoveAt(2);
        });
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            SetHttpResponseRejected(result, receipt!, 200);
            receipt!["slots"]![2]!["wrapper_digest_sha256"] = new string('d', 64);
        });
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            SetHttpResponseRejected(result, receipt!, 200);
            result["terminal_submission_state"] = SubmissionState.SubmissionUnknown.ToString();
            receipt!["slots"]![2]!["submission_state"] = SubmissionState.SubmissionUnknown.ToString();
        });
        AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
        {
            SetHttpResponseRejected(result, receipt!, 200);
            result["terminal_status_code"] = null;
            receipt!["slots"]![2]!["status_code"] = null;
        });
        foreach (int status in new[] { 99, 600 })
        {
            AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
            {
                SetHttpResponseRejected(result, receipt!, status);
            });
        }
    }

    [Fact]
    public async Task IntegrityValidTerminalBoundsNullabilitySubmissionStatusAndReceiptMatrixIsClosed()
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        foreach (int? completed in Enumerable.Range(-1, 15).Select(static value => (int?)value).Append(null))
        {
            if (completed == 2) continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
            { result["completed_slot_count"] = completed; receipt!["completed_slot_count"] = completed; });
        }
        foreach (int? terminal in Enumerable.Range(0, 14).Select(static value => (int?)value).Append(null))
        {
            if (terminal == 3) continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, receipt) =>
            { result["terminal_slot_ordinal"] = terminal; receipt!["terminal_slot_ordinal"] = terminal; });
        }
        foreach (string? submission in Enum.GetNames<SubmissionState>().Cast<string?>().Append(null))
        {
            if (submission == SubmissionState.ResponseReceived.ToString()) continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["terminal_submission_state"] = submission);
        }
        foreach (int? status in new int?[] { null, 99, 100, 199, 201, 399, 429, 599, 600 })
            AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["terminal_status_code"] = status);
        foreach (string? charge in Enum.GetNames<ChargeState>().Cast<string?>().Append(null))
        {
            if (charge == ChargeState.NotApplicable.ToString()) continue;
            AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["terminal_charge_state"] = charge);
        }
        AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["recording_result_present"] = false);
        AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["receipt_present"] = false);
        AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["receipt_digest_sha256"] = null);
        AssertIntegrityValidForgeryRejected(failed, (_, result, _) => result["nested_recording_evidence_digest_sha256"] = new string('a', 64));
    }

    [Fact]
    public void CompositionOnlyRowsRequireExactLabelsAndAllRecordingFieldsNull()
    {
        SnowGlobeOllamaRecordingCompositionModule module = new(Root);
        OllamaRecordingCompositionPlan authorizationPlan = module.Prepare(new(777, StartTicks), "authorization-row-v1");
        OllamaRecordingExecutionArtifact authorization = OllamaRecordingExecutionArtifactModule.Create(authorizationPlan,
            new("AuthorizationRejected", "AuthorizationRejected", false, null, null, null, null, null, null, null,
                "Authorization", "Authorization", null, null, null));
        SnowGlobeOllamaRecordingCompositionModule second = new(Root);
        OllamaRecordingExecutionArtifact composition = OllamaRecordingExecutionArtifactModule.Create(
            second.Prepare(new(777, StartTicks), "composition-row-v1"),
            new("CompositionFailed", "CompositionFailed", false, null, null, null, null, null, null, null,
                "Composition", "UnexpectedException", null, null, null));
        Assert.Equal("AuthorizationRejected", OllamaRecordingExecutionArtifactModule.Validate(authorization.CanonicalUtf8).OutcomeCode);
        Assert.Equal("CompositionFailed", OllamaRecordingExecutionArtifactModule.Validate(composition.CanonicalUtf8).OutcomeCode);

        foreach (OllamaRecordingExecutionArtifact artifact in new[] { authorization, composition })
        {
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["recording_result_present"] = true);
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["recording_outcome_code"] = "Failed");
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["recording_failure_code"] = "TransportFailure");
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["completed_slot_count"] = 0);
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_slot_ordinal"] = 1);
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_submission_state"] = "DefinitelyNotSubmitted");
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_charge_state"] = "NotApplicable");
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["terminal_status_code"] = 200);
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["receipt_present"] = true);
            AssertIntegrityValidForgeryRejected(artifact, (_, result, _) => result["nested_recording_evidence_digest_sha256"] = new string('a', 64));
        }
    }

    [Fact]
    public async Task IntegrityValidReceiptSlotOrderSuccessAndTerminalRulesAreClosed()
    {
        OllamaRecordingExecutionArtifact failed = await CreateTerminalArtifact();
        AssertIntegrityValidForgeryRejected(failed, (_, _, receipt) =>
        {
            JsonArray slots = receipt!["slots"]!.AsArray(); JsonNode first = slots[0]!.DeepClone(); JsonNode second = slots[1]!.DeepClone(); slots[0] = second; slots[1] = first;
        });
        AssertIntegrityValidForgeryRejected(failed, (_, _, receipt) => receipt!["slots"]![0]!["wrapper_digest_sha256"] = null);
        AssertIntegrityValidForgeryRejected(failed, (_, _, receipt) => receipt!["slots"]![0]!["status_code"] = 201);
        AssertIntegrityValidForgeryRejected(failed, (_, _, receipt) => receipt!["slots"]![0]!["submission_state"] = "SubmissionUnknown");
        AssertIntegrityValidForgeryRejected(failed, (_, _, receipt) => receipt!["slots"]![2]!["wrapper_digest_sha256"] = null);
        AssertIntegrityValidForgeryRejected(failed, (_, _, receipt) =>
        {
            JsonArray slots = receipt!["slots"]!.AsArray(); slots.Add(JsonNode.Parse(slots[2]!.ToJsonString()));
        });
    }

    [Fact]
    public async Task IntegrityValidEvidenceRejectedRowIsAccepted()
    {
        OllamaRecordingExecutionArtifact complete = await CreateArtifact();
        byte[] evidenceRejected = ForgeIntegrityValid(complete, (_, result, receipt) =>
        {
            result["composition_outcome_code"] = "Failed";
            result["composition_failure_code"] = "EvidenceRejected";
            result["recording_outcome_code"] = "Failed";
            result["recording_failure_code"] = "EvidenceRejected";
            result["terminal_slot_ordinal"] = 12;
            result["nested_recording_evidence_digest_sha256"] = null;
            result["terminal_checkpoint_code"] = "EvidenceConstruction";
            result["terminal_policy_code"] = "EvidenceShape";
            receipt!["status"] = "terminal";
            receipt["outcome"] = "Failed";
            receipt["failure_code"] = "EvidenceRejected";
            receipt["terminal_checkpoint_code"] = "EvidenceConstruction";
            receipt["terminal_policy_code"] = "EvidenceShape";
            receipt["terminal_slot_ordinal"] = 12;
            receipt["nested_recording_evidence_digest_sha256"] = null;
        });

        OllamaRecordingExecutionArtifact validated = OllamaRecordingExecutionArtifactModule.Validate(evidenceRejected);
        Assert.Equal("Failed", validated.OutcomeCode); Assert.Equal("EvidenceRejected", validated.FailureCode);
        Assert.Equal(12, validated.CompletedSlotCount); Assert.Equal(12, validated.TerminalSlotOrdinal);
        Assert.True(validated.ReceiptPresent); Assert.Null(validated.NestedRecordingEvidenceDigestSha256);

        Action<JsonObject, JsonObject, JsonObject?>[] resultMutations =
        [
            (_, result, _) => result["composition_outcome_code"] = null,
            (_, result, _) => result["composition_failure_code"] = "None",
            (_, result, _) => result["recording_result_present"] = false,
            (_, result, _) => result["recording_result_present"] = null,
            (_, result, _) => result["repository_root_digest_sha256"] = null,
            (_, result, _) => result["recording_outcome_code"] = "Complete",
            (_, result, _) => result["recording_outcome_code"] = null,
            (_, result, _) => result["recording_failure_code"] = "None",
            (_, result, _) => result["recording_failure_code"] = null,
            (_, result, _) => result["completed_slot_count"] = 11,
            (_, result, _) => result["completed_slot_count"] = null,
            (_, result, _) => result["terminal_slot_ordinal"] = 11,
            (_, result, _) => result["terminal_slot_ordinal"] = null,
            (_, result, _) => result["terminal_submission_state"] = "SubmissionUnknown",
            (_, result, _) => result["terminal_submission_state"] = null,
            (_, result, _) => result["terminal_charge_state"] = "Unknown",
            (_, result, _) => result["terminal_charge_state"] = null,
            (_, result, _) => result["terminal_status_code"] = 201,
            (_, result, _) => result["terminal_status_code"] = null,
            (_, result, _) => result["terminal_checkpoint_code"] = "WrapperDecode",
            (_, result, _) => result["terminal_policy_code"] = "WrapperShape",
            (_, result, _) => result["additional_attempt_authorized"] = true,
            (_, result, _) => result["additional_attempt_authorized"] = null,
            (_, result, _) => result["automatic_retry_count"] = 1,
            (_, result, _) => result["automatic_retry_count"] = null,
            (_, result, _) => result["fallback_count"] = 1,
            (_, result, _) => result["fallback_count"] = null,
            (_, result, _) => result["alternate_endpoint_or_model_count"] = 1,
            (_, result, _) => result["alternate_endpoint_or_model_count"] = null,
            (_, result, _) => result["receipt_present"] = false,
            (_, result, _) => result["receipt_present"] = null,
            (_, result, _) => result["receipt_digest_sha256"] = null,
            (_, result, _) => result["nested_recording_evidence_digest_sha256"] = new string('a', 64)
        ];
        foreach (Action<JsonObject, JsonObject, JsonObject?> mutation in resultMutations)
            AssertIntegrityValidForgeryRejected(validated, mutation);

        Action<JsonObject, JsonObject, JsonObject?>[] receiptMutations =
        [
            (_, _, receipt) => receipt!["status"] = "complete",
            (_, _, receipt) => receipt!["outcome"] = "Complete",
            (_, _, receipt) => receipt!["failure_code"] = "None",
            (_, _, receipt) => receipt!["terminal_checkpoint_code"] = "WrapperDecode",
            (_, _, receipt) => receipt!["terminal_policy_code"] = "WrapperShape",
            (_, _, receipt) => receipt!["slots"]!.AsArray().RemoveAt(11),
            (_, _, receipt) => receipt!["slots"]![11]!["wrapper_digest_sha256"] = null,
            (_, _, receipt) => receipt!["slots"]![11]!["status_code"] = 201,
            (_, _, receipt) => receipt!["slots"]![11]!["submission_state"] = "SubmissionUnknown",
            (_, _, receipt) => receipt!["completed_slot_count"] = 11,
            (_, _, receipt) => receipt!["terminal_slot_ordinal"] = 11,
            (_, _, receipt) => receipt!["terminal_slot_ordinal"] = null,
            (_, _, receipt) => receipt!["automatic_retry_count"] = 1,
            (_, _, receipt) => receipt!["fallback_count"] = 1,
            (_, _, receipt) => receipt!["alternate_endpoint_or_model_count"] = 1,
            (_, _, receipt) => receipt!["nested_recording_evidence_digest_sha256"] = new string('a', 64)
        ];
        foreach (Action<JsonObject, JsonObject, JsonObject?> mutation in receiptMutations)
            AssertIntegrityValidForgeryRejected(validated, mutation);
        foreach (string failure in Enum.GetNames<SnowGlobeOllamaLoopbackRecordingFailureCode>())
        {
            if (failure == SnowGlobeOllamaLoopbackRecordingFailureCode.EvidenceRejected.ToString()) continue;
            AssertIntegrityValidForgeryRejected(validated, (_, result, receipt) =>
            {
                result["composition_failure_code"] = failure;
                result["recording_failure_code"] = failure;
                receipt!["failure_code"] = failure;
            });
        }
    }

    [Fact]
    public async Task ArtifactCopiedToAnotherVerifiedRootIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        string firstRoot = Path.Combine(Path.GetTempPath(), "recording-root-a-" + Guid.NewGuid().ToString("N")); string secondRoot = Path.Combine(Path.GetTempPath(), "recording-root-b-" + Guid.NewGuid().ToString("N"));
        try
        {
            OllamaRecordingExecutionArtifact artifact = await CreateArtifact(firstRoot); CreateRepositoryMarkers(firstRoot); CreateRepositoryMarkers(secondRoot);
            WriteArtifact(firstRoot, artifact); WriteArtifact(secondRoot, artifact);
            Assert.Equal(artifact.CanonicalDigestSha256, new SnowGlobeOllamaRecordingCompositionModule(firstRoot).ValidateArtifact().CanonicalDigestSha256);
            Assert.Throws<OllamaRecordingCompositionException>(() => new SnowGlobeOllamaRecordingCompositionModule(secondRoot).ValidateArtifact());
        }
        finally { if (Directory.Exists(firstRoot)) Directory.Delete(firstRoot, true); if (Directory.Exists(secondRoot)) Directory.Delete(secondRoot, true); }
    }

    [Fact]
    public async Task SameRuntimeAndNonceOnDifferentRootsProduceDifferentArtifactBindingsAndDigests()
    {
        OllamaRecordingExecutionArtifact first = await CreateArtifact(@"C:\artifact-root-a");
        OllamaRecordingExecutionArtifact second = await CreateArtifact(@"C:\artifact-root-b");
        Assert.NotEqual(first.RepositoryRootDigestSha256, second.RepositoryRootDigestSha256);
        Assert.NotEqual(first.PayloadDigestSha256, second.PayloadDigestSha256);
        Assert.NotEqual(first.CanonicalDigestSha256, second.CanonicalDigestSha256);
    }

    private static void AssertIntegrityValidForgeryRejected(
        OllamaRecordingExecutionArtifact artifact,
        Action<JsonObject, JsonObject, JsonObject?> mutate)
    {
        byte[] forged = ForgeIntegrityValid(artifact, mutate);
        Assert.Throws<OllamaRecordingExecutionArtifactException>(() => OllamaRecordingExecutionArtifactModule.Validate(forged));
    }

    private static byte[] ForgeIntegrityValid(
        OllamaRecordingExecutionArtifact artifact,
        Action<JsonObject, JsonObject, JsonObject?> mutate)
    {
        JsonObject root = JsonNode.Parse(artifact.CanonicalUtf8.Span)!.AsObject();
        JsonObject result = root["result"]!.AsObject();
        JsonObject? receipt = root["receipt"] as JsonObject;
        mutate(root, result, receipt);
        if (receipt is not null)
        {
            bool preserveNullDigest = result["receipt_digest_sha256"] is null;
            RecomputeLastDigest(receipt, "receipt_payload_digest_sha256");
            if (!preserveNullDigest) result["receipt_digest_sha256"] = CognitionQualityHash.Sha256(JsonSerializer.SerializeToUtf8Bytes(receipt));
        }
        RecomputeLastDigest(root, "artifact_payload_digest_sha256");
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static void SetHttpResponseRejected(JsonObject result, JsonObject receipt, int status)
    {
        result["composition_failure_code"] = "HttpResponseRejected";
        result["recording_failure_code"] = "HttpResponseRejected";
        result["terminal_submission_state"] = SubmissionState.ResponseReceived.ToString();
        result["terminal_status_code"] = status;
        result["terminal_checkpoint_code"] = "ResponseHeaders";
        result["terminal_policy_code"] = status == 200 ? "ContentType" : "HttpStatus";
        receipt["failure_code"] = "HttpResponseRejected";
        receipt["terminal_checkpoint_code"] = "ResponseHeaders";
        receipt["terminal_policy_code"] = status == 200 ? "ContentType" : "HttpStatus";
        receipt["slots"]![2]!["wrapper_digest_sha256"] = null;
        receipt["slots"]![2]!["status_code"] = status;
        receipt["slots"]![2]!["submission_state"] = SubmissionState.ResponseReceived.ToString();
    }

    private static void SetResponseBodyRejected(JsonObject result, JsonObject receipt, string? wrapperDigest)
    {
        result["composition_failure_code"] = "ResponseBodyRejected";
        result["recording_failure_code"] = "ResponseBodyRejected";
        result["terminal_submission_state"] = SubmissionState.ResponseReceived.ToString();
        result["terminal_status_code"] = 200;
        result["terminal_checkpoint_code"] = "ResponseBody";
        result["terminal_policy_code"] = "BodyRead";
        receipt["failure_code"] = "ResponseBodyRejected";
        receipt["terminal_checkpoint_code"] = "ResponseBody";
        receipt["terminal_policy_code"] = "BodyRead";
        receipt["slots"]![2]!["wrapper_digest_sha256"] = wrapperDigest;
        receipt["slots"]![2]!["status_code"] = 200;
        receipt["slots"]![2]!["submission_state"] = SubmissionState.ResponseReceived.ToString();
    }

    private static string ReplaceFirstDigestCharacter(string json, string prefix)
    {
        int start = json.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        return json[..start] + (json[start] == '0' ? '1' : '0') + json[(start + 1)..];
    }

    private static async Task<OllamaRecordingExecutionArtifact> CreateArtifact(string root = Root)
    {
        InMemoryOllamaRecordingArtifactStore store = new();
        OllamaRecordingCompositionTests.TestTransportFactory factory = new(TestWrappers.Valid());
        SnowGlobePinnedOllamaRecordingModule inner = new(new Clock(), factory);
        SnowGlobeOllamaRecordingCompositionModule module = new(root, inner, store);
        OllamaRecordingCompositionResult result = await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), "artifact-test-nonce-v1"));
        return result.Artifact!;
    }

    private sealed class Clock : ICognitionQualityRecordingSessionClock { public long NowMilliseconds => 1; }

    private static async Task<OllamaRecordingExecutionArtifact> CreateTerminalArtifact()
    {
        byte[][] wrappers = TestWrappers.Valid(); wrappers[2] = "{}"u8.ToArray(); InMemoryOllamaRecordingArtifactStore store = new();
        SnowGlobeOllamaRecordingCompositionModule module = new(Root, new SnowGlobePinnedOllamaRecordingModule(new Clock(), new OllamaRecordingCompositionTests.TestTransportFactory(wrappers)), store);
        return (await module.ExecuteAndPublishOnceAsync(module.Prepare(new(777, StartTicks), "semantic-forgery-v1"))).Artifact!;
    }

    private static void RecomputeLastDigest(JsonObject value, string propertyName)
    {
        value.Remove(propertyName); byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value); value[propertyName] = CognitionQualityHash.Sha256(payload);
    }

    private static void CreateRepositoryMarkers(string root)
    {
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, ".git"), "gitdir: offline-test"); File.WriteAllText(Path.Combine(root, "CURRENT_BUILD.md"), "# offline test");
        string lab = Path.Combine(root, "labs", "Societies.SnowGlobe"); Directory.CreateDirectory(lab); File.WriteAllText(Path.Combine(lab, "Societies.SnowGlobe.csproj"), "<Project />");
    }

    private static void WriteArtifact(string root, OllamaRecordingExecutionArtifact artifact)
    {
        string path = Path.Combine(root, OllamaRecordingExecutionArtifactModule.RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, artifact.CanonicalUtf8.ToArray());
    }
}
