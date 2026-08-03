using Godot;
using Societies.Simulation;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeWetlandConsequenceTests
    {
        [Theory]
        [InlineData(0, "degraded")]
        [InlineData(39, "degraded")]
        [InlineData(40, "strained")]
        [InlineData(69, "strained")]
        [InlineData(70, "healthy")]
        [InlineData(100, "healthy")]
        public void HealthBandsUseInclusiveFrozenBoundaries(int health, string expectedBand)
        {
            Assert.Equal(
                expectedBand,
                PrototypeWetlandCatalog.GetHealthBandId(
                    PrototypeWetlandCatalog.GetHealthBand(health)));
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.ProtectWetland, 4, 75, "healthy", 15, -1)]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland, 12, 45, "strained", -15, -2)]
        public void PolicySelectionAppliesExactQuotaHealthAndCanonicalEventOrder(
            PrototypeCivicPolicy policy,
            int expectedLimit,
            int expectedHealth,
            string expectedBand,
            int expectedSelectionDelta,
            int expectedHarvestDelta)
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.Equal(60, session.Wetland.WetlandHealth);
            Assert.Equal(0, session.Wetland.ReedQuotaLimit);

            Assert.True(session.SelectCivicPolicy(new(policy, 0, 0)).Succeeded);

            PrototypeWetlandSnapshot wetland = session.Wetland;
            Assert.Equal(PrototypeCivicPolicyCatalog.GetId(policy), wetland.PolicyId);
            Assert.Equal(0, wetland.PolicySelectedTick);
            Assert.Equal(1, wetland.PolicyVersion);
            Assert.Equal(expectedLimit, wetland.ReedQuotaLimit);
            Assert.Equal(0, wetland.ReedQuotaConsumed);
            Assert.Equal(expectedHealth, wetland.WetlandHealth);
            Assert.Equal(expectedBand, wetland.WetlandHealthBand);
            Assert.Equal(expectedSelectionDelta, expectedHealth - PrototypeWetlandCatalog.NeutralHealth);
            Assert.Equal(expectedHarvestDelta, PrototypeWetlandCatalog.GetHarvestHealthDelta(policy));
            Assert.Equal(
                new[]
                {
                    PrototypeEventTypes.CivicPolicySelected,
                    PrototypeEventTypes.CivicPreferenceSummary,
                    PrototypeEventTypes.CivicWetlandQuotaApplied,
                    PrototypeEventTypes.CivicWetlandTransition
                },
                session.EventLog.Entries.Select(entry => entry.EventType));
            Assert.Equal(
                PrototypeWetlandCatalog.BuildQuotaAppliedMessage(wetland),
                session.EventLog.Entries[2].Message);
            Assert.Equal(
                PrototypeWetlandCatalog.BuildTransitionMessage(
                    "policy_selection",
                    60,
                    PrototypeWetlandHealthBand.Strained,
                    wetland),
                session.EventLog.Entries[3].Message);
            Assert.All(session.EventLog.Entries, entry => Assert.Equal(0, entry.Tick));
        }

        [Fact]
        public void PlayerReedQuotaExhaustionIsAtomicAndNonReedsRemainUnchanged()
        {
            PrototypeRuntimeSession session = CreateSession();
            PrototypeResourceSnapshot initialReed = FindAvailableSite(session, "reeds");
            long neutralRevision = session.ResourceRevision;
            Assert.True(session.HarvestForPlayer(initialReed.SiteId, 1).Succeeded);
            Assert.Equal(neutralRevision + 1, session.ResourceRevision);
            Assert.Equal(0, session.Wetland.ReedQuotaConsumed);
            Assert.Equal(60, session.Wetland.WetlandHealth);

            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland, 0, 0)).Succeeded);
            for (int count = 0; count < 4; count++)
            {
                PrototypeResourceSnapshot reed = FindAvailableSite(session, "reeds");
                Assert.True(session.HarvestForPlayer(reed.SiteId, 1).Succeeded);
            }

            Assert.Equal(4, session.Wetland.ReedQuotaConsumed);
            Assert.Equal(71, session.Wetland.WetlandHealth);
            Assert.Equal("healthy", session.Wetland.WetlandHealthBand);
            PrototypeResourceSnapshot blockedReed = FindAvailableSite(session, "reeds");
            int reedUnitsBefore = blockedReed.UnitsRemaining;
            int inventoryBefore = session.Inventory.GetCount("reeds");
            long revisionBefore = session.ResourceRevision;
            int eventCountBefore = session.EventLog.Entries.Count;

            PrototypeHarvestResult blocked = session.HarvestForPlayer(blockedReed.SiteId, 1);

            Assert.False(blocked.Succeeded);
            Assert.Equal("wetland_quota_exhausted", blocked.FailureReason);
            Assert.Equal(revisionBefore, session.ResourceRevision);
            Assert.Equal(inventoryBefore, session.Inventory.GetCount("reeds"));
            Assert.Equal(reedUnitsBefore, session.ResourceSnapshots.Single(site => site.SiteId == blockedReed.SiteId).UnitsRemaining);
            Assert.Equal(eventCountBefore, session.EventLog.Entries.Count);
            Assert.DoesNotContain(
                session.CaptureResourceSitesForPlanning(),
                site => site.ResourceId == "reeds");

            PrototypeResourceSnapshot nonReed = FindAvailableNonReedSite(session);
            Assert.True(session.HarvestForPlayer(nonReed.SiteId, 1).Succeeded);
            Assert.Equal(4, session.Wetland.ReedQuotaConsumed);
            Assert.Equal(71, session.Wetland.WetlandHealth);
        }

        [Fact]
        public void AiReedQuotaChecksBeforeLedgerAndPreservesFailureCleanupAndEventOrder()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland, 0, 0)).Succeeded);
            PrototypeWorkerState worker = session.Workers[0];
            List<PrototypeHarvestRequest> requests = BuildUnitRequests(session, worker, "reeds", 5);
            long revisionBefore = session.ResourceRevision;

            IReadOnlyList<PrototypeHarvestResult> results = ApplyAiRequests(session, requests);

            Assert.Equal(4, results.Count(result => result.Succeeded));
            PrototypeHarvestResult rejected = Assert.Single(results.Where(result => !result.Succeeded));
            Assert.Equal("wetland_quota_exhausted", rejected.FailureReason);
            Assert.Equal(revisionBefore + 4, session.ResourceRevision);
            Assert.Equal(4, session.Wetland.ReedQuotaConsumed);
            Assert.Equal("harvest.failed", worker.LastFailureReason);
            Assert.Equal(4, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.AiHarvestSucceeded));
            Assert.Equal(4, session.EventLog.Entries.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicWetlandQuotaConsumed));
            Assert.Single(session.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.AiHarvestFailed));
            for (int index = 0; index < session.EventLog.Entries.Count; index++)
            {
                if (session.EventLog.Entries[index].EventType == PrototypeEventTypes.CivicWetlandQuotaConsumed)
                {
                    Assert.True(index > 0);
                    Assert.Equal(
                        PrototypeEventTypes.AiHarvestSucceeded,
                        session.EventLog.Entries[index - 1].EventType);
                }
            }
        }

        [Fact]
        public void DrawDownHarvestEmitsAdditionalTransitionOnlyAtDegradedBoundary()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland, 0, 0)).Succeeded);

            for (int count = 0; count < 2; count++)
            {
                Assert.True(session.HarvestForPlayer(FindAvailableSite(session, "reeds").SiteId, 1).Succeeded);
            }
            Assert.Equal(41, session.Wetland.WetlandHealth);
            Assert.Single(session.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.CivicWetlandTransition));

            Assert.True(session.HarvestForPlayer(FindAvailableSite(session, "reeds").SiteId, 1).Succeeded);

            Assert.Equal(39, session.Wetland.WetlandHealth);
            Assert.Equal("degraded", session.Wetland.WetlandHealthBand);
            PrototypeEventRecord[] transitions = session.EventLog.Entries
                .Where(entry => entry.EventType == PrototypeEventTypes.CivicWetlandTransition)
                .ToArray();
            Assert.Equal(2, transitions.Length);
            Assert.Equal(
                "Wetland transition: cause=reed_harvest; health=41->39; band=strained->degraded",
                transitions[1].Message);
        }

        [Fact]
        public void MultiUnitPlayerHarvestAppliesExactPerUnitHealthAndRoundTripsStrictV9()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland, 0, 0)).Succeeded);
            PrototypeResourceSnapshot reed = session.ResourceSnapshots.First(site =>
                site.ResourceId == "reeds" && site.UnitsRemaining >= 3);

            PrototypeHarvestResult result = session.HarvestForPlayer(reed.SiteId, 3);

            Assert.True(result.Succeeded);
            Assert.Equal(3, session.Wetland.ReedQuotaConsumed);
            Assert.Equal(39, session.Wetland.WetlandHealth);
            Assert.Equal("degraded", session.Wetland.WetlandHealthBand);
            Assert.Equal(
                "Wetland reed quota consumed: amount=3; consumed=3; remaining=9; health=39; band=degraded",
                session.EventLog.Entries.Single(entry =>
                    entry.EventType == PrototypeEventTypes.CivicWetlandQuotaConsumed).Message);

            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            PrototypeRuntimeSnapshot deserialized = PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(snapshot));
            PrototypeRuntimeSession restored = CreateSession(initialize: false);
            restored.ApplySnapshot(deserialized);
            AssertWetlandEqual(snapshot.Wetland!, restored.Wetland);

            deserialized.Wetland!.WetlandHealth = 41;
            deserialized.Wetland.WetlandHealthBand = "strained";
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeSnapshot(
                    PrototypePersistenceService.SerializeSnapshot(deserialized)));
        }

        [Fact]
        public void ArtifactValidationRejectsTamperedMultiUnitConsequencePayload()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland, 0, 0)).Succeeded);
            PrototypeResourceSnapshot reed = session.ResourceSnapshots.First(site =>
                site.ResourceId == "reeds" && site.UnitsRemaining >= 3);
            Assert.True(session.HarvestForPlayer(reed.SiteId, 3).Succeeded);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            List<PrototypeEventRecord> tampered = CloneEvents(session.EventLog.Entries);
            tampered.Single(entry =>
                entry.EventType == PrototypeEventTypes.CivicWetlandQuotaConsumed).Message =
                "Wetland reed quota consumed: amount=1; consumed=3; remaining=9; health=39; band=degraded";
            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(snapshot, tampered, 8.0f);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokeArtifactValidation(summary, snapshot, tampered));
            Assert.IsType<InvalidDataException>(exception.InnerException);
        }

        [Theory]
        [InlineData("missing_quota")]
        [InlineData("missing_selection_transition")]
        [InlineData("duplicate_quota")]
        public void ArtifactValidationRejectsPartialMixedOrDuplicateSelectionConsequences(
            string corruption)
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland, 0, 0)).Succeeded);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            List<PrototypeEventRecord> events = CloneEvents(session.EventLog.Entries);
            switch (corruption)
            {
                case "missing_quota":
                    events.Remove(events.Single(entry =>
                        entry.EventType == PrototypeEventTypes.CivicWetlandQuotaApplied));
                    break;
                case "missing_selection_transition":
                    events.Remove(events.Single(entry =>
                        entry.EventType == PrototypeEventTypes.CivicWetlandTransition));
                    break;
                case "duplicate_quota":
                    PrototypeEventRecord quota = events.Single(entry =>
                        entry.EventType == PrototypeEventTypes.CivicWetlandQuotaApplied);
                    events.Insert(events.IndexOf(quota), new PrototypeEventRecord
                    {
                        Tick = quota.Tick,
                        EventType = quota.EventType,
                        Message = quota.Message
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corruption));
            }

            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(snapshot, events, 8.0f);
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokeArtifactValidation(summary, snapshot, events));
            Assert.IsType<InvalidDataException>(exception.InnerException);
        }

        [Fact]
        public void PlanningProjectionCachesUntilLedgerRevisionAndExcludesOnlyReedsAfterExhaustion()
        {
            PrototypeRuntimeSession session = CreateSession();
            IReadOnlyList<PrototypeResourceSiteState> neutral = session.CaptureResourceSitesForPlanning();
            Assert.Same(neutral, session.CaptureResourceSitesForPlanning());
            Assert.Contains(neutral, site => site.ResourceId == "reeds");
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland, 0, 0)).Succeeded);
            Assert.Same(neutral, session.CaptureResourceSitesForPlanning());

            Assert.True(session.HarvestForPlayer(
                session.ResourceSnapshots.First(site =>
                    site.ResourceId == "reeds" && site.UnitsRemaining >= 4).SiteId,
                4).Succeeded);
            IReadOnlyList<PrototypeResourceSiteState> exhausted = session.CaptureResourceSitesForPlanning();
            Assert.NotSame(neutral, exhausted);
            Assert.Same(exhausted, session.CaptureResourceSitesForPlanning());
            Assert.DoesNotContain(exhausted, site => site.ResourceId == "reeds");

            PrototypeResourceSnapshot nonReed = FindAvailableNonReedSite(session);
            Assert.True(session.HarvestForPlayer(nonReed.SiteId, 1).Succeeded);
            IReadOnlyList<PrototypeResourceSiteState> revised = session.CaptureResourceSitesForPlanning();
            Assert.NotSame(exhausted, revised);
            Assert.Same(revised, session.CaptureResourceSitesForPlanning());
            Assert.DoesNotContain(revised, site => site.ResourceId == "reeds");
            Assert.Equal(
                nonReed.UnitsRemaining - 1,
                revised.Single(site => site.NodeName == nonReed.SiteId).UnitsRemaining);
            Assert.Equal(
                revised.Select(site => site.NodeName).OrderBy(siteId => siteId, StringComparer.Ordinal),
                revised.Select(site => site.NodeName));
        }

        [Fact]
        public void CheckpointResumePreservesWetlandStateAndFutureEventsExactly()
        {
            PrototypeRuntimeSession direct = CreateSession();
            Assert.True(direct.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland, 0, 0)).Succeeded);
            Assert.True(direct.HarvestForPlayer(FindAvailableSite(direct, "reeds").SiteId, 1).Succeeded);
            PrototypeRuntimeSnapshot checkpoint = direct.CaptureSnapshot(new Vector3(1, 2, 3));
            List<PrototypeEventRecord> checkpointEvents = CloneEvents(direct.EventLog.Entries);
            PrototypeRuntimeSession resumed = CreateSession(initialize: false);
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(checkpoint)));
            resumed.RestoreArtifacts(checkpointEvents, null);

            string siteId = FindAvailableSite(direct, "reeds").SiteId;
            Assert.True(direct.HarvestForPlayer(siteId, 1).Succeeded);
            Assert.True(resumed.HarvestForPlayer(siteId, 1).Succeeded);

            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(direct.CaptureSnapshot(new Vector3(1, 2, 3))),
                PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(new Vector3(1, 2, 3))));
            Assert.Equal(
                PrototypePersistenceService.SerializeEventLog(direct.EventLog),
                PrototypePersistenceService.SerializeEventLog(resumed.EventLog));
        }

        [Fact]
        public void V5ThroughV8MigrateToDeterministicWetlandDefaultsIncludingSelectedV8()
        {
            foreach (int legacyVersion in new[] { 5, 6, 7 })
            {
                PrototypeRuntimeSnapshot legacy = CreateSession().CaptureSnapshot(Vector3.Zero);
                legacy.SchemaVersion = legacyVersion;
                legacy.CivicPolicy = null;
                legacy.Wetland = null;
                PrototypeRuntimeSession migrated = CreateSession(initialize: false);
                migrated.ApplySnapshot(legacy);
                Assert.Equal(9, migrated.CaptureSnapshot(Vector3.Zero).SchemaVersion);
                Assert.Equal("neutral", migrated.Wetland.PolicyId);
                Assert.Equal(0, migrated.Wetland.ReedQuotaLimit);
                Assert.Equal(60, migrated.Wetland.WetlandHealth);
            }

            PrototypeRuntimeSession selected = CreateSession();
            Assert.True(selected.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland, 0, 0)).Succeeded);
            PrototypeRuntimeSnapshot current = selected.CaptureSnapshot(Vector3.Zero);
            JsonObject v8Json = JsonNode.Parse(
                PrototypePersistenceService.SerializeSnapshot(current))!.AsObject();
            v8Json[nameof(PrototypeRuntimeSnapshot.SchemaVersion)] = 8;
            Assert.True(v8Json.Remove(nameof(PrototypeRuntimeSnapshot.Wetland)));
            PrototypeRuntimeSnapshot v8 = PrototypePersistenceService.DeserializeSnapshot(
                v8Json.ToJsonString());
            PrototypeRuntimeSession migratedV8 = CreateSession(initialize: false);
            migratedV8.ApplySnapshot(v8);
            Assert.Equal("draw_down_wetland", migratedV8.Wetland.PolicyId);
            Assert.Equal(12, migratedV8.Wetland.ReedQuotaLimit);
            Assert.Equal(0, migratedV8.Wetland.ReedQuotaConsumed);
            Assert.Equal(45, migratedV8.Wetland.WetlandHealth);
        }

        [Fact]
        public void StrictV9RequiresEveryWetlandFieldAndSaveValidatesBeforeReplacement()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland, 0, 0)).Succeeded);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            string canonical = PrototypePersistenceService.SerializeSnapshot(snapshot);
            string[] requiredFields =
            {
                nameof(PrototypeWetlandSnapshot.PolicyId),
                nameof(PrototypeWetlandSnapshot.PolicySelectedTick),
                nameof(PrototypeWetlandSnapshot.PolicyVersion),
                nameof(PrototypeWetlandSnapshot.ReedQuotaLimit),
                nameof(PrototypeWetlandSnapshot.ReedQuotaConsumed),
                nameof(PrototypeWetlandSnapshot.WetlandHealth),
                nameof(PrototypeWetlandSnapshot.WetlandHealthBand)
            };
            foreach (string field in requiredFields)
            {
                JsonObject malformed = JsonNode.Parse(canonical)!.AsObject();
                Assert.True(malformed[nameof(PrototypeRuntimeSnapshot.Wetland)]!.AsObject().Remove(field));
                Assert.Throws<InvalidDataException>(() =>
                    PrototypePersistenceService.DeserializeSnapshot(malformed.ToJsonString()));
            }

            string directory = Path.Combine(Path.GetTempPath(), $"societies-wetland-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "snapshot.json");
            try
            {
                PrototypePersistenceService.SaveSnapshot(path, snapshot);
                byte[] committed = File.ReadAllBytes(path);
                snapshot.Wetland!.WetlandHealth = 44;
                Assert.Throws<InvalidDataException>(() =>
                    PrototypePersistenceService.SaveSnapshot(path, snapshot));
                Assert.Equal(committed, File.ReadAllBytes(path));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void RunSummaryAndArtifactSemanticsAreCoherentAndLegacyWetlandAbsenceIsAccepted()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland, 0, 0)).Succeeded);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(
                snapshot,
                session.EventLog.Entries,
                8.0f);
            AssertWetlandEqual(snapshot.Wetland!, summary.Wetland!);
            InvokeArtifactValidation(summary, snapshot, session.EventLog.Entries);

            List<PrototypeEventRecord> legacyEvents = CloneEvents(session.EventLog.Entries)
                .Where(entry => entry.EventType != PrototypeEventTypes.CivicWetlandQuotaApplied &&
                    entry.EventType != PrototypeEventTypes.CivicWetlandTransition)
                .ToList();
            PrototypeRunSummary legacySummary = PrototypeRunSummaryBuilder.Build(
                snapshot,
                legacyEvents,
                8.0f);
            InvokeArtifactValidation(legacySummary, snapshot, legacyEvents);

            List<PrototypeEventRecord> malformed = CloneEvents(session.EventLog.Entries);
            malformed.Single(entry => entry.EventType == PrototypeEventTypes.CivicWetlandQuotaApplied).Message = "tampered";
            PrototypeRunSummary malformedSummary = PrototypeRunSummaryBuilder.Build(snapshot, malformed, 8.0f);
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                InvokeArtifactValidation(malformedSummary, snapshot, malformed));
            Assert.IsType<InvalidDataException>(exception.InnerException);

            summary.Wetland!.ReedQuotaLimit++;
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary(
                    PrototypePersistenceService.SerializeRunSummary(summary)));
        }

        private static void InvokeArtifactValidation(
            PrototypeRunSummary summary,
            PrototypeRuntimeSnapshot snapshot,
            IReadOnlyList<PrototypeEventRecord> events)
        {
            MethodInfo validate = typeof(PrototypeRunArtifactManager).GetMethod(
                "ValidateRunSummary",
                BindingFlags.Static | BindingFlags.NonPublic) ??
                throw new InvalidOperationException("Artifact semantic validator was not found.");
            _ = validate.Invoke(null, new object[] { summary, snapshot, events });
        }

        private static IReadOnlyList<PrototypeHarvestResult> ApplyAiRequests(
            PrototypeRuntimeSession session,
            IReadOnlyList<PrototypeHarvestRequest> requests)
        {
            MethodInfo apply = typeof(PrototypeRuntimeSession).GetMethod(
                "ApplyAiHarvestRequests",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new InvalidOperationException("AI harvest transaction method was not found.");
            return (IReadOnlyList<PrototypeHarvestResult>)apply.Invoke(
                session,
                new object?[] { requests, null })!;
        }

        private static List<PrototypeHarvestRequest> BuildUnitRequests(
            PrototypeRuntimeSession session,
            PrototypeWorkerState worker,
            string resourceId,
            int count)
        {
            List<PrototypeHarvestRequest> requests = new(count);
            foreach (PrototypeResourceSnapshot site in session.ResourceSnapshots.Where(site =>
                site.ResourceId == resourceId && site.UnitsRemaining > 0))
            {
                for (int unit = 0; unit < site.UnitsRemaining && requests.Count < count; unit++)
                {
                    requests.Add(new PrototypeHarvestRequest(
                        worker.WorkerId,
                        worker.DisplayName,
                        site.SiteId,
                        site.ResourceId,
                        1,
                        site.ClusterId));
                }
                if (requests.Count == count)
                {
                    break;
                }
            }

            Assert.Equal(count, requests.Count);
            return requests;
        }

        private static PrototypeResourceSnapshot FindAvailableSite(
            PrototypeRuntimeSession session,
            string resourceId)
        {
            return session.ResourceSnapshots.First(site =>
                site.ResourceId == resourceId && site.UnitsRemaining > 0);
        }

        private static PrototypeResourceSnapshot FindAvailableNonReedSite(
            PrototypeRuntimeSession session)
        {
            return session.ResourceSnapshots.First(site =>
                site.ResourceId != PrototypeWetlandCatalog.ReedResourceId && site.UnitsRemaining > 0);
        }

        private static void AssertWetlandEqual(
            PrototypeWetlandSnapshot expected,
            PrototypeWetlandSnapshot actual)
        {
            Assert.Equal(expected.PolicyId, actual.PolicyId);
            Assert.Equal(expected.PolicySelectedTick, actual.PolicySelectedTick);
            Assert.Equal(expected.PolicyVersion, actual.PolicyVersion);
            Assert.Equal(expected.ReedQuotaLimit, actual.ReedQuotaLimit);
            Assert.Equal(expected.ReedQuotaConsumed, actual.ReedQuotaConsumed);
            Assert.Equal(expected.WetlandHealth, actual.WetlandHealth);
            Assert.Equal(expected.WetlandHealthBand, actual.WetlandHealthBand);
        }

        private static List<PrototypeEventRecord> CloneEvents(
            IReadOnlyList<PrototypeEventRecord> events)
        {
            return JsonSerializer.Deserialize<List<PrototypeEventRecord>>(
                JsonSerializer.Serialize(events))!;
        }

        private static PrototypeRuntimeSession CreateSession(bool initialize = true)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(
                bundle.Scenarios.Resolve("balanced_basin"),
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            if (initialize)
            {
                session.Initialize(8.0f);
            }
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
    }
}
