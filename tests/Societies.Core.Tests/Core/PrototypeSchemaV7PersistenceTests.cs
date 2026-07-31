using Godot;
using Societies.Simulation;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeSchemaV7PersistenceTests
    {
        private const float TickIntervalSeconds = 1.0f / 20.0f;
        private const float DayLengthSeconds = 600.0f;

        [Fact]
        public void LegacyV6AndV5SnapshotsMigrateToTheSameNeutralSchemaV7State()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession source = CreateSession(bundle, scenario);
            for (int tick = 0; tick < 20; tick++)
            {
                _ = source.Advance(TickIntervalSeconds, DayLengthSeconds);
            }

            PrototypeRuntimeSnapshot current = source.CaptureSnapshot(Vector3.Zero);
            string v6Json = DowngradeSnapshotJson(current, 6, clearStableResourceIds: false);
            string v5Json = DowngradeSnapshotJson(current, 5, clearStableResourceIds: true);
            PrototypeRuntimeSnapshot legacyV6 = PrototypePersistenceService.DeserializeSnapshot(v6Json);
            PrototypeRuntimeSnapshot legacyV5 = PrototypePersistenceService.DeserializeSnapshot(v5Json);

            PrototypeRuntimeSession migratedV6 = CreateSession(bundle, scenario, initialize: false);
            PrototypeRuntimeSession migratedV5 = CreateSession(bundle, scenario, initialize: false);
            migratedV6.ApplySnapshot(legacyV6);
            migratedV5.ApplySnapshot(legacyV5);

            PrototypeRuntimeSnapshot v6Result = migratedV6.CaptureSnapshot(Vector3.Zero);
            PrototypeRuntimeSnapshot v5Result = migratedV5.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(7, v6Result.SchemaVersion);
            Assert.Equal(7, v5Result.SchemaVersion);
            Assert.Equal("neutral", v6Result.Directive!.DirectiveId);
            Assert.Empty(v6Result.ContributionCountsByResource);
            Assert.Null(v6Result.Crisis);
            Assert.False(v6Result.Telemetry!.HasCrisisObservation);
            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(v6Result),
                PrototypePersistenceService.SerializeSnapshot(v5Result));
        }

        [Fact]
        public void SchemaV7CrisisRoundTripAndCheckpointResumeAreExact()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = CreateShortCrisisScenario(bundle, stableHoldTicks: 4, requiredMeals: 0);
            PrototypeRuntimeSession continuous = CreateSession(bundle, scenario);
            continuous.Inventory.AddItem("logs", 3);
            Assert.True(continuous.SetDirective(PrototypeSettlementDirective.FoodAndFuel).Changed);
            Assert.True(continuous.ContributeToStockpile("logs", 3).Succeeded);
            _ = continuous.Advance(TickIntervalSeconds, DayLengthSeconds);
            Assert.True(continuous.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            _ = continuous.Advance(TickIntervalSeconds, DayLengthSeconds);

            PrototypeRuntimeSnapshot checkpoint = continuous.CaptureSnapshot(new Vector3(1.0f, 2.0f, 3.0f));
            List<PrototypeEventRecord> checkpointEvents = CloneEvents(continuous.EventLog.Entries);
            PrototypeRuntimeSession resumed = CreateSession(bundle, scenario, initialize: false);
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(checkpoint)));
            resumed.RestoreArtifacts(checkpointEvents, null);

            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(checkpoint),
                PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(new Vector3(1.0f, 2.0f, 3.0f))));
            while (!continuous.Crisis!.IsTerminal)
            {
                _ = continuous.Advance(TickIntervalSeconds, DayLengthSeconds);
                _ = resumed.Advance(TickIntervalSeconds, DayLengthSeconds);
            }

            string continuousJson = PrototypePersistenceService.SerializeSnapshot(
                continuous.CaptureSnapshot(new Vector3(1.0f, 2.0f, 3.0f)));
            string resumedJson = PrototypePersistenceService.SerializeSnapshot(
                resumed.CaptureSnapshot(new Vector3(1.0f, 2.0f, 3.0f)));
            Assert.Equal(continuousJson, resumedJson);
            Assert.Equal(
                JsonSerializer.Serialize(continuous.EventLog.Entries),
                JsonSerializer.Serialize(resumed.EventLog.Entries));
            Assert.Equal(PrototypeCrisisOutcome.Stable, resumed.Crisis!.Outcome);
            Assert.True(resumed.Crisis.TerminalEventEmitted);
            Assert.Single(resumed.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.CrisisStabilized));

            int terminalEventCount = resumed.EventLog.Entries.Count;
            PrototypeRuntimeSnapshot terminalCheckpoint = resumed.CaptureSnapshot(Vector3.Zero);
            PrototypeRuntimeSession terminalResume = CreateSession(bundle, scenario, initialize: false);
            terminalResume.ApplySnapshot(terminalCheckpoint);
            terminalResume.RestoreArtifacts(CloneEvents(resumed.EventLog.Entries), null);
            _ = terminalResume.Advance(TickIntervalSeconds, DayLengthSeconds);
            Assert.Equal(terminalEventCount, terminalResume.EventLog.Entries.Count);
            Assert.Single(terminalResume.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.CrisisStabilized));
        }

        [Fact]
        public void MalformedAndFutureSnapshotsAreRejectedWithoutLiveMutation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = CreateShortCrisisScenario(bundle, stableHoldTicks: 5, requiredMeals: 0);
            PrototypeRuntimeSession session = CreateSession(bundle, scenario);
            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            string eventsBefore = JsonSerializer.Serialize(session.EventLog.Entries);

            PrototypeRuntimeSnapshot invalidDirective = CloneSnapshot(before);
            invalidDirective.Directive!.DirectiveId = "unknown";
            AssertRejectedWithoutMutation(session, invalidDirective, before, eventsBefore);

            PrototypeRuntimeSnapshot invalidContribution = CloneSnapshot(before);
            invalidContribution.ContributionCountsByResource["logs"] = -1;
            AssertRejectedWithoutMutation(session, invalidContribution, before, eventsBefore);

            PrototypeRuntimeSnapshot invalidCrisis = CloneSnapshot(before);
            invalidCrisis.Crisis!.ElapsedTicks = invalidCrisis.Crisis.DeadlineTicks + 1;
            AssertRejectedWithoutMutation(session, invalidCrisis, before, eventsBefore);

            PrototypeRuntimeSnapshot invalidTelemetry = CloneSnapshot(before);
            invalidTelemetry.Telemetry!.FinalMeals++;
            AssertRejectedWithoutMutation(session, invalidTelemetry, before, eventsBefore);

            JsonObject future = JsonNode.Parse(before)!.AsObject();
            future[nameof(PrototypeRuntimeSnapshot.SchemaVersion)] = 8;
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeSnapshot(future.ToJsonString()));
            Assert.Equal(before, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBefore, JsonSerializer.Serialize(session.EventLog.Entries));
        }

        [Fact]
        public void EverySchemaV7SnapshotPropertyIsRequiredWithoutLiveMutation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = CreateShortCrisisScenario(bundle, stableHoldTicks: 5, requiredMeals: 0);
            PrototypeRuntimeSession session = CreateSession(bundle, scenario);
            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            string before = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            string eventsBefore = JsonSerializer.Serialize(session.EventLog.Entries);

            foreach (string propertyName in typeof(PrototypeRuntimeSnapshot)
                .GetProperties()
                .Select(property => property.Name))
            {
                JsonObject incomplete = JsonNode.Parse(before)!.AsObject();
                Assert.True(incomplete.Remove(propertyName));

                Assert.Throws<InvalidDataException>(() =>
                {
                    PrototypeRuntimeSnapshot candidate =
                        PrototypePersistenceService.DeserializeSnapshot(incomplete.ToJsonString());
                    session.ApplySnapshot(candidate);
                });
                Assert.Equal(
                    before,
                    PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
                Assert.Equal(eventsBefore, JsonSerializer.Serialize(session.EventLog.Entries));
            }
        }

        public static IEnumerable<object[]> NestedSchemaV7RequiredPropertyPaths()
        {
            foreach (string propertyName in typeof(PrototypeDirectiveSnapshot)
                .GetProperties()
                .Select(property => property.Name))
            {
                yield return new object[] { nameof(PrototypeRuntimeSnapshot.Directive), propertyName };
            }

            foreach (string propertyName in typeof(PrototypeRuntimeTelemetrySnapshot)
                .GetProperties()
                .Select(property => property.Name))
            {
                yield return new object[] { nameof(PrototypeRuntimeSnapshot.Telemetry), propertyName };
            }

            foreach (string propertyName in typeof(PrototypeCrisisStateSnapshot)
                .GetProperties()
                .Select(property => property.Name))
            {
                yield return new object[] { nameof(PrototypeRuntimeSnapshot.Crisis), propertyName };
            }

            foreach (string propertyName in typeof(PrototypeCrisisObservation)
                .GetProperties()
                .Select(property => property.Name))
            {
                yield return new object[]
                {
                    $"{nameof(PrototypeRuntimeSnapshot.Crisis)}.{nameof(PrototypeCrisisStateSnapshot.LastObservation)}",
                    propertyName
                };
            }
        }

        [Theory]
        [MemberData(nameof(NestedSchemaV7RequiredPropertyPaths))]
        public void EveryNestedSchemaV7PropertyIsRequiredWithoutLiveMutation(
            string payloadPath,
            string propertyName)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario =
                CreateShortCrisisScenario(bundle, stableHoldTicks: 5, requiredMeals: 0);
            PrototypeRuntimeSession session = CreateSession(bundle, scenario);
            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            string before = PrototypePersistenceService.SerializeSnapshot(
                session.CaptureSnapshot(Vector3.Zero));
            string eventsBefore = JsonSerializer.Serialize(session.EventLog.Entries);
            JsonObject incomplete = JsonNode.Parse(before)!.AsObject();
            JsonObject payload = payloadPath switch
            {
                nameof(PrototypeRuntimeSnapshot.Directive) =>
                    incomplete[nameof(PrototypeRuntimeSnapshot.Directive)]!.AsObject(),
                nameof(PrototypeRuntimeSnapshot.Telemetry) =>
                    incomplete[nameof(PrototypeRuntimeSnapshot.Telemetry)]!.AsObject(),
                nameof(PrototypeRuntimeSnapshot.Crisis) =>
                    incomplete[nameof(PrototypeRuntimeSnapshot.Crisis)]!.AsObject(),
                _ => incomplete[nameof(PrototypeRuntimeSnapshot.Crisis)]!
                    .AsObject()[nameof(PrototypeCrisisStateSnapshot.LastObservation)]!
                    .AsObject()
            };
            Assert.True(payload.Remove(propertyName));

            Assert.Throws<InvalidDataException>(() =>
            {
                PrototypeRuntimeSnapshot candidate =
                    PrototypePersistenceService.DeserializeSnapshot(incomplete.ToJsonString());
                session.ApplySnapshot(candidate);
            });
            Assert.Equal(
                before,
                PrototypePersistenceService.SerializeSnapshot(
                    session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBefore, JsonSerializer.Serialize(session.EventLog.Entries));
        }

        [Fact]
        public void RestoredStabilityHoldBreakEmitsOneBoundedTransitionEvent()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = CreateShortCrisisScenario(bundle, stableHoldTicks: 4, requiredMeals: 2);
            PrototypeRuntimeSession source = CreateSession(bundle, scenario);
            _ = source.Advance(TickIntervalSeconds, DayLengthSeconds);
            PrototypeRuntimeSnapshot checkpoint = source.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(1, checkpoint.Crisis!.StableHoldTicks);
            Assert.Equal(1, checkpoint.Telemetry!.StabilityHoldEntries);

            checkpoint.Stockpile.Remove("meals");
            checkpoint.Settlement!.CentralDepot.Items.Remove("meals");
            PrototypeRuntimeSession resumed = CreateSession(bundle, scenario, initialize: false);
            resumed.ApplySnapshot(checkpoint);
            resumed.RestoreArtifacts(CloneEvents(source.EventLog.Entries), null);
            _ = resumed.Advance(TickIntervalSeconds, DayLengthSeconds);

            Assert.Equal(0, resumed.Crisis!.StableHoldTicks);
            Assert.Equal(1, resumed.CaptureTelemetrySnapshot().StabilityHoldBreaks);
            Assert.Single(resumed.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.CrisisStabilityHoldBroken));
        }

        [Fact]
        public void RestoredMaximumHoldCountersSaturateAcrossNextTransitions()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition entryScenario =
                CreateShortCrisisScenario(bundle, stableHoldTicks: 4, requiredMeals: 0);
            PrototypeRuntimeSession entrySource = CreateSession(bundle, entryScenario);
            _ = entrySource.Advance(TickIntervalSeconds, DayLengthSeconds);
            PrototypeRuntimeSnapshot entrySnapshot = entrySource.CaptureSnapshot(Vector3.Zero);
            entrySnapshot.Crisis!.StableHoldTicks = 0;
            entrySnapshot.Telemetry!.StabilityHoldEntries = int.MaxValue;
            PrototypeRuntimeSession entryResume = CreateSession(bundle, entryScenario, initialize: false);
            entryResume.ApplySnapshot(entrySnapshot);

            _ = entryResume.Advance(TickIntervalSeconds, DayLengthSeconds);

            Assert.Equal(
                int.MaxValue,
                entryResume.CaptureTelemetrySnapshot().StabilityHoldEntries);

            PrototypeScenarioDefinition breakScenario =
                CreateShortCrisisScenario(bundle, stableHoldTicks: 4, requiredMeals: 2);
            PrototypeRuntimeSession breakSource = CreateSession(bundle, breakScenario);
            _ = breakSource.Advance(TickIntervalSeconds, DayLengthSeconds);
            PrototypeRuntimeSnapshot breakSnapshot = breakSource.CaptureSnapshot(Vector3.Zero);
            Assert.True(breakSnapshot.Crisis!.StableHoldTicks > 0);
            breakSnapshot.Telemetry!.StabilityHoldEntries = int.MaxValue;
            breakSnapshot.Telemetry.StabilityHoldBreaks = int.MaxValue;
            breakSnapshot.Stockpile.Remove("meals");
            breakSnapshot.Settlement!.CentralDepot.Items.Remove("meals");
            PrototypeRuntimeSession breakResume = CreateSession(bundle, breakScenario, initialize: false);
            breakResume.ApplySnapshot(breakSnapshot);

            _ = breakResume.Advance(TickIntervalSeconds, DayLengthSeconds);

            Assert.Equal(
                int.MaxValue,
                breakResume.CaptureTelemetrySnapshot().StabilityHoldBreaks);
        }

        [Fact]
        public void SummaryAndMetricsExposeCompactSchemaV7CrisisTelemetry()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = CreateShortCrisisScenario(bundle, stableHoldTicks: 3, requiredMeals: 0);
            PrototypeRuntimeSession session = CreateSession(bundle, scenario);
            session.Inventory.AddItem("berries", 2);
            Assert.True(session.SetDirective(PrototypeSettlementDirective.FoodAndFuel).Changed);
            Assert.True(session.ContributeToStockpile("berries", 2).Succeeded);
            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            Assert.True(session.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            while (!session.Crisis!.IsTerminal)
            {
                _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            }

            session.CaptureMetrics();
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            PrototypeRunSummary summary = PrototypeRunSummaryBuilder.Build(
                snapshot,
                session.EventLog.Entries,
                session.RunStartHour,
                scenario.Id,
                scenario.DisplayName,
                null);
            string csv = session.MetricsTracker.BuildCsv();

            Assert.Equal(7, summary.SchemaVersion);
            Assert.Equal("stable", summary.CrisisOutcome);
            Assert.Equal(string.Empty, summary.CrisisFailureReason);
            Assert.Equal(snapshot.Crisis!.ElapsedTicks, summary.CrisisElapsedTicks);
            Assert.Equal(snapshot.Crisis.DeadlineTicks, summary.CrisisDeadlineTicks);
            Assert.True(summary.TerminalEventEmitted);
            Assert.Equal(0, summary.FirstDirectiveTick);
            Assert.Equal(0, summary.FirstContributionTick);
            Assert.Equal(2, summary.DirectiveChanges);
            Assert.Equal("shelter", summary.FinalDirective);
            Assert.Equal(2, summary.ContributionsByResource["berries"]);
            Assert.Equal(snapshot.Telemetry!.PeakIncapacitatedCitizens, summary.PeakIncapacitatedCitizens);
            Assert.Equal(snapshot.Telemetry.MinimumMeals, summary.MinimumMeals);
            Assert.Equal(snapshot.Telemetry.MinimumHearthFuel, summary.MinimumHearthFuel);
            Assert.Equal(snapshot.Telemetry.MaximumBedCoveragePercent, summary.MaximumBedCoveragePercent);
            Assert.Equal(1, summary.StabilityHoldEntries);
            Assert.Equal(0, summary.StabilityHoldBreaks);
            Assert.Contains("contributions_by_resource", csv, StringComparison.Ordinal);
            Assert.Contains("berries:2", csv, StringComparison.Ordinal);
            Assert.DoesNotContain("per_tick_narrative", csv, StringComparison.Ordinal);

            PrototypeEventRecord[] crisisEvents = session.EventLog.Entries
                .Where(entry => entry.EventType.StartsWith("crisis.", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(2, crisisEvents.Length);
            Assert.Contains(crisisEvents, entry => entry.EventType == PrototypeEventTypes.CrisisStabilityHoldEntered);
            Assert.Contains(crisisEvents, entry => entry.EventType == PrototypeEventTypes.CrisisStabilized);
        }

        private static PrototypeRuntimeSession CreateSession(
            PrototypeCatalogBundle bundle,
            PrototypeScenarioDefinition scenario,
            bool initialize = true)
        {
            PrototypeRuntimeSession session = new(
                scenario,
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            if (initialize)
            {
                session.Initialize(8.0f);
            }

            return session;
        }

        private static PrototypeScenarioDefinition CreateShortCrisisScenario(
            PrototypeCatalogBundle bundle,
            int stableHoldTicks,
            int requiredMeals)
        {
            PrototypeScenarioDefinition scenario = JsonSerializer.Deserialize<PrototypeScenarioDefinition>(
                JsonSerializer.Serialize(bundle.Scenarios.Resolve("empty_stores")))!;
            PrototypeCrisisDefinition crisis = scenario.Crisis!;
            crisis.DeadlineTicks = 20;
            crisis.StableHoldTicks = stableHoldTicks;
            crisis.CollapseHoldTicks = 5;
            crisis.RequiredCapableCitizens = 1;
            crisis.RequiredMeals = requiredMeals;
            crisis.RequiredHearthFuel = 0;
            crisis.RequiredBedCoveragePercent = 0;
            crisis.CollapseIncapacitatedCitizens = scenario.InitialCitizens;
            return scenario;
        }

        private static string DowngradeSnapshotJson(
            PrototypeRuntimeSnapshot snapshot,
            int schemaVersion,
            bool clearStableResourceIds)
        {
            JsonObject root = JsonNode.Parse(PrototypePersistenceService.SerializeSnapshot(snapshot))!.AsObject();
            root[nameof(PrototypeRuntimeSnapshot.SchemaVersion)] = schemaVersion;
            root.Remove(nameof(PrototypeRuntimeSnapshot.Directive));
            root.Remove(nameof(PrototypeRuntimeSnapshot.ContributionCountsByResource));
            root.Remove(nameof(PrototypeRuntimeSnapshot.Crisis));
            root.Remove(nameof(PrototypeRuntimeSnapshot.Telemetry));
            if (clearStableResourceIds)
            {
                foreach (JsonNode? resource in root[nameof(PrototypeRuntimeSnapshot.Resources)]!.AsArray())
                {
                    resource!.AsObject()[nameof(PrototypeResourceSnapshot.SiteId)] = string.Empty;
                }
            }

            return root.ToJsonString();
        }

        private static PrototypeRuntimeSnapshot CloneSnapshot(string json)
        {
            return PrototypePersistenceService.DeserializeSnapshot(json);
        }

        private static List<PrototypeEventRecord> CloneEvents(
            IReadOnlyList<PrototypeEventRecord> events)
        {
            return events
                .Select(entry => new PrototypeEventRecord
                {
                    Tick = entry.Tick,
                    EventType = entry.EventType,
                    Message = entry.Message
                })
                .ToList();
        }

        private static void AssertRejectedWithoutMutation(
            PrototypeRuntimeSession session,
            PrototypeRuntimeSnapshot invalid,
            string snapshotBefore,
            string eventsBefore)
        {
            Assert.Throws<InvalidDataException>(() => session.ApplySnapshot(invalid));
            Assert.Equal(snapshotBefore, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBefore, JsonSerializer.Serialize(session.EventLog.Entries));
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
