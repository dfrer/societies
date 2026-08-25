using Societies.Simulation;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Societies.Core
{
    public sealed class PrototypeCognitionModule
    {
        private readonly Guid _resolutionCapability = Guid.NewGuid();
        public const string ObservationSchema = "societies_civic_cognition_observation/v1";
        public const string ProposalSchema = "societies_civic_cognition_proposal/v1";
        public const int MaximumObservationBytes = 1_024;
        public const int MaximumProposalBytes = 1_024;
        public const int MaximumJsonDepth = 2;
        public const int MaximumSummaryLength = PrototypeCitizenInterestEvaluator.MaximumSummaryLength;
        public const string ObservationContractDigest = "98bee0f1cee7b8f2e4dd6b6850ffeb80cc996cd316f2006f9b0b04dae4d64864";
        public const string ProposalContractDigest = "562beb9b05aa62ffbb147d9d43f8b0aff41ea61d572ba24f31c08b46b505b12d";

        internal const string SharedContractDefinition = "schemas=observation:societies_civic_cognition_observation/v1,proposal:societies_civic_cognition_proposal/v1;schemaVersion=1|utf8=strict-no-bom|jsonDepth=2|observationBytes=1024|proposalBytes=1024|eventBytes=1024|citizenId=[A-Za-z0-9._-]{1,128}|actionString=1..32-ascii-printable-no-quote-backslash|reasonString=1..64-ascii-printable-no-quote-backslash|summary=1..64-ascii-printable-no-quote-backslash|digest=sha256-lowerhex-64|actions=support_policy,oppose_policy,request_reconsideration|sources=validated_proposal,deterministic_fallback|sourceWire=ValidatedProposal:validated_proposal,DeterministicFallback:deterministic_fallback|evidence=proposal,missing,cancelled,timed_out,unavailable|evidenceMatrix=proposal:valid->validated_proposal;proposal:missing-or-invalid->deterministic_fallback;missing:null->deterministic_fallback;cancelled:null->deterministic_fallback;timed_out:null->deterministic_fallback;unavailable:null->deterministic_fallback;nonproposal:bytes->no_output_contains_bytes;undefined->invalid_evidence_condition|coherence=supports->support_policy;opposes->oppose_policy-or-request_reconsideration;uncommitted->reject|fallback=supports->support_policy;opposes->oppose_policy;fallback-never-request_reconsideration|eventType=civic.cognition.decision|eventSchema={source,citizenId,action,reasonCode,summary,observationDigest,stateId};eventFieldOrder=source,citizenId,action,reasonCode,summary,observationDigest,stateId|errors=invalid_evidence_condition,policy_not_selected,invalid_policy_state,invalid_citizen_id,unknown_citizen,duplicate_citizen_id,invalid_role,invalid_needs,invalid_nutrition,invalid_fatigue,invalid_citizen_interest,invalid_wetland_state,observation_too_large,stale_observation,no_output_contains_bytes,invalid_interest_position,fallback_validation_failed,invalid_nutrition_band,invalid_fatigue_band";
        internal const string ObservationContractDefinition = "observation/v1|fields=schema:string,schemaVersion:number,citizenId:string,tick:number,stateId:sha256-lower,nutritionBand:closed,fatigueBand:closed,role:closed,reasonCode:closed,policy:closed,wetlandHealthBand:closed,remainingReedQuota:number,allowedActions:array-ordered,payloadDigest:sha256-lower|fieldOrder=schema,schemaVersion,citizenId,tick,stateId,nutritionBand,fatigueBand,role,reasonCode,policy,wetlandHealthBand,remainingReedQuota,allowedActions,payloadDigest|payloadDigestScope=SHA256-lowerhex-over-exact-canonical-observation-UTF8-through-allowedActions-excluding-payloadDigest|stateIdScope=SHA256-lowerhex-over-UTF8-template:state/v1|tick={tick}|citizen={citizen}|nutritionBits={nutritionBits}|fatigueBits={fatigueBits}|role={role}|reason={reason}|policy={policy}|policyTick={policyTick}|policyVersion={policyVersion}|health={health}|healthBand={healthBand}|remaining={remaining};numericEncoding=CultureInfo.InvariantCulture-signed-decimal;nutritionBits=fixed-SingleToInt32Bits;fatigueBits=fixed-SingleToInt32Bits|bands=nutrition:critical,food_insecure,secure;fatigue:exhausted,needs_recovery,rested;wetland:degraded,strained,healthy|roles=forager,generalist,builder,logger,mason,hauler,processor|policies=protect_wetland,draw_down_wetland|secureRoleReason=forager:future_reed_supply;generalist:balanced_long_term_supply;builder:immediate_shelter_supply;logger:immediate_material_supply;mason:immediate_material_supply;hauler:material_throughput;processor:material_throughput|preferred=critical_nutrition:draw_down_wetland;critical_fatigue:draw_down_wetland;food_security:draw_down_wetland;recovery_need:draw_down_wetland;future_reed_supply:protect_wetland;balanced_long_term_supply:protect_wetland;immediate_shelter_supply:draw_down_wetland;immediate_material_supply:draw_down_wetland;material_throughput:draw_down_wetland|thresholds=nutrition<=12:critical_nutrition;fatigue>=90:critical_fatigue;nutrition<=45:food_security;fatigue>=62:recovery_need|summaries=critical_nutrition:nutrition=critical;critical_fatigue:fatigue=exhausted;food_security:nutrition=food_insecure;recovery_need:fatigue=needs_recovery;roles:role={role}|" + SharedContractDefinition;
        internal const string ProposalContractDefinition = "proposal/v1|observationIdentity=" + ObservationContractDigest + "|fields=schema:string,schemaVersion:number,citizenId:string,action:string,reasonCode:string,summary:string,observationDigest:sha256-lower,stateId:sha256-lower|fieldOrder=schema,schemaVersion,citizenId,action,reasonCode,summary,observationDigest,stateId|validation=exact-current-observation-and-state,exact-current-evaluator-reason-and-summary,strict-canonical-byte-equality,no-duplicate-or-unknown-or-trailing-fields|eventType=civic.cognition.decision|" + SharedContractDefinition;

        public PrototypeCognitionObservation PublishObservation(PrototypeRuntimeSession session, string citizenId)
        {
            ArgumentNullException.ThrowIfNull(session);
            RequireCanonicalCitizenId(citizenId);
            PrototypeCivicPolicy policy = GetSelectedPolicy(session.CivicPolicy);
            PrototypeWorkerState worker = FindWorker(session, citizenId);
            ValidateWorkerFacts(worker);
            PrototypeCitizenInterest interest;
            try { interest = PrototypeCitizenInterestEvaluator.Evaluate(worker, policy); }
            catch (ArgumentException) { throw new PrototypeCognitionException("invalid_citizen_interest"); }
            PrototypeWetlandSnapshot wetland = session.Wetland;
            ValidateWetlandFacts(wetland, policy);
            int remaining = checked(wetland.ReedQuotaLimit - wetland.ReedQuotaConsumed);
            if (remaining < 0) throw new PrototypeCognitionException("invalid_wetland_state");
            string stateId = BuildStateId(session, worker, interest, policy, wetland, remaining);
            byte[] payload = Utf8(string.Create(CultureInfo.InvariantCulture, $"{{\"schema\":\"{ObservationSchema}\",\"schemaVersion\":1,\"citizenId\":\"{citizenId}\",\"tick\":{session.SimulationTick},\"stateId\":\"{stateId}\",\"nutritionBand\":\"{NutritionBandId(interest.NutritionBand)}\",\"fatigueBand\":\"{FatigueBandId(interest.FatigueBand)}\",\"role\":\"{interest.RoleId}\",\"reasonCode\":\"{PrototypeCitizenInterestEvaluator.GetReasonCode(interest.Reason)}\",\"policy\":\"{PrototypeCivicPolicyCatalog.GetId(policy)}\",\"wetlandHealthBand\":\"{wetland.WetlandHealthBand}\",\"remainingReedQuota\":{remaining},\"allowedActions\":[\"support_policy\",\"oppose_policy\",\"request_reconsideration\"]}}"));
            string digest = Sha256(payload);
            byte[] canonical = Utf8(Encoding.UTF8.GetString(payload)[..^1] + $",\"payloadDigest\":\"{digest}\"}}");
            if (canonical.Length > MaximumObservationBytes) throw new PrototypeCognitionException("observation_too_large");
            return new PrototypeCognitionObservation(citizenId, session.SimulationTick, stateId, digest, payload, canonical);
        }

        public PrototypeCognitionResolution Resolve(PrototypeRuntimeSession session, PrototypeCognitionObservation observation, PrototypeCognitionEvidence evidence)
        {
            ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(observation); ArgumentNullException.ThrowIfNull(evidence);
            if (!Enum.IsDefined(typeof(PrototypeCognitionEvidenceCondition), evidence.Condition)) return Reject("invalid_evidence_condition");
            PrototypeCognitionObservation current;
            try { current = PublishObservation(session, observation.CitizenId); }
            catch (PrototypeCognitionException exception) { return Reject(exception.Code); }
            if (!ObservationMatches(observation, current)) return Reject("stale_observation");
            byte[]? raw = evidence.CopyRawProposal();
            if (evidence.Condition == PrototypeCognitionEvidenceCondition.Proposal)
            {
                if (raw != null && TryValidateProposal(session, current, raw, out PrototypeCognitionProposal? proposal)) return Accept(PrototypeCognitionDecisionSource.ValidatedProposal, proposal!);
                return ResolveFallback(session, current);
            }
            return raw == null ? ResolveFallback(session, current) : Reject("no_output_contains_bytes");
        }

        public bool Apply(PrototypeRuntimeSession session, PrototypeCognitionResolution resolution)
        {
            ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(resolution);
            if (!resolution.TryConsume(_resolutionCapability, out PrototypeCognitionDecisionSource source, out PrototypeCognitionProposal? ownedProposal)) return false;
            try
            {
                PrototypeCognitionObservation observation = PublishObservation(session, ownedProposal!.CitizenId);
                if (!TryValidateProposal(session, observation, ownedProposal.CopyCanonicalUtf8(), out PrototypeCognitionProposal? checkedProposal)) return false;
                session.RecordCivicCognitionDecision(source, checkedProposal!);
                return true;
            }
            catch (PrototypeCognitionException) { return false; }
        }

        internal byte[] EncodeCanonicalProposal(PrototypeCognitionObservation observation, string citizenId, string action, string reasonCode, string summary) => EncodeProposal(citizenId, action, reasonCode, summary, observation.PayloadDigest, observation.StateId);

        internal static string BuildEventMessage(PrototypeCognitionDecisionSource source, PrototypeCognitionProposal proposal)
        {
            string sourceId = source switch { PrototypeCognitionDecisionSource.ValidatedProposal => "validated_proposal", PrototypeCognitionDecisionSource.DeterministicFallback => "deterministic_fallback", _ => throw new ArgumentOutOfRangeException(nameof(source)) };
            return Encoding.UTF8.GetString(Utf8($"{{\"source\":\"{sourceId}\",\"citizenId\":\"{proposal.CitizenId}\",\"action\":\"{proposal.Action}\",\"reasonCode\":\"{proposal.ReasonCode}\",\"summary\":\"{proposal.Summary}\",\"observationDigest\":\"{proposal.ObservationDigest}\",\"stateId\":\"{proposal.StateId}\"}}"));
        }

        private PrototypeCognitionResolution ResolveFallback(PrototypeRuntimeSession session, PrototypeCognitionObservation observation)
        {
            try
            {
                PrototypeCitizenInterest interest = PrototypeCitizenInterestEvaluator.Evaluate(FindWorker(session, observation.CitizenId), GetSelectedPolicy(session.CivicPolicy));
                string action = interest.Position switch { PrototypeCitizenInterestPosition.Supports => "support_policy", PrototypeCitizenInterestPosition.Opposes => "oppose_policy", _ => throw new PrototypeCognitionException("invalid_interest_position") };
                byte[] raw = EncodeProposal(observation.CitizenId, action, PrototypeCitizenInterestEvaluator.GetReasonCode(interest.Reason), interest.Summary, observation.PayloadDigest, observation.StateId);
                return TryValidateProposal(session, observation, raw, out PrototypeCognitionProposal? proposal) ? Accept(PrototypeCognitionDecisionSource.DeterministicFallback, proposal!) : Reject("fallback_validation_failed");
            }
            catch (PrototypeCognitionException exception) { return Reject(exception.Code); }
        }

        private PrototypeCognitionResolution Accept(PrototypeCognitionDecisionSource source, PrototypeCognitionProposal proposal) => new(_resolutionCapability, source, proposal);
        private static PrototypeCognitionResolution Reject(string errorCode) => new(errorCode);

        private static bool TryValidateProposal(PrototypeRuntimeSession session, PrototypeCognitionObservation observation, byte[] raw, out PrototypeCognitionProposal? proposal)
        {
            proposal = null;
            if (!TryParseProposalJson(raw, out JsonDocument? document)) return false;
            try
            {
                using (document)
                {
                    JsonProperty[] fields = document!.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.EnumerateObject().ToArray() : Array.Empty<JsonProperty>();
                    string[] names = { "schema", "schemaVersion", "citizenId", "action", "reasonCode", "summary", "observationDigest", "stateId" };
                    if (fields.Length != names.Length || fields.Where((field, index) => field.Name != names[index]).Any() || fields[0].Value.ValueKind != JsonValueKind.String || fields[0].Value.GetString() != ProposalSchema || fields[1].Value.ValueKind != JsonValueKind.Number || !fields[1].Value.TryGetInt32(out int version) || version != 1 || !TryCanonicalId(fields[2].Value, out string citizenId) || !TryAscii(fields[3].Value, 32, out string action) || !TryAscii(fields[4].Value, 64, out string reasonCode) || !TryAscii(fields[5].Value, MaximumSummaryLength, out string summary) || !TryDigest(fields[6].Value, out string observationDigest) || !TryDigest(fields[7].Value, out string stateId)) return false;
                    byte[] canonical = EncodeProposal(citizenId, action, reasonCode, summary, observationDigest, stateId);
                    if (canonical.Length == 0 || !raw.AsSpan().SequenceEqual(canonical) || citizenId != observation.CitizenId || observationDigest != observation.PayloadDigest || stateId != observation.StateId) return false;
                    PrototypeCitizenInterest interest = PrototypeCitizenInterestEvaluator.Evaluate(FindWorker(session, citizenId), GetSelectedPolicy(session.CivicPolicy));
                    if (!IsKnownAction(action) || reasonCode != PrototypeCitizenInterestEvaluator.GetReasonCode(interest.Reason) || summary != interest.Summary || !IsCoherent(action, interest.Position)) return false;
                    proposal = new PrototypeCognitionProposal(citizenId, action, reasonCode, summary, observationDigest, stateId, canonical); return true;
                }
            }
            catch (PrototypeCognitionException) { return false; } catch (ArgumentException) { return false; }
        }

        private static byte[] EncodeProposal(string citizenId, string action, string reasonCode, string summary, string observationDigest, string stateId) => IsCanonicalCitizenId(citizenId) && IsAscii(action, 32) && IsAscii(reasonCode, 64) && IsAscii(summary, MaximumSummaryLength) && IsDigest(observationDigest) && IsDigest(stateId) ? Utf8($"{{\"schema\":\"{ProposalSchema}\",\"schemaVersion\":1,\"citizenId\":\"{citizenId}\",\"action\":\"{action}\",\"reasonCode\":\"{reasonCode}\",\"summary\":\"{summary}\",\"observationDigest\":\"{observationDigest}\",\"stateId\":\"{stateId}\"}}") : Array.Empty<byte>();
        private static PrototypeCivicPolicy GetSelectedPolicy(PrototypeCivicPolicySnapshot snapshot) { try { PrototypeCivicPolicy policy = PrototypeCivicPolicyCatalog.ParseId(snapshot.PolicyId); if (policy == PrototypeCivicPolicy.Neutral || snapshot.SelectedTick == null || snapshot.Version != 1) throw new PrototypeCognitionException("policy_not_selected"); return policy; } catch (ArgumentException) { throw new PrototypeCognitionException("invalid_policy_state"); } }
        private static PrototypeWorkerState FindWorker(PrototypeRuntimeSession session, string citizenId) { RequireCanonicalCitizenId(citizenId); PrototypeWorkerState[] matches = session.Workers.Where(worker => worker.WorkerId == citizenId).ToArray(); return matches.Length switch { 1 => matches[0], 0 => throw new PrototypeCognitionException("unknown_citizen"), _ => throw new PrototypeCognitionException("duplicate_citizen_id") }; }
        private static void ValidateWorkerFacts(PrototypeWorkerState worker) { if (worker.Needs == null) throw new PrototypeCognitionException("invalid_needs"); if (!Enum.IsDefined(typeof(PrototypeCitizenRole), worker.Role)) throw new PrototypeCognitionException("invalid_role"); if (!float.IsFinite(worker.Needs.Nutrition) || worker.Needs.Nutrition < 0 || worker.Needs.Nutrition > 100) throw new PrototypeCognitionException("invalid_nutrition"); if (!float.IsFinite(worker.Needs.Fatigue) || worker.Needs.Fatigue < 0 || worker.Needs.Fatigue > 100) throw new PrototypeCognitionException("invalid_fatigue"); }
        private static void ValidateWetlandFacts(PrototypeWetlandSnapshot wetland, PrototypeCivicPolicy policy) { if (wetland.PolicyId != PrototypeCivicPolicyCatalog.GetId(policy) || wetland.PolicySelectedTick == null || wetland.PolicyVersion != 1 || wetland.ReedQuotaLimit < 0 || wetland.ReedQuotaConsumed < 0 || wetland.ReedQuotaConsumed > wetland.ReedQuotaLimit || wetland.WetlandHealth is < PrototypeWetlandCatalog.MinimumHealth or > PrototypeWetlandCatalog.MaximumHealth || wetland.WetlandHealthBand != PrototypeWetlandCatalog.GetHealthBandId(PrototypeWetlandCatalog.GetHealthBand(wetland.WetlandHealth))) throw new PrototypeCognitionException("invalid_wetland_state"); }
        private static string BuildStateId(PrototypeRuntimeSession session, PrototypeWorkerState worker, PrototypeCitizenInterest interest, PrototypeCivicPolicy policy, PrototypeWetlandSnapshot wetland, int remaining) => Sha256(Utf8(string.Create(CultureInfo.InvariantCulture, $"state/v1|tick={session.SimulationTick}|citizen={worker.WorkerId}|nutritionBits={BitConverter.SingleToInt32Bits(worker.Needs.Nutrition)}|fatigueBits={BitConverter.SingleToInt32Bits(worker.Needs.Fatigue)}|role={interest.RoleId}|reason={PrototypeCitizenInterestEvaluator.GetReasonCode(interest.Reason)}|policy={PrototypeCivicPolicyCatalog.GetId(policy)}|policyTick={wetland.PolicySelectedTick!.Value}|policyVersion={wetland.PolicyVersion}|health={wetland.WetlandHealth}|healthBand={wetland.WetlandHealthBand}|remaining={remaining}")));
        private static bool ObservationMatches(PrototypeCognitionObservation left, PrototypeCognitionObservation right) => left.CitizenId == right.CitizenId && left.Tick == right.Tick && left.StateId == right.StateId && left.PayloadDigest == right.PayloadDigest && left.CopyPayloadUtf8().AsSpan().SequenceEqual(right.CopyPayloadUtf8()) && left.CopyCanonicalUtf8().AsSpan().SequenceEqual(right.CopyCanonicalUtf8());
        private static bool IsKnownAction(string action) => action is "support_policy" or "oppose_policy" or "request_reconsideration";
        private static bool IsCoherent(string action, PrototypeCitizenInterestPosition position) => position switch { PrototypeCitizenInterestPosition.Supports => action == "support_policy", PrototypeCitizenInterestPosition.Opposes => action is "oppose_policy" or "request_reconsideration", _ => false };
        private static void RequireCanonicalCitizenId(string value) { if (!IsCanonicalCitizenId(value)) throw new PrototypeCognitionException("invalid_citizen_id"); }
        private static bool IsCanonicalCitizenId(string? value) => value is { Length: > 0 and <= PrototypeRunArtifactManager.MaximumIdentifierLength } && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
        private static bool TryCanonicalId(JsonElement value, out string result) { result = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty; return IsCanonicalCitizenId(result); }
        private static bool TryAscii(JsonElement value, int max, out string result) { result = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty; return IsAscii(result, max); }
        private static bool IsAscii(string? value, int max) => value is { Length: > 0 } && value.Length <= max && value.All(character => character is >= ' ' and <= '~' && character != '"' && character != '\\');
        private static bool TryDigest(JsonElement value, out string result) { result = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty; return IsDigest(result); }
        private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        private static bool TryDecodeStrictUtf8(byte[] bytes, out string text) { try { text = new UTF8Encoding(false, true).GetString(bytes); return true; } catch (DecoderFallbackException) { text = string.Empty; return false; } }
        private static bool TryParseProposalJson(byte[] bytes, out JsonDocument? document) { document = null; if (bytes.Length == 0 || bytes.Length > MaximumProposalBytes || !TryDecodeStrictUtf8(bytes, out string text)) return false; try { document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaximumJsonDepth }); return true; } catch (JsonException) { return false; } }
        internal static bool IsWithinMaximumJsonDepthForTests(byte[] bytes) { if (!TryParseProposalJson(bytes, out JsonDocument? document)) return false; using (document) return true; }
        private static string NutritionBandId(PrototypeCitizenNutritionBand band) => band switch { PrototypeCitizenNutritionBand.Critical => "critical", PrototypeCitizenNutritionBand.FoodInsecure => "food_insecure", PrototypeCitizenNutritionBand.Secure => "secure", _ => throw new PrototypeCognitionException("invalid_nutrition_band") };
        private static string FatigueBandId(PrototypeCitizenFatigueBand band) => band switch { PrototypeCitizenFatigueBand.Exhausted => "exhausted", PrototypeCitizenFatigueBand.NeedsRecovery => "needs_recovery", PrototypeCitizenFatigueBand.Rested => "rested", _ => throw new PrototypeCognitionException("invalid_fatigue_band") };
        private static byte[] Utf8(string value) => new UTF8Encoding(false, true).GetBytes(value);
        private static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    }

    public enum PrototypeCognitionEvidenceCondition { Proposal, Missing, Cancelled, TimedOut, Unavailable }
    public enum PrototypeCognitionDecisionSource { ValidatedProposal, DeterministicFallback }
    public sealed class PrototypeCognitionEvidence { private readonly byte[]? _raw; internal PrototypeCognitionEvidence(PrototypeCognitionEvidenceCondition condition, byte[]? raw) { Condition = condition; _raw = raw?.ToArray(); } public PrototypeCognitionEvidenceCondition Condition { get; } public static PrototypeCognitionEvidence Proposal(ReadOnlySpan<byte> raw) => new(PrototypeCognitionEvidenceCondition.Proposal, raw.ToArray()); public static PrototypeCognitionEvidence Missing() => new(PrototypeCognitionEvidenceCondition.Missing, null); public static PrototypeCognitionEvidence Cancelled() => new(PrototypeCognitionEvidenceCondition.Cancelled, null); public static PrototypeCognitionEvidence TimedOut() => new(PrototypeCognitionEvidenceCondition.TimedOut, null); public static PrototypeCognitionEvidence Unavailable() => new(PrototypeCognitionEvidenceCondition.Unavailable, null); internal byte[]? CopyRawProposal() => _raw?.ToArray(); }
    public sealed class PrototypeCognitionObservation { private readonly byte[] _payload; private readonly byte[] _canonical; internal PrototypeCognitionObservation(string citizenId, long tick, string stateId, string digest, byte[] payload, byte[] canonical) { CitizenId = citizenId; Tick = tick; StateId = stateId; PayloadDigest = digest; _payload = payload.ToArray(); _canonical = canonical.ToArray(); } public string CitizenId { get; } public long Tick { get; } public string StateId { get; } public string PayloadDigest { get; } public byte[] PayloadUtf8 => _payload.ToArray(); public byte[] CanonicalUtf8 => _canonical.ToArray(); internal byte[] CopyPayloadUtf8() => _payload.ToArray(); internal byte[] CopyCanonicalUtf8() => _canonical.ToArray(); }
    public sealed class PrototypeCognitionProposal { private readonly byte[] _canonical; internal PrototypeCognitionProposal(string citizenId, string action, string reason, string summary, string observationDigest, string stateId, byte[] canonical) { CitizenId = citizenId; Action = action; ReasonCode = reason; Summary = summary; ObservationDigest = observationDigest; StateId = stateId; _canonical = canonical.ToArray(); } public string CitizenId { get; } public string Action { get; } public string ReasonCode { get; } public string Summary { get; } public string ObservationDigest { get; } public string StateId { get; } public byte[] CanonicalUtf8 => _canonical.ToArray(); internal byte[] CopyCanonicalUtf8() => _canonical.ToArray(); }
    public sealed class PrototypeCognitionResolution { private readonly Guid _capability; private readonly PrototypeCognitionDecisionSource? _source; private readonly PrototypeCognitionProposal? _proposal; private int _consumed; internal PrototypeCognitionResolution(Guid capability, PrototypeCognitionDecisionSource source, PrototypeCognitionProposal proposal) { _capability = capability; _source = source; _proposal = proposal; ErrorCode = string.Empty; } internal PrototypeCognitionResolution(string errorCode) => ErrorCode = errorCode; public bool Accepted => _source.HasValue && _proposal != null; public PrototypeCognitionDecisionSource? Source => _source; public PrototypeCognitionProposal? Proposal => _proposal == null ? null : new PrototypeCognitionProposal(_proposal.CitizenId, _proposal.Action, _proposal.ReasonCode, _proposal.Summary, _proposal.ObservationDigest, _proposal.StateId, _proposal.CopyCanonicalUtf8()); public string ErrorCode { get; } internal bool TryConsume(Guid capability, out PrototypeCognitionDecisionSource source, out PrototypeCognitionProposal? proposal) { source = default; proposal = null; if (!Accepted || capability != _capability || Interlocked.CompareExchange(ref _consumed, 1, 0) != 0) return false; source = _source!.Value; proposal = _proposal!; return true; } }
    public sealed class PrototypeCognitionException : Exception { internal PrototypeCognitionException(string code) : base(code) => Code = code; public string Code { get; } }
}
