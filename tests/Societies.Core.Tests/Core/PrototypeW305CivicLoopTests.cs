using Godot;
using Societies.Simulation;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Societies.Core.Tests
{
    /// <summary>
    /// W3-05 cross-slice proofs. These deliberately compose the previously isolated civic,
    /// wetland, cognition, persistence, and no-input contracts without changing runtime code.
    /// </summary>
    public sealed class PrototypeW305CivicLoopTests
    {
        [Theory]
        [InlineData(PrototypeCivicPolicy.ProtectWetland, 4, 75, 74, "support_policy")]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland, 12, 45, 43, "oppose_policy")]
        public void FixedCatalogSeed_PolicyInterestWetlandAndValidatedCognitionCompose(
            PrototypeCivicPolicy policy,
            int expectedQuota,
            int expectedSelectionHealth,
            int expectedHarvestHealth,
            string expectedAction)
        {
            PrototypeRuntimeSession session = CreateConflictedSession();
            Assert.True(session.SelectCivicPolicy(new(policy, ExpectedVersion: 0, IssuedTick: 0)).Succeeded);

            PrototypeCitizenInterest[] interests = session.CaptureCitizenInterests().ToArray();
            Assert.Contains(interests, interest => interest.WorkerId == "citizen-001" &&
                interest.Reason == PrototypeCitizenInterestReason.FutureReedSupply &&
                interest.Position == (policy == PrototypeCivicPolicy.ProtectWetland
                    ? PrototypeCitizenInterestPosition.Supports
                    : PrototypeCitizenInterestPosition.Opposes));
            Assert.Contains(interests, interest => interest.WorkerId == "citizen-002" &&
                interest.Reason == PrototypeCitizenInterestReason.ImmediateShelterSupply &&
                interest.Position == (policy == PrototypeCivicPolicy.DrawDownWetland
                    ? PrototypeCitizenInterestPosition.Supports
                    : PrototypeCitizenInterestPosition.Opposes));
            Assert.Equal(PrototypeCivicPolicyCatalog.GetId(policy), session.CivicPolicy.PolicyId);
            Assert.Equal(expectedQuota, session.Wetland.ReedQuotaLimit);
            Assert.Equal(expectedSelectionHealth, session.Wetland.WetlandHealth);

            PrototypeResourceSnapshot reeds = session.ResourceSnapshots.First(resource =>
                resource.ResourceId == PrototypeWetlandCatalog.ReedResourceId && resource.UnitsRemaining > 0);
            Assert.True(session.HarvestForPlayer(reeds.SiteId, 1).Succeeded);
            Assert.Equal(1, session.Wetland.ReedQuotaConsumed);
            Assert.Equal(expectedHarvestHealth, session.Wetland.WetlandHealth);

            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, "citizen-001");
            PrototypeCognitionProposal fallback = Assert.IsType<PrototypeCognitionProposal>(
                module.Resolve(session, observation, PrototypeCognitionEvidence.Missing()).Proposal);
            byte[] valid = module.EncodeCanonicalProposal(
                observation,
                fallback.CitizenId,
                expectedAction,
                fallback.ReasonCode,
                fallback.Summary);
            PrototypeCognitionResolution resolution = module.Resolve(
                session,
                observation,
                PrototypeCognitionEvidence.Proposal(valid));
            int policyEventsBefore = session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicPolicySelected);
            int cognitionEventsBefore = session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicCognitionDecision);

            Assert.True(resolution.Accepted);
            Assert.Equal(PrototypeCognitionDecisionSource.ValidatedProposal, resolution.Source);
            Assert.True(module.Apply(session, resolution));
            Assert.False(module.Apply(session, resolution));
            Assert.Equal(policyEventsBefore, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicPolicySelected));
            Assert.Equal(cognitionEventsBefore + 1, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicCognitionDecision));
            Assert.Contains("\"source\":\"validated_proposal\"", session.EventLog.Entries[^1].Message,
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void MissingInvalidAndClosedNoOutputPathsUseOneFallbackEventWithoutPolicyMutation(int condition)
        {
            PrototypeRuntimeSession session = CreateConflictedSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, "citizen-001");
            PrototypeCognitionEvidence evidence = condition switch
            {
                0 => PrototypeCognitionEvidence.Missing(),
                1 => PrototypeCognitionEvidence.Proposal(Encoding.UTF8.GetBytes("not-json")),
                2 => PrototypeCognitionEvidence.Cancelled(),
                3 => PrototypeCognitionEvidence.TimedOut(),
                _ => PrototypeCognitionEvidence.Unavailable()
            };
            string policyBefore = session.CivicPolicy.PolicyId;
            int policyEventsBefore = session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicPolicySelected);
            int cognitionEventsBefore = session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicCognitionDecision);

            PrototypeCognitionResolution resolution = module.Resolve(session, observation, evidence);

            Assert.True(resolution.Accepted);
            Assert.Equal(PrototypeCognitionDecisionSource.DeterministicFallback, resolution.Source);
            Assert.Equal(policyBefore, session.CivicPolicy.PolicyId);
            Assert.Equal(policyEventsBefore, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicPolicySelected));
            Assert.Equal(cognitionEventsBefore, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicCognitionDecision));
            Assert.True(module.Apply(session, resolution));
            Assert.False(module.Apply(session, resolution));
            Assert.Equal(policyBefore, session.CivicPolicy.PolicyId);
            Assert.Equal(policyEventsBefore, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicPolicySelected));
            Assert.Equal(cognitionEventsBefore + 1, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicCognitionDecision));
            Assert.Contains("\"source\":\"deterministic_fallback\"", session.EventLog.Entries[^1].Message,
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void StaleMalformedOversizedAndDeepEvidenceRemainInertUntilFallbackIsExplicitlyApplied(int condition)
        {
            PrototypeRuntimeSession session = CreateConflictedSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            PrototypeCognitionModule module = new();
            PrototypeCognitionObservation observation = module.PublishObservation(session, "citizen-001");
            byte[] invalid = condition switch
            {
                0 => Encoding.UTF8.GetBytes("{"),
                1 => new byte[PrototypeCognitionModule.MaximumProposalBytes + 1],
                2 => Encoding.UTF8.GetBytes("{\"a\":{\"b\":{\"c\":1}}}"),
                _ => ValidButIncoherent(session, module, observation)
            };
            string snapshotBefore = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            string eventsBefore = PrototypePersistenceService.SerializeEventLog(session.EventLog);

            PrototypeCognitionResolution invalidResolution = module.Resolve(
                session,
                observation,
                PrototypeCognitionEvidence.Proposal(invalid));

            Assert.True(invalidResolution.Accepted);
            Assert.Equal(PrototypeCognitionDecisionSource.DeterministicFallback, invalidResolution.Source);
            Assert.Equal(snapshotBefore, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBefore, PrototypePersistenceService.SerializeEventLog(session.EventLog));
            Assert.True(module.Apply(session, invalidResolution));

            PrototypeCognitionObservation stale = module.PublishObservation(session, "citizen-002");
            _ = session.Advance(1.0f / 20.0f, 600.0f);
            string staleSnapshot = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            string staleEvents = PrototypePersistenceService.SerializeEventLog(session.EventLog);
            PrototypeCognitionResolution staleResolution = module.Resolve(session, stale, PrototypeCognitionEvidence.Missing());
            Assert.False(staleResolution.Accepted);
            Assert.Equal("stale_observation", staleResolution.ErrorCode);
            Assert.False(module.Apply(session, staleResolution));
            Assert.Equal(staleSnapshot, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(staleEvents, PrototypePersistenceService.SerializeEventLog(session.EventLog));
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.ProtectWetland)]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland)]
        public void SchemaV9CheckpointResumeAndNoInputContinuationPreserveCivicCognitionIdentity(
            PrototypeCivicPolicy policy)
        {
            PrototypeRuntimeSession direct = CreateConflictedSession();
            Assert.True(direct.SelectCivicPolicy(new(policy, ExpectedVersion: 0, IssuedTick: 0)).Succeeded);
            PrototypeResourceSnapshot reeds = direct.ResourceSnapshots.First(resource =>
                resource.ResourceId == PrototypeWetlandCatalog.ReedResourceId && resource.UnitsRemaining > 0);
            Assert.True(direct.HarvestForPlayer(reeds.SiteId, 1).Succeeded);
            _ = direct.Advance(1.0f / 20.0f, 600.0f);
            PrototypeRuntimeSnapshot checkpoint = direct.CaptureSnapshot(Vector3.Zero);
            string checkpointEvents = PrototypePersistenceService.SerializeEventLog(direct.EventLog);
            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(checkpoint, direct.EventLog.Entries, direct.RunStartHour);
            string checkpointSummary = PrototypePersistenceService.SerializeRunSummary(summary);
            PrototypeRuntimeSession resumed = CreateConflictedSession();
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(checkpoint)));
            resumed.RestoreArtifacts(
                PrototypePersistenceService.DeserializeEventLog(checkpointEvents),
                PrototypePersistenceService.DeserializeRunSummary(checkpointSummary));

            ApplyUnavailableFallback(direct, "citizen-001");
            ApplyUnavailableFallback(resumed, "citizen-001");
            _ = direct.Advance(1.0f / 20.0f, 600.0f);
            _ = resumed.Advance(1.0f / 20.0f, 600.0f);

            string directSnapshot = PrototypePersistenceService.SerializeSnapshot(direct.CaptureSnapshot(Vector3.Zero));
            string resumedSnapshot = PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero));
            string directEvents = PrototypePersistenceService.SerializeEventLog(direct.EventLog);
            string resumedEvents = PrototypePersistenceService.SerializeEventLog(resumed.EventLog);
            Assert.Equal(9, direct.CaptureSnapshot(Vector3.Zero).SchemaVersion);
            Assert.Equal(direct.CivicPolicy.PolicyId, resumed.CivicPolicy.PolicyId);
            Assert.Equal(direct.Wetland.WetlandHealth, resumed.Wetland.WetlandHealth);
            Assert.Equal(direct.CaptureCitizenInterests().ToArray(), resumed.CaptureCitizenInterests().ToArray());
            Assert.Equal(directSnapshot, resumedSnapshot);
            Assert.Equal(directEvents, resumedEvents);
            Assert.Equal(Digest(directSnapshot + directEvents), Digest(resumedSnapshot + resumedEvents));
        }

        [Fact]
        public void FixedCatalogSeed_NoInputOfflineBaselineContinuesWithoutCivicOrCognitionEvents()
        {
            PrototypeRuntimeSession first = CreateConflictedSession();
            PrototypeRuntimeSession second = CreateConflictedSession();
            _ = first.Advance(1.0f / 20.0f, 600.0f);
            _ = second.Advance(1.0f / 20.0f, 600.0f);

            string firstSnapshot = PrototypePersistenceService.SerializeSnapshot(first.CaptureSnapshot(Vector3.Zero));
            string secondSnapshot = PrototypePersistenceService.SerializeSnapshot(second.CaptureSnapshot(Vector3.Zero));
            Assert.Equal("neutral", first.CivicPolicy.PolicyId);
            Assert.Equal("neutral", first.Wetland.PolicyId);
            Assert.DoesNotContain(first.EventLog.Entries, entry => entry.EventType.StartsWith("civic.", StringComparison.Ordinal));
            Assert.Equal(firstSnapshot, secondSnapshot);
            Assert.Equal(Digest(firstSnapshot), Digest(secondSnapshot));
        }

        private static byte[] ValidButIncoherent(
            PrototypeRuntimeSession session,
            PrototypeCognitionModule module,
            PrototypeCognitionObservation observation)
        {
            PrototypeCognitionProposal fallback = Assert.IsType<PrototypeCognitionProposal>(
                module.Resolve(session, observation, PrototypeCognitionEvidence.Missing()).Proposal);
            return module.EncodeCanonicalProposal(
                observation,
                fallback.CitizenId,
                fallback.Action == "support_policy" ? "oppose_policy" : "support_policy",
                fallback.ReasonCode,
                fallback.Summary);
        }

        private static void ApplyUnavailableFallback(PrototypeRuntimeSession session, string citizenId)
        {
            PrototypeCognitionModule module = new();
            PrototypeCognitionResolution resolution = module.Resolve(
                session,
                module.PublishObservation(session, citizenId),
                PrototypeCognitionEvidence.Unavailable());
            Assert.True(resolution.Accepted);
            Assert.Equal(PrototypeCognitionDecisionSource.DeterministicFallback, resolution.Source);
            Assert.True(module.Apply(session, resolution));
        }

        private static PrototypeRuntimeSession CreateConflictedSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(
                bundle.Scenarios.Resolve("balanced_basin"),
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);

            session.Workers[0].WorkerId = "citizen-001";
            session.Workers[0].Role = PrototypeCitizenRole.Forager;
            session.Workers[0].Needs.Nutrition = 100.0f;
            session.Workers[0].Needs.Fatigue = 0.0f;
            session.Workers[1].WorkerId = "citizen-002";
            session.Workers[1].Role = PrototypeCitizenRole.Builder;
            session.Workers[1].Needs.Nutrition = 100.0f;
            session.Workers[1].Needs.Fatigue = 0.0f;
            return session;
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }

        private static string Digest(string value) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
