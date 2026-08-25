using Godot;
using Societies.Simulation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeCognitionModuleTests
    {
        [Fact]
        public void ContractDigestsArePinnedAndBindDefinitions()
        {
            Assert.Equal("98bee0f1cee7b8f2e4dd6b6850ffeb80cc996cd316f2006f9b0b04dae4d64864", PrototypeCognitionModule.ObservationContractDigest);
            Assert.Equal("562beb9b05aa62ffbb147d9d43f8b0aff41ea61d572ba24f31c08b46b505b12d", PrototypeCognitionModule.ProposalContractDigest);
            Assert.Equal(PrototypeCognitionModule.ObservationContractDigest, Digest(PrototypeCognitionModule.ObservationContractDefinition));
            Assert.Equal(PrototypeCognitionModule.ProposalContractDigest, Digest(PrototypeCognitionModule.ProposalContractDefinition));
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenRole.Forager, "support_policy")]
        [InlineData(PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenRole.Builder, "oppose_policy")]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenRole.Forager, "request_reconsideration")]
        public void ValidClosedStancesAreAcceptedAndAppendOneCanonicalEvent(PrototypeCivicPolicy policy, PrototypeCitizenRole role, string action)
        {
            PrototypeRuntimeSession session = CreateSelectedSession(policy);
            session.Workers[0].Role = role; session.Workers[0].Needs.Nutrition = 100; session.Workers[0].Needs.Fatigue = 0;
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
            PrototypeCognitionProposal fallback = Assert.IsType<PrototypeCognitionProposal>(module.Resolve(session, observation, PrototypeCognitionEvidence.Missing()).Proposal);
            byte[] raw = module.EncodeCanonicalProposal(observation, fallback.CitizenId, action, fallback.ReasonCode, fallback.Summary);
            PrototypeCognitionResolution result = module.Resolve(session, observation, PrototypeCognitionEvidence.Proposal(raw));

            Assert.True(result.Accepted); Assert.Equal(PrototypeCognitionDecisionSource.ValidatedProposal, result.Source); Assert.True(module.Apply(session, result));
            Assert.Equal(PrototypeEventTypes.CivicCognitionDecision, session.EventLog.Entries[^1].EventType);
            Assert.Contains("\"source\":\"validated_proposal\"", session.EventLog.Entries[^1].Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void EveryClosedNoOutputConditionUsesFallback(int condition)
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
            PrototypeCognitionEvidence evidence = condition switch { 0 => PrototypeCognitionEvidence.Missing(), 1 => PrototypeCognitionEvidence.Cancelled(), 2 => PrototypeCognitionEvidence.TimedOut(), _ => PrototypeCognitionEvidence.Unavailable() };
            PrototypeCognitionResolution result = module.Resolve(session, observation, evidence);
            Assert.True(result.Accepted); Assert.Equal(PrototypeCognitionDecisionSource.DeterministicFallback, result.Source); Assert.True(module.Apply(session, result));
        }

        [Fact]
        public void ResolutionCapabilityIsOneUseAndDistinctResolutionsInterleaveInCallOrder()
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
            PrototypeCognitionProposal expected = Assert.IsType<PrototypeCognitionProposal>(module.Resolve(session, observation, PrototypeCognitionEvidence.Missing()).Proposal);
            PrototypeCognitionResolution first = module.Resolve(session, observation, PrototypeCognitionEvidence.Proposal(module.EncodeCanonicalProposal(observation, expected.CitizenId, expected.Action, expected.ReasonCode, expected.Summary)));
            PrototypeCognitionResolution second = module.Resolve(session, observation, PrototypeCognitionEvidence.Unavailable());
            int before = session.EventLog.Entries.Count;

            Assert.True(module.Apply(session, second));
            Assert.True(module.Apply(session, first));
            Assert.False(module.Apply(session, second));
            Assert.Equal(before + 2, session.EventLog.Entries.Count);
            Assert.Contains("\"source\":\"deterministic_fallback\"", session.EventLog.Entries[before].Message, StringComparison.Ordinal);
            Assert.Contains("\"source\":\"validated_proposal\"", session.EventLog.Entries[before + 1].Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolutionSourceAndDtosCannotBePubliclyConstructedOrRelabeledAndExposeDefensiveBytes()
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
            PrototypeCognitionResolution result = module.Resolve(session, observation, PrototypeCognitionEvidence.Missing());
            byte[] observationBytes = observation.CanonicalUtf8; observationBytes[0] = (byte)'!';
            byte[] proposalBytes = result.Proposal!.CanonicalUtf8; proposalBytes[0] = (byte)'!';

            Assert.Equal((PrototypeCognitionDecisionSource?)PrototypeCognitionDecisionSource.DeterministicFallback, result.Source);
            Assert.Empty(typeof(PrototypeCognitionObservation).GetConstructors());
            Assert.Empty(typeof(PrototypeCognitionProposal).GetConstructors());
            Assert.Empty(typeof(PrototypeCognitionResolution).GetConstructors());
            Assert.Null(typeof(PrototypeCognitionResolution).GetProperty(nameof(PrototypeCognitionResolution.Source))!.SetMethod);
            Assert.NotEqual((byte)'!', observation.CanonicalUtf8[0]);
            Assert.NotEqual((byte)'!', result.Proposal.CanonicalUtf8[0]);
            Assert.True(module.Apply(session, result));
            Assert.Contains("\"source\":\"deterministic_fallback\"", session.EventLog.Entries[^1].Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EvidenceSnapshotsCallerBytesBeforeValidation()
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
            PrototypeCognitionProposal proposal = Assert.IsType<PrototypeCognitionProposal>(module.Resolve(session, observation, PrototypeCognitionEvidence.Missing()).Proposal);
            byte[] raw = module.EncodeCanonicalProposal(observation, proposal.CitizenId, proposal.Action, proposal.ReasonCode, proposal.Summary);
            PrototypeCognitionEvidence evidence = PrototypeCognitionEvidence.Proposal(raw);
            raw[0] = 0xff;
            PrototypeCognitionResolution result = module.Resolve(session, observation, evidence);

            Assert.True(result.Accepted); Assert.Equal(PrototypeCognitionDecisionSource.ValidatedProposal, result.Source);
        }

        [Theory]
        [InlineData("bad\"quote")] [InlineData("bad\\slash")] [InlineData("bad\ncontrol")] [InlineData("bad;other")]
        public void HostileRuntimeCitizenIdsRejectWithAStableTypedCode(string hostileId)
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            session.Workers[0].WorkerId = hostileId;
            PrototypeCognitionException exception = Assert.Throws<PrototypeCognitionException>(() => new PrototypeCognitionModule().PublishObservation(session, hostileId));
            Assert.Equal("invalid_citizen_id", exception.Code);
        }

        [Theory]
        [InlineData("duplicate_citizen_id", 0)] [InlineData("invalid_role", 1)] [InlineData("invalid_nutrition", 2)] [InlineData("invalid_fatigue", 3)]
        public void PublicationNormalizesDuplicateAndInvalidCitizenFacts(string expectedCode, int mutation)
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            switch (mutation)
            {
                case 0: session.Workers[1].WorkerId = session.Workers[0].WorkerId; break;
                case 1: session.Workers[0].Role = (PrototypeCitizenRole)99; break;
                case 2: session.Workers[0].Needs.Nutrition = float.NaN; break;
                default: session.Workers[0].Needs.Fatigue = float.PositiveInfinity; break;
            }
            PrototypeCognitionException exception = Assert.Throws<PrototypeCognitionException>(() => new PrototypeCognitionModule().PublishObservation(session, session.Workers[0].WorkerId));
            Assert.Equal(expectedCode, exception.Code);
        }

        [Fact]
        public void ExactJsonDepthBoundaryIsIndependentlyProven()
        {
            Assert.True(PrototypeCognitionModule.IsWithinMaximumJsonDepthForTests(Utf8("{\"a\":{\"b\":1}}")));
            Assert.False(PrototypeCognitionModule.IsWithinMaximumJsonDepthForTests(Utf8("{\"a\":{\"b\":{\"c\":1}}}")));
        }

        [Fact]
        public void ParserAdversariesAlterOneInvariantFromValidCanonicalBytesAndRemainInertBeforeFallback()
        {
            foreach ((string name, Func<PrototypeRuntimeSession, PrototypeCognitionModule, PrototypeCognitionObservation, byte[]> mutate) in Adversaries())
            {
                PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
                PrototypeCognitionModule module = new();
                PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
                PrototypeCognitionResolution baseline = module.Resolve(session, observation, PrototypeCognitionEvidence.Proposal(Valid(session, module, observation)));
                Assert.True(baseline.Accepted, name + " baseline");
                Assert.Equal(PrototypeCognitionDecisionSource.ValidatedProposal, baseline.Source);
                string snapshot = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
                string events = PrototypePersistenceService.SerializeEventLog(session.EventLog);
                PrototypeCognitionResolution result = module.Resolve(session, observation, PrototypeCognitionEvidence.Proposal(mutate(session, module, observation)));
                Assert.True(result.Accepted, name); Assert.Equal(PrototypeCognitionDecisionSource.DeterministicFallback, result.Source);
                Assert.Equal(snapshot, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
                Assert.Equal(events, PrototypePersistenceService.SerializeEventLog(session.EventLog));
                Assert.True(module.Apply(session, result));
            }
        }

        [Fact]
        public void StaleAndUndefinedEvidenceAreRejectedWithNoSourceOrMutation()
        {
            PrototypeRuntimeSession session = CreateSelectedSession(PrototypeCivicPolicy.ProtectWetland);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId);
            _ = session.Advance(1.0f / 20.0f, 600.0f);
            PrototypeCognitionResolution stale = module.Resolve(session, observation, PrototypeCognitionEvidence.Missing());
            PrototypeCognitionResolution undefined = module.Resolve(session, module.PublishObservation(session, session.Workers[0].WorkerId), new PrototypeCognitionEvidence((PrototypeCognitionEvidenceCondition)99, null));
            Assert.False(stale.Accepted); Assert.Null(stale.Source); Assert.Equal("stale_observation", stale.ErrorCode);
            Assert.False(undefined.Accepted); Assert.Null(undefined.Source); Assert.Equal("invalid_evidence_condition", undefined.ErrorCode);
        }

        [Fact]
        public void MidRunCheckpointWithWetlandConsumptionPreservesFutureFallbackExactly()
        {
            PrototypeRuntimeSession direct = CreateSelectedSession(PrototypeCivicPolicy.DrawDownWetland);
            PrototypeResourceSnapshot reeds = direct.ResourceSnapshots.First(resource => resource.ResourceId == PrototypeWetlandCatalog.ReedResourceId && resource.UnitsRemaining > 0);
            Assert.True(direct.HarvestForPlayer(reeds.SiteId, 1).Succeeded);
            _ = direct.Advance(1.0f / 20.0f, 600.0f);
            PrototypeRuntimeSnapshot checkpoint = direct.CaptureSnapshot(Vector3.Zero);
            string checkpointEvents = PrototypePersistenceService.SerializeEventLog(direct.EventLog);
            PrototypeRunSummary checkpointSummary = PrototypeRunSummaryBuilder.Build(checkpoint, direct.EventLog.Entries, direct.RunStartHour);
            string checkpointSummaryJson = PrototypePersistenceService.SerializeRunSummary(checkpointSummary);
            PrototypeRuntimeSession resumed = CreateSession(initialize: false);
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(checkpoint)));
            PrototypeRunSummary restoredSummary = PrototypePersistenceService.DeserializeRunSummary(checkpointSummaryJson);
            resumed.RestoreArtifacts(PrototypePersistenceService.DeserializeEventLog(checkpointEvents), restoredSummary);
            Assert.Equal(checkpointSummaryJson, PrototypePersistenceService.SerializeRunSummary(restoredSummary));
            _ = direct.Advance(1.0f / 20.0f, 600.0f);
            _ = resumed.Advance(1.0f / 20.0f, 600.0f);
            PrototypeCognitionModule directModule = new(); PrototypeCognitionModule resumedModule = new();
            PrototypeCognitionObservation directObservation = directModule.PublishObservation(direct, direct.Workers[0].WorkerId);
            PrototypeCognitionObservation resumedObservation = resumedModule.PublishObservation(resumed, resumed.Workers[0].WorkerId);
            Assert.Equal(directObservation.PayloadUtf8, resumedObservation.PayloadUtf8);
            Assert.Equal(directObservation.CanonicalUtf8, resumedObservation.CanonicalUtf8);
            Assert.Equal(directObservation.StateId, resumedObservation.StateId);
            Assert.Equal(directObservation.PayloadDigest, resumedObservation.PayloadDigest);
            Assert.Equal(Assert.IsType<PrototypeCognitionProposal>(directModule.Resolve(direct, directObservation, PrototypeCognitionEvidence.Unavailable()).Proposal).CanonicalUtf8, Assert.IsType<PrototypeCognitionProposal>(resumedModule.Resolve(resumed, resumedObservation, PrototypeCognitionEvidence.Unavailable()).Proposal).CanonicalUtf8);
            ApplyFallback(direct); ApplyFallback(resumed);

            Assert.Equal(direct.CivicPolicy.PolicyId, resumed.CivicPolicy.PolicyId);
            Assert.Equal(direct.Wetland.ReedQuotaConsumed, resumed.Wetland.ReedQuotaConsumed);
            Assert.Equal(direct.Wetland.WetlandHealth, resumed.Wetland.WetlandHealth);
            Assert.Equal(direct.CaptureCitizenInterests().ToArray(), resumed.CaptureCitizenInterests().ToArray());
            Assert.Equal(PrototypePersistenceService.SerializeSnapshot(direct.CaptureSnapshot(Vector3.Zero)), PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(PrototypePersistenceService.SerializeEventLog(direct.EventLog), PrototypePersistenceService.SerializeEventLog(resumed.EventLog));
            Assert.Equal(Digest(PrototypePersistenceService.SerializeSnapshot(direct.CaptureSnapshot(Vector3.Zero))), Digest(PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero))));
            Assert.Equal(Digest(PrototypePersistenceService.SerializeEventLog(direct.EventLog)), Digest(PrototypePersistenceService.SerializeEventLog(resumed.EventLog)));
            PrototypeRunSummary directFinalSummary = PrototypeRunSummaryBuilder.Build(direct.CaptureSnapshot(Vector3.Zero), direct.EventLog.Entries, direct.RunStartHour);
            PrototypeRunSummary resumedFinalSummary = PrototypeRunSummaryBuilder.Build(resumed.CaptureSnapshot(Vector3.Zero), resumed.EventLog.Entries, resumed.RunStartHour);
            string directFinalSummaryJson = PrototypePersistenceService.SerializeRunSummary(directFinalSummary);
            string resumedFinalSummaryJson = PrototypePersistenceService.SerializeRunSummary(resumedFinalSummary);
            Assert.Equal(directFinalSummaryJson, resumedFinalSummaryJson);
            Assert.Equal(Digest(PrototypePersistenceService.SerializeSnapshot(direct.CaptureSnapshot(Vector3.Zero)) + PrototypePersistenceService.SerializeEventLog(direct.EventLog) + directFinalSummaryJson), Digest(PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)) + PrototypePersistenceService.SerializeEventLog(resumed.EventLog) + resumedFinalSummaryJson));
        }

        [Fact]
        public void NeutralPolicyObservationUsesTheExactTypedPolicyNotSelectedCode()
        {
            PrototypeCognitionException exception = Assert.Throws<PrototypeCognitionException>(() => new PrototypeCognitionModule().PublishObservation(CreateSession(), "citizen-001"));
            Assert.Equal("policy_not_selected", exception.Code);
        }

        [Fact]
        public void PublicApiIsExactlyAllowlisted()
        {
            Assert.Equal(new[] { "public:instance:.ctor()" }, PublicConstructors(typeof(PrototypeCognitionModule)));
            Assert.Equal(new[] { "public:instance:Apply(value Societies.Core.PrototypeRuntimeSession,value Societies.Core.PrototypeCognitionResolution):System.Boolean", "public:instance:PublishObservation(value Societies.Core.PrototypeRuntimeSession,value System.String):Societies.Core.PrototypeCognitionObservation", "public:instance:Resolve(value Societies.Core.PrototypeRuntimeSession,value Societies.Core.PrototypeCognitionObservation,value Societies.Core.PrototypeCognitionEvidence):Societies.Core.PrototypeCognitionResolution" }, PublicMethods(typeof(PrototypeCognitionModule)));
            Assert.Equal(new[] { "public:static:MaximumJsonDepth:System.Int32", "public:static:MaximumObservationBytes:System.Int32", "public:static:MaximumProposalBytes:System.Int32", "public:static:MaximumSummaryLength:System.Int32", "public:static:ObservationContractDigest:System.String", "public:static:ObservationSchema:System.String", "public:static:ProposalContractDigest:System.String", "public:static:ProposalSchema:System.String" }, PublicFields(typeof(PrototypeCognitionModule)));
            Assert.Empty(PublicMethods(typeof(PrototypeCognitionObservation))); Assert.Empty(PublicConstructors(typeof(PrototypeCognitionObservation))); Assert.Equal(new[] { "public:instance:CanonicalUtf8:System.Byte[]:get", "public:instance:CitizenId:System.String:get", "public:instance:PayloadDigest:System.String:get", "public:instance:PayloadUtf8:System.Byte[]:get", "public:instance:StateId:System.String:get", "public:instance:Tick:System.Int64:get" }, PublicProperties(typeof(PrototypeCognitionObservation)));
            Assert.Empty(PublicMethods(typeof(PrototypeCognitionProposal))); Assert.Empty(PublicConstructors(typeof(PrototypeCognitionProposal))); Assert.Equal(new[] { "public:instance:Action:System.String:get", "public:instance:CanonicalUtf8:System.Byte[]:get", "public:instance:CitizenId:System.String:get", "public:instance:ObservationDigest:System.String:get", "public:instance:ReasonCode:System.String:get", "public:instance:StateId:System.String:get", "public:instance:Summary:System.String:get" }, PublicProperties(typeof(PrototypeCognitionProposal)));
            Assert.Empty(PublicMethods(typeof(PrototypeCognitionResolution))); Assert.Empty(PublicConstructors(typeof(PrototypeCognitionResolution))); Assert.Equal(new[] { "public:instance:Accepted:System.Boolean:get", "public:instance:ErrorCode:System.String:get", "public:instance:Proposal:Societies.Core.PrototypeCognitionProposal:get", "public:instance:Source:System.Nullable<Societies.Core.PrototypeCognitionDecisionSource>:get" }, PublicProperties(typeof(PrototypeCognitionResolution)));
            Assert.Empty(PublicConstructors(typeof(PrototypeCognitionEvidence))); Assert.Equal(new[] { "public:static:Cancelled():Societies.Core.PrototypeCognitionEvidence", "public:static:Missing():Societies.Core.PrototypeCognitionEvidence", "public:static:Proposal(value System.ReadOnlySpan<System.Byte>):Societies.Core.PrototypeCognitionEvidence", "public:static:TimedOut():Societies.Core.PrototypeCognitionEvidence", "public:static:Unavailable():Societies.Core.PrototypeCognitionEvidence" }, PublicMethods(typeof(PrototypeCognitionEvidence))); Assert.Equal(new[] { "public:instance:Condition:Societies.Core.PrototypeCognitionEvidenceCondition:get" }, PublicProperties(typeof(PrototypeCognitionEvidence)));
            Assert.Empty(PublicConstructors(typeof(PrototypeCognitionException))); Assert.Empty(PublicMethods(typeof(PrototypeCognitionException))); Assert.Equal(new[] { "public:instance:Code:System.String:get" }, PublicProperties(typeof(PrototypeCognitionException)));
            Assert.Equal(new[] { "Proposal", "Missing", "Cancelled", "TimedOut", "Unavailable" }, Enum.GetNames<PrototypeCognitionEvidenceCondition>());
            Assert.Equal(new[] { "ValidatedProposal", "DeterministicFallback" }, Enum.GetNames<PrototypeCognitionDecisionSource>());
        }

        private static IEnumerable<(string, Func<PrototypeRuntimeSession, PrototypeCognitionModule, PrototypeCognitionObservation, byte[]>)> Adversaries()
        {
            yield return ("unsupported_version", (s, m, o) => Replace(Valid(s, m, o), "\"schemaVersion\":1", "\"schemaVersion\":2"));
            yield return ("truncated_valid", (s, m, o) => Valid(s, m, o)[..^1]);
            yield return ("missing", (s, m, o) => Utf8(RemoveSummary(ValidText(s, m, o))));
            yield return ("duplicate", (s, m, o) => Utf8(ValidText(s, m, o).Replace("\"action\":", "\"action\":\"support_policy\",\"action\":", StringComparison.Ordinal)));
            yield return ("reordered", (s, m, o) => Utf8(ValidText(s, m, o).Replace("\"schema\":\"societies_civic_cognition_proposal/v1\",\"schemaVersion\":1", "\"schemaVersion\":1,\"schema\":\"societies_civic_cognition_proposal/v1\"", StringComparison.Ordinal)));
            yield return ("unknown", (s, m, o) => Replace(Valid(s, m, o), "\"summary\"", "\"unknown\""));
            yield return ("oversized", (s, m, o) => Utf8(ValidText(s, m, o) + new string(' ', PrototypeCognitionModule.MaximumProposalBytes)));
            yield return ("deep", (s, m, o) => Utf8(ReplaceSummary(ValidText(s, m, o), "{\"x\":{\"y\":{\"z\":1}}}")));
            yield return ("trailing", (s, m, o) => Utf8(ValidText(s, m, o) + " "));
            yield return ("invalid_utf8", (s, m, o) => new byte[] { 0xff }.Concat(Valid(s, m, o).Skip(1)).ToArray());
            yield return ("noncanonical", (s, m, o) => Utf8(ValidText(s, m, o).Replace("societies_civic", "\\u0073ocieties_civic", StringComparison.Ordinal)));
            yield return ("wrong_citizen", (s, m, o) => Replace(Valid(s, m, o), s.Workers[0].WorkerId, s.Workers[1].WorkerId));
            yield return ("wrong_observation", (s, m, o) => Replace(Valid(s, m, o), o.PayloadDigest, new string('0', 64)));
            yield return ("wrong_state", (s, m, o) => Replace(Valid(s, m, o), o.StateId, new string('1', 64)));
            yield return ("illegal_action", (s, m, o) => Replace(Valid(s, m, o), "\"action\":\"" + Assert.IsType<PrototypeCognitionProposal>(m.Resolve(s, o, PrototypeCognitionEvidence.Missing()).Proposal).Action + "\"", "\"action\":\"not_allowed\""));
            yield return ("coherent_reason_incoherent_action", (s, m, o) => { PrototypeCognitionProposal fallback = Assert.IsType<PrototypeCognitionProposal>(m.Resolve(s, o, PrototypeCognitionEvidence.Missing()).Proposal); string other = fallback.Action == "support_policy" ? "oppose_policy" : "support_policy"; return Replace(Valid(s, m, o), "\"action\":\"" + fallback.Action + "\"", "\"action\":\"" + other + "\""); });
            yield return ("unknown_reason", (s, m, o) => Replace(Valid(s, m, o), "\"reasonCode\":\"" + Assert.IsType<PrototypeCognitionProposal>(m.Resolve(s, o, PrototypeCognitionEvidence.Missing()).Proposal).ReasonCode + "\"", "\"reasonCode\":\"unknown_reason\""));
            yield return ("wrong_summary", (s, m, o) => Utf8(ReplaceSummary(ValidText(s, m, o), "\"wrong\"")));
        }

        private static byte[] Valid(PrototypeRuntimeSession session, PrototypeCognitionModule module, PrototypeCognitionObservation observation)
        {
            PrototypeCognitionProposal fallback = Assert.IsType<PrototypeCognitionProposal>(module.Resolve(session, observation, PrototypeCognitionEvidence.Missing()).Proposal);
            return module.EncodeCanonicalProposal(observation, fallback.CitizenId, fallback.Action, fallback.ReasonCode, fallback.Summary);
        }
        private static string ValidText(PrototypeRuntimeSession session, PrototypeCognitionModule module, PrototypeCognitionObservation observation) => Encoding.UTF8.GetString(Valid(session, module, observation));
        private static string RemoveSummary(string input) => System.Text.RegularExpressions.Regex.Replace(input, "\\\"summary\\\":\\\"[^\\\"]*\\\",", string.Empty);
        private static string ReplaceSummary(string input, string replacement) => System.Text.RegularExpressions.Regex.Replace(input, "\\\"summary\\\":\\\"[^\\\"]*\\\"", "\\\"summary\\\":" + replacement);
        private static byte[] Replace(byte[] input, string oldValue, string newValue) => Utf8(Encoding.UTF8.GetString(input).Replace(oldValue, newValue, StringComparison.Ordinal));
        private static string[] PublicConstructors(Type type) => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(constructor => "public:instance:.ctor(" + string.Join(",", constructor.GetParameters().Select(FormatParameter)) + ")").OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string[] PublicMethods(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName).Select(method => "public:" + (method.IsStatic ? "static" : "instance") + ":" + method.Name + "(" + string.Join(",", method.GetParameters().Select(FormatParameter)) + "):" + FormatType(method.ReturnType)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string[] PublicFields(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(field => "public:" + (field.IsStatic ? "static" : "instance") + ":" + field.Name + ":" + FormatType(field.FieldType)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string[] PublicProperties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(property => "public:" + ((property.GetMethod ?? property.SetMethod)!.IsStatic ? "static" : "instance") + ":" + property.Name + ":" + FormatType(property.PropertyType) + ":" + (property.GetMethod != null ? "get" : string.Empty) + (property.SetMethod != null ? ",set" : string.Empty)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static string FormatParameter(ParameterInfo parameter) => (parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : "value ") + FormatType(parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType);
        private static string FormatType(Type type) => type.IsArray ? FormatType(type.GetElementType()!) + "[]" : type.IsGenericType ? type.GetGenericTypeDefinition().FullName![..type.GetGenericTypeDefinition().FullName!.IndexOf('`')] + "<" + string.Join(",", type.GetGenericArguments().Select(FormatType)) + ">" : type.FullName ?? type.Name;
        private static void ApplyFallback(PrototypeRuntimeSession session) { PrototypeCognitionModule module = new(); PrototypeCognitionObservation observation = module.PublishObservation(session, session.Workers[0].WorkerId); Assert.True(module.Apply(session, module.Resolve(session, observation, PrototypeCognitionEvidence.Unavailable()))); }
        private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Utf8(value))).ToLowerInvariant();
        private static byte[] Utf8(string value) => new UTF8Encoding(false, true).GetBytes(value);
        private static PrototypeRuntimeSession CreateSelectedSession(PrototypeCivicPolicy policy) { PrototypeRuntimeSession session = CreateSession(); Assert.True(session.SelectCivicPolicy(new PrototypeCivicPolicyCommand(policy, 0, 0)).Succeeded); return session; }
        private static PrototypeRuntimeSession CreateSession(bool initialize = true) { PrototypeCatalogBundle bundle = LoadCatalogs(); PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles, resourceDefinitions: bundle.Resources.Resources); if (initialize) session.Initialize(8.0f); return session; }
        private static PrototypeCatalogBundle LoadCatalogs() { string? current = AppContext.BaseDirectory; while (!string.IsNullOrWhiteSpace(current)) { string candidate = Path.Combine(current, "src", "societies", "data"); if (Directory.Exists(candidate)) return PrototypeCatalogLoader.LoadFromDirectory(candidate); current = Directory.GetParent(current)?.FullName; } throw new DirectoryNotFoundException(); }
    }
}
