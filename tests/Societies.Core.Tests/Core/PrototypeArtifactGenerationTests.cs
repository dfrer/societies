using Godot;
using Societies.Simulation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.Core.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class PrototypeArtifactPersistenceCollection
    {
        public const string CollectionName = "Prototype artifact persistence";
    }

    [Collection(PrototypeArtifactPersistenceCollection.CollectionName)]
    public sealed class PrototypeArtifactGenerationTests
    {
        private const float TickIntervalSeconds = 1.0f / 20.0f;
        private const float DayLengthSeconds = 600.0f;
        private const string OutputEnvironmentVariable = "SOCIETIES_RUN_OUTPUT_DIR";

        [Fact]
        public void SchemaV12CausewayArtifactsBindSnapshotSummaryAndExactEventRecords()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession();
            session.Advance(24.0f, 24.0f);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);

            fixture.Save(session, snapshot);
            PrototypeLoadedArtifacts loaded = Assert.NotNull(fixture.Manager.LoadLatestArtifacts());

            Assert.Equal(12, loaded.Snapshot.SchemaVersion);
            Assert.Equal(
                JsonSerializer.Serialize(loaded.Snapshot.Causeway),
                JsonSerializer.Serialize(loaded.RunSummary!.Causeway));
            Assert.Equal(
                loaded.EventLog.Where(entry => entry.EventType.StartsWith("causeway.", StringComparison.Ordinal)).Select(EventIdentity),
                loaded.RunSummary.CausewayEvents.Select(EventIdentity));
        }

        [Fact]
        public void SchemaV12ArtifactsBindAndReloadTheActualNonDefaultFrozenCausewayDefinition()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession(causeway =>
            {
                causeway.InitialCausewayIntegrity = 41;
                causeway.InitialWetlandHealth = 91;
            });
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0,
                Kind = PrototypeCausewayCommandKind.ContributeCommunityTimber
            }).Accepted);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            fixture.Save(session, snapshot);

            PrototypeLoadedArtifacts loaded = Assert.IsType<PrototypeLoadedArtifacts>(
                fixture.Manager.LoadLatestArtifacts());
            Assert.Equal(41, loaded.Snapshot.Causeway!.Definition!.InitialCausewayIntegrity);
            Assert.Equal(91, loaded.Snapshot.Causeway.Definition.InitialWetlandHealth);
            Assert.True(PrototypeCausewayDefinitionContract.SnapshotsEqual(
                loaded.Snapshot.Causeway.Definition,
                loaded.RunSummary!.Causeway!.Definition));
            Assert.True(PrototypeCausewayState.SnapshotsEqual(
                loaded.Snapshot.Causeway, loaded.RunSummary.Causeway));
        }

        [Fact]
        public void SchemaV12ArtifactsRejectCausewayDefinitionDigestMismatchAfterCoherentRebinding()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession();
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            fixture.Save(session, snapshot);

            JsonObject summary = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            summary[nameof(PrototypeRunSummary.Causeway)]!
                [nameof(PrototypeCausewayStateSnapshot.Definition)]!
                [nameof(PrototypeCausewayDefinitionSnapshot.InitialWetlandHealth)] = 83;
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            fixture.Rebind(fixture.Paths.LegacyRunSummaryPath, nameof(PrototypeArtifactGenerationManifest.RunSummary));

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("duplicate")]
        [InlineData("wrong_order")]
        [InlineData("post_nightfall_command")]
        public void SchemaV12CausewayArtifactsRejectCoherentlyReboundEventTampering(string tamper)
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession();
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0,
                Kind = PrototypeCausewayCommandKind.ContributeStone
            }).Accepted);
            session.Advance(24.0f, 24.0f);
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonArray events = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            if (tamper == "missing")
            {
                events.RemoveAt(0);
            }
            else if (tamper == "duplicate")
            {
                events.Add(events[0]!.DeepClone());
            }
            else if (tamper == "wrong_order")
            {
                JsonNode first = events[0]!.DeepClone();
                events.RemoveAt(0);
                events.Add(first);
            }
            else
            {
                JsonNode first = events[0]!.DeepClone();
                JsonNode second = events[1]!.DeepClone();
                events[0] = second;
                events[1] = first;
            }
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, events.ToJsonString());

            JsonObject summary = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            var counts = events
                .Select(node => node!.AsObject()[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>())
                .GroupBy(type => type, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            summary[nameof(PrototypeRunSummary.EventCountsByType)] = JsonSerializer.SerializeToNode(counts);
            summary[nameof(PrototypeRunSummary.CausewayEvents)] = events.DeepClone();
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(fixture.Paths.LegacyEventLogPath, nameof(PrototypeArtifactGenerationManifest.EventLog), events.Count);
            fixture.Rebind(fixture.Paths.LegacyRunSummaryPath, nameof(PrototypeArtifactGenerationManifest.RunSummary));

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV12ArtifactsRemainSaveableAfterSixtyFiveWaterSelectionAttempts()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession();
            for (int attempt = 1; attempt <= 65; attempt++)
            {
                PrototypeCausewayCommandResult result = session.ExecuteCausewayCommand(new PrototypeCausewayCommand
                {
                    ActorId = "player", ExpectedRevision = attempt == 1 ? 0 : 1,
                    Kind = PrototypeCausewayCommandKind.SelectWaterControl,
                    WaterControl = attempt % 2 == 0
                        ? PrototypeCausewayWaterControl.DrawDownWetland
                        : PrototypeCausewayWaterControl.ProtectNursery
                });
                Assert.Equal(attempt == 1, result.Accepted);
            }

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            PrototypeLoadedArtifacts loaded = Assert.NotNull(fixture.Manager.LoadLatestArtifacts());
            Assert.Single(loaded.RunSummary!.CausewayEvents);
            Assert.Equal(1, loaded.Snapshot.Causeway!.Revision);
        }

        [Fact]
        public void SchemaV12CausewayArtifactsRejectDuplicateWaterSelectionWithoutMutatingArtifacts()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession();
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0,
                Kind = PrototypeCausewayCommandKind.SelectWaterControl,
                WaterControl = PrototypeCausewayWaterControl.DrawDownWetland
            }).Accepted);
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonObject snapshot = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacySnapshotPath))!.AsObject();
            snapshot[nameof(PrototypeRuntimeSnapshot.Causeway)]!.AsObject()[nameof(PrototypeCausewayStateSnapshot.Revision)] = 2;
            File.WriteAllText(fixture.Paths.LegacySnapshotPath, snapshot.ToJsonString());
            fixture.Rebind(fixture.Paths.LegacySnapshotPath, nameof(PrototypeArtifactGenerationManifest.Snapshot));

            JsonArray events = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            events.Add(events[0]!.DeepClone());
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, events.ToJsonString());
            fixture.Rebind(fixture.Paths.LegacyEventLogPath, nameof(PrototypeArtifactGenerationManifest.EventLog), events.Count);

            JsonObject summary = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            summary[nameof(PrototypeRunSummary.Causeway)]!.AsObject()[nameof(PrototypeCausewayStateSnapshot.Revision)] = 2;
            summary[nameof(PrototypeRunSummary.CausewayEvents)] = events.DeepClone();
            summary[nameof(PrototypeRunSummary.EventCountsByType)] = JsonSerializer.SerializeToNode(new Dictionary<string, int>
            {
                [PrototypeEventTypes.CausewayWaterControlSelected] = 2
            });
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(fixture.Paths.LegacyRunSummaryPath, nameof(PrototypeArtifactGenerationManifest.RunSummary));

            string snapshotBeforeLoad = File.ReadAllText(fixture.Paths.LegacySnapshotPath);
            string eventsBeforeLoad = File.ReadAllText(fixture.Paths.LegacyEventLogPath);
            string summaryBeforeLoad = File.ReadAllText(fixture.Paths.LegacyRunSummaryPath);

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
            Assert.Equal(snapshotBeforeLoad, File.ReadAllText(fixture.Paths.LegacySnapshotPath));
            Assert.Equal(eventsBeforeLoad, File.ReadAllText(fixture.Paths.LegacyEventLogPath));
            Assert.Equal(summaryBeforeLoad, File.ReadAllText(fixture.Paths.LegacyRunSummaryPath));
        }

        [Fact]
        public void SchemaV12CausewayArtifactsRejectReservedTimberSacrificeMaskedAsCommunityCustody()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateCausewaySession();
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 0, Kind = PrototypeCausewayCommandKind.RepairPlayerShelter
            }).Accepted);
            Assert.True(session.ExecuteCausewayCommand(new PrototypeCausewayCommand
            {
                ActorId = "player", ExpectedRevision = 1, Kind = PrototypeCausewayCommandKind.ContributeCommunityTimber
            }).Accepted);
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonArray events = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            JsonObject sacrifice = events[0]!.AsObject();
            sacrifice[nameof(PrototypeEventRecord.EventType)] = PrototypeEventTypes.CausewayTimberSacrificed;
            sacrifice[nameof(PrototypeEventRecord.Message)] = "reserved_dry_timber:1";
            JsonObject repair = events[1]!.AsObject();
            repair[nameof(PrototypeEventRecord.EventType)] = PrototypeEventTypes.CausewayShelterRepaired;
            repair[nameof(PrototypeEventRecord.Message)] = "reserved_dry_timber:2;player_labor:1";
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, events.ToJsonString());

            JsonObject summary = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            summary[nameof(PrototypeRunSummary.CausewayEvents)] = events.DeepClone();
            summary[nameof(PrototypeRunSummary.EventCountsByType)] = JsonSerializer.SerializeToNode(new Dictionary<string, int>
            {
                [PrototypeEventTypes.CausewayTimberSacrificed] = 1,
                [PrototypeEventTypes.CausewayShelterRepaired] = 1
            });
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(fixture.Paths.LegacyEventLogPath, nameof(PrototypeArtifactGenerationManifest.EventLog), events.Count);
            fixture.Rebind(fixture.Paths.LegacyRunSummaryPath, nameof(PrototypeArtifactGenerationManifest.RunSummary));

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8ArtifactsRoundTripAsOneCommittedGeneration()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);

            fixture.Save(session, snapshot);
            PrototypeLoadedArtifacts loaded = Assert.NotNull(fixture.Manager.LoadLatestArtifacts());

            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(snapshot),
                PrototypePersistenceService.SerializeSnapshot(loaded.Snapshot));
            Assert.Equal(
                PrototypePersistenceService.SerializeEventLog(session.EventLog),
                JsonSerializer.Serialize(loaded.EventLog, new JsonSerializerOptions { WriteIndented = true }));
            Assert.NotNull(loaded.RunSummary);
            Assert.True(File.Exists(fixture.Paths.GenerationManifestPath));
        }

        [Fact]
        public void SchemaV8SelectedCivicPolicyIsCoherentAcrossSnapshotEventSummaryAndManifest()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: session.SimulationTick)).Succeeded);
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            PrototypeLoadedArtifacts loaded = Assert.NotNull(
                fixture.Manager.LoadLatestArtifacts());

            Assert.Equal("protect_wetland", loaded.Snapshot.CivicPolicy!.PolicyId);
            Assert.Equal(session.SimulationTick, loaded.Snapshot.CivicPolicy.SelectedTick);
            Assert.Equal("protect_wetland", loaded.RunSummary!.CivicPolicy!.PolicyId);
            Assert.Single(loaded.EventLog.Where(entry =>
                entry.EventType == PrototypeEventTypes.CivicPolicySelected));
            Assert.Single(loaded.EventLog.Where(entry =>
                entry.EventType == PrototypeEventTypes.CivicPreferenceSummary));
            PrototypeArtifactGenerationManifest manifest =
                JsonSerializer.Deserialize<PrototypeArtifactGenerationManifest>(
                    File.ReadAllText(fixture.Paths.GenerationManifestPath))!;
            Assert.Equal(9, manifest.RuntimeSchemaVersion);
        }

        [Fact]
        public void SchemaV9LoadRejectsPreSelectionWetlandConsumptionWithCoherentBindings()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession(advance: false);
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            PrototypeResourceSnapshot reed = session.ResourceSnapshots.First(resource =>
                resource.ResourceId == "reeds" && resource.UnitsRemaining >= 2);
            Assert.True(session.HarvestForPlayer(reed.SiteId, 2).Succeeded);
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonArray eventRows = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            JsonObject harvest = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.PlayerHarvestSucceeded);
            JsonObject consumed = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicWetlandQuotaConsumed);
            eventRows.Remove(harvest);
            eventRows.Remove(consumed);
            eventRows.Insert(0, harvest);
            eventRows.Insert(1, consumed);
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog),
                eventCount: eventRows.Count);

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SelectedSchemaV8ArtifactUpgradesAfterHarvestAndReloadsAsExactV9(
            bool includePreferenceSummary)
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession legacySession = fixture.CreateSession(advance: false);
            Assert.True(legacySession.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            List<PrototypeEventRecord> legacyEvents = legacySession.EventLog.Entries
                .Where(entry => entry.EventType == PrototypeEventTypes.CivicPolicySelected ||
                    includePreferenceSummary && entry.EventType == PrototypeEventTypes.CivicPreferenceSummary)
                .Select(entry => new PrototypeEventRecord
                {
                    Tick = entry.Tick,
                    EventType = entry.EventType,
                    Message = entry.Message
                })
                .ToList();
            legacySession.EventLog.ReplaceEntries(legacyEvents);
            JsonObject v8Json = JsonNode.Parse(PrototypePersistenceService.SerializeSnapshot(
                legacySession.CaptureSnapshot(Vector3.Zero)))!.AsObject();
            v8Json[nameof(PrototypeRuntimeSnapshot.SchemaVersion)] = 8;
            Assert.True(v8Json.Remove(nameof(PrototypeRuntimeSnapshot.Wetland)));
            PrototypeRuntimeSnapshot v8Snapshot = PrototypePersistenceService.DeserializeSnapshot(
                v8Json.ToJsonString());
            fixture.Save(legacySession, v8Snapshot);

            JsonObject persistedV8Snapshot = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacySnapshotPath))!.AsObject();
            Assert.True(persistedV8Snapshot.Remove(nameof(PrototypeRuntimeSnapshot.Wetland)));
            File.WriteAllText(fixture.Paths.LegacySnapshotPath, persistedV8Snapshot.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacySnapshotPath,
                nameof(PrototypeArtifactGenerationManifest.Snapshot));
            JsonObject persistedV8Summary = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            Assert.True(persistedV8Summary.Remove(nameof(PrototypeRunSummary.Wetland)));
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, persistedV8Summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));

            PrototypeLoadedArtifacts loadedV8 = Assert.NotNull(fixture.Manager.LoadLatestArtifacts());
            Assert.Equal(8, loadedV8.Snapshot.SchemaVersion);
            Assert.Equal(includePreferenceSummary, loadedV8.EventLog.Any(entry =>
                entry.EventType == PrototypeEventTypes.CivicPreferenceSummary));
            PrototypeRuntimeSession resumed = fixture.CreateSession(advance: false);
            resumed.ApplySnapshot(loadedV8.Snapshot);
            resumed.RestoreArtifacts(loadedV8.EventLog, loadedV8.RunSummary);
            Assert.Equal(12, resumed.Wetland.ReedQuotaLimit);
            Assert.Equal(0, resumed.Wetland.ReedQuotaConsumed);
            Assert.Equal(45, resumed.Wetland.WetlandHealth);
            PrototypeResourceSnapshot reed = resumed.ResourceSnapshots.First(resource =>
                resource.ResourceId == "reeds" && resource.UnitsRemaining >= 3);
            Assert.True(resumed.HarvestForPlayer(reed.SiteId, 3).Succeeded);
            PrototypeRuntimeSnapshot upgraded = resumed.CaptureSnapshot(Vector3.Zero);
            fixture.Save(resumed, upgraded);

            PrototypeLoadedArtifacts reloadedV9 = Assert.NotNull(fixture.Manager.LoadLatestArtifacts());
            Assert.Equal(9, reloadedV9.Snapshot.SchemaVersion);
            Assert.Equal(3, reloadedV9.Snapshot.Wetland!.ReedQuotaConsumed);
            Assert.Equal(39, reloadedV9.Snapshot.Wetland.WetlandHealth);
            Assert.Equal("degraded", reloadedV9.Snapshot.Wetland.WetlandHealthBand);
            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(upgraded),
                PrototypePersistenceService.SerializeSnapshot(reloadedV9.Snapshot));
            Assert.Equal(
                PrototypePersistenceService.SerializeEventLog(resumed.EventLog),
                JsonSerializer.Serialize(
                    reloadedV9.EventLog,
                    new JsonSerializerOptions { WriteIndented = true }));
            Assert.Equal(3, reloadedV9.RunSummary!.Wetland!.ReedQuotaConsumed);
            Assert.Equal(39, reloadedV9.RunSummary.Wetland.WetlandHealth);
        }

        [Fact]
        public void SchemaV8LoadRejectsCivicSummaryOrEventMismatchWithValidBindings()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession(advance: false);
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: session.SimulationTick)).Succeeded);
            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonObject summary = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            summary[nameof(PrototypeRunSummary.CivicPolicy)]!
                .AsObject()[nameof(PrototypeCivicPolicySnapshot.PolicyId)] = "draw_down_wetland";
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            JsonArray eventRows = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            JsonObject civicEvent = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicPolicySelected);
            civicEvent[nameof(PrototypeEventRecord.Message)] = "tampered";
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            eventRows = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            JsonObject preferenceEvent = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicPreferenceSummary);
            eventRows.Remove(preferenceEvent);
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            summary = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            Assert.True(summary[nameof(PrototypeRunSummary.EventCountsByType)]!
                .AsObject().Remove(PrototypeEventTypes.CivicPreferenceSummary));
            foreach (string legacyAbsentEventType in new[]
            {
                PrototypeEventTypes.CivicWetlandQuotaApplied,
                PrototypeEventTypes.CivicWetlandTransition
            })
            {
                JsonObject wetlandEvent = eventRows
                    .Select(node => node!.AsObject())
                    .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                        legacyAbsentEventType);
                eventRows.Remove(wetlandEvent);
                Assert.True(summary[nameof(PrototypeRunSummary.EventCountsByType)]!
                    .AsObject().Remove(legacyAbsentEventType));
            }
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog),
                eventCount: eventRows.Count);
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));
            Assert.NotNull(fixture.Manager.LoadLatestArtifacts());

            JsonObject legacySelection = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicPolicySelected);
            legacySelection[nameof(PrototypeEventRecord.Message)] = "tampered";
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            eventRows = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            preferenceEvent = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicPreferenceSummary);
            eventRows.Remove(preferenceEvent);
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            summary = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            Assert.True(summary[nameof(PrototypeRunSummary.EventCountsByType)]!
                .AsObject().Remove(PrototypeEventTypes.CivicPreferenceSummary));
            foreach (string legacyAbsentEventType in new[]
            {
                PrototypeEventTypes.CivicWetlandQuotaApplied,
                PrototypeEventTypes.CivicWetlandTransition
            })
            {
                JsonObject wetlandEvent = eventRows
                    .Select(node => node!.AsObject())
                    .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                        legacyAbsentEventType);
                eventRows.Remove(wetlandEvent);
                Assert.True(summary[nameof(PrototypeRunSummary.EventCountsByType)]!
                    .AsObject().Remove(legacyAbsentEventType));
            }
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog),
                eventCount: eventRows.Count);
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));
            legacySelection = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicPolicySelected);
            long selectedTick = legacySelection[nameof(PrototypeEventRecord.Tick)]!.GetValue<long>();
            legacySelection[nameof(PrototypeEventRecord.Tick)] = selectedTick == 0 ? 1 : 0;
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            eventRows = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            preferenceEvent = eventRows
                .Select(node => node!.AsObject())
                .Single(node => node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>() ==
                    PrototypeEventTypes.CivicPreferenceSummary);
            preferenceEvent[nameof(PrototypeEventRecord.Message)] = "bogus";
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void EverySchemaV8GenerationManifestPropertyIsRequired()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            string complete = File.ReadAllText(fixture.Paths.GenerationManifestPath);

            foreach (string propertyName in typeof(PrototypeArtifactGenerationManifest)
                .GetProperties()
                .Select(property => property.Name))
            {
                JsonObject incomplete = JsonNode.Parse(complete)!.AsObject();
                Assert.True(incomplete.Remove(propertyName));
                File.WriteAllText(
                    fixture.Paths.GenerationManifestPath,
                    incomplete.ToJsonString());
                Assert.Throws<InvalidDataException>(() =>
                    fixture.Manager.LoadLatestArtifacts());
            }

            foreach (string bindingName in new[]
            {
                nameof(PrototypeArtifactGenerationManifest.Snapshot),
                nameof(PrototypeArtifactGenerationManifest.EventLog),
                nameof(PrototypeArtifactGenerationManifest.RunSummary)
            })
            {
                foreach (string propertyName in typeof(PrototypeArtifactFileBinding)
                    .GetProperties()
                    .Select(property => property.Name))
                {
                    JsonObject incomplete = JsonNode.Parse(complete)!.AsObject();
                    JsonObject binding = incomplete[bindingName]!.AsObject();
                    Assert.True(binding.Remove(propertyName));
                    File.WriteAllText(
                        fixture.Paths.GenerationManifestPath,
                        incomplete.ToJsonString());
                    Assert.Throws<InvalidDataException>(() =>
                        fixture.Manager.LoadLatestArtifacts());
                }
            }
        }

        [Fact]
        public void SchemaV8LoadRejectsMissingManifestAndMissingCompanion()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            File.Delete(fixture.Paths.GenerationManifestPath);
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            File.Delete(fixture.Paths.LegacyEventLogPath);
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8LoadRejectsStaleOrTamperedCompanions()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            File.AppendAllText(fixture.Paths.LegacyEventLogPath, " ");
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            File.AppendAllText(fixture.Paths.LegacyRunSummaryPath, " ");
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8LoadRejectsInverseTerminalEventEvenWhenHashesAndCountsMatch()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateTerminalSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonArray eventRows = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            JsonObject terminal = eventRows
                .Select(node => node!.AsObject())
                .Single(node => string.Equals(
                    node[nameof(PrototypeEventRecord.EventType)]!.GetValue<string>(),
                    PrototypeEventTypes.CrisisStabilized,
                    StringComparison.Ordinal));
            terminal[nameof(PrototypeEventRecord.EventType)] = PrototypeEventTypes.CrisisCollapsed;
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());

            JsonObject summary = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            JsonObject eventCounts = summary[nameof(PrototypeRunSummary.EventCountsByType)]!.AsObject();
            int terminalCount = eventCounts[PrototypeEventTypes.CrisisStabilized]!.GetValue<int>();
            eventCounts.Remove(PrototypeEventTypes.CrisisStabilized);
            eventCounts[PrototypeEventTypes.CrisisCollapsed] = terminalCount;
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8LoadRejectsMalformedAndFutureSummaryWithValidBinding()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, "{");
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());

            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));
            JsonObject summary = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyRunSummaryPath))!.AsObject();
            summary[nameof(PrototypeRunSummary.SchemaVersion)] = 10;
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8LoadRejectsNullEventRowWithValidBinding()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonArray eventRows = JsonNode.Parse(File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            eventRows.Add(null);
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8LoadRejectsNegativeEventTickWithValidBinding()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            JsonArray eventRows = JsonNode.Parse(
                File.ReadAllText(fixture.Paths.LegacyEventLogPath))!.AsArray();
            Assert.NotEmpty(eventRows);
            eventRows[0]!.AsObject()[nameof(PrototypeEventRecord.Tick)] = -1;
            File.WriteAllText(fixture.Paths.LegacyEventLogPath, eventRows.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyEventLogPath,
                nameof(PrototypeArtifactGenerationManifest.EventLog));

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV8LoadRejectsOversizedEventLogBeforeParsing()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            fixture.Save(session, session.CaptureSnapshot(Vector3.Zero));

            using (FileStream stream = new(
                fixture.Paths.LegacyEventLogPath,
                FileMode.Create,
                System.IO.FileAccess.Write,
                FileShare.None))
            {
                stream.SetLength((16 * 1024 * 1024) + 1L);
            }

            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void OversizedSaveLeavesExistingCommittedGenerationByteIdenticalAndLoadable()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            fixture.Save(session, snapshot);
            string[] committedPaths =
            {
                fixture.Paths.LegacySnapshotPath,
                fixture.Paths.LegacyEventLogPath,
                fixture.Paths.LegacyRunSummaryPath,
                fixture.Paths.GenerationManifestPath
            };
            Dictionary<string, byte[]> committedBytes = committedPaths.ToDictionary(
                path => path,
                File.ReadAllBytes,
                StringComparer.Ordinal);
            int committedEventCount = session.EventLog.Entries.Count;

            while (session.EventLog.Entries.Count <= 50_000)
            {
                session.EventLog.Record(
                    snapshot.SimulationTick,
                    "test.oversized",
                    "bounded");
            }

            Assert.Throws<InvalidDataException>(() => fixture.Save(session, snapshot));
            foreach (string path in committedPaths)
            {
                Assert.Equal(committedBytes[path], File.ReadAllBytes(path));
            }

            PrototypeLoadedArtifacts loaded = Assert.NotNull(
                fixture.Manager.LoadLatestArtifacts());
            Assert.Equal(committedEventCount, loaded.EventLog.Count);
            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(snapshot),
                PrototypePersistenceService.SerializeSnapshot(loaded.Snapshot));
        }

        [Fact]
        public void InconsistentCivicSavePreservesExistingCommittedGeneration()
        {
            using ArtifactFixture fixture = ArtifactFixture.Create();
            PrototypeRuntimeSession session = fixture.CreateSession();
            PrototypeRuntimeSnapshot committedSnapshot = session.CaptureSnapshot(Vector3.Zero);
            fixture.Save(session, committedSnapshot);
            string[] committedPaths =
            {
                fixture.Paths.LegacySnapshotPath,
                fixture.Paths.LegacyEventLogPath,
                fixture.Paths.LegacyRunSummaryPath,
                fixture.Paths.GenerationManifestPath
            };
            Dictionary<string, byte[]> committedBytes = committedPaths.ToDictionary(
                path => path,
                File.ReadAllBytes,
                StringComparer.Ordinal);
            PrototypeRuntimeSnapshot invalid = PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(committedSnapshot));
            invalid.CivicPolicy!.PolicyId = "protect_wetland";

            Assert.Throws<InvalidDataException>(() => fixture.Save(session, invalid));
            foreach (string path in committedPaths)
            {
                Assert.Equal(committedBytes[path], File.ReadAllBytes(path));
            }

            Assert.NotNull(fixture.Manager.LoadLatestArtifacts());
        }

        private sealed class ArtifactFixture : IDisposable
        {
            private readonly string? _previousOutputDirectory;
            private readonly PrototypeCatalogBundle _bundle;

            private ArtifactFixture(string directory)
            {
                Directory = directory;
                _previousOutputDirectory = System.Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
                System.Environment.SetEnvironmentVariable(OutputEnvironmentVariable, directory);
                _bundle = LoadCatalogs();
                Manager = new PrototypeRunArtifactManager();
                Paths = Manager.GetArtifactPaths();
            }

            public string Directory { get; }

            public PrototypeRunArtifactManager Manager { get; }

            public PrototypeArtifactPaths Paths { get; }

            public static ArtifactFixture Create()
            {
                string directory = Path.Combine(
                    Path.GetTempPath(),
                    $"societies-artifact-generation-tests-{Guid.NewGuid():N}");
                System.IO.Directory.CreateDirectory(directory);
                return new ArtifactFixture(directory);
            }

            public PrototypeRuntimeSession CreateSession(bool advance = true)
            {
                PrototypeScenarioDefinition scenario = _bundle.Scenarios.Resolve("balanced_basin");
                PrototypeRuntimeSession session = new(
                    scenario,
                    _bundle.RoleQuotas.Roles,
                    resourceDefinitions: _bundle.Resources.Resources);
                session.Initialize(8.0f);
                if (advance)
                {
                    _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
                }
                return session;
            }

            public PrototypeRuntimeSession CreateCausewaySession(
                Action<PrototypeCausewayDefinition>? configure = null)
            {
                PrototypeScenarioDefinition scenario = _bundle.Scenarios.Resolve("snow_globe_voxel");
                configure?.Invoke(scenario.Causeway!);
                PrototypeRuntimeSession session = new(
                    scenario,
                    _bundle.RoleQuotas.Roles,
                    resourceDefinitions: _bundle.Resources.Resources);
                session.Initialize(8.0f);
                return session;
            }

            public PrototypeRuntimeSession CreateTerminalSession()
            {
                PrototypeScenarioDefinition scenario = JsonSerializer.Deserialize<PrototypeScenarioDefinition>(
                    JsonSerializer.Serialize(_bundle.Scenarios.Resolve("empty_stores")))!;
                PrototypeCrisisDefinition crisis = scenario.Crisis!;
                crisis.DeadlineTicks = 20;
                crisis.StableHoldTicks = 2;
                crisis.CollapseHoldTicks = 5;
                crisis.RequiredCapableCitizens = 1;
                crisis.RequiredMeals = 0;
                crisis.RequiredHearthFuel = 0;
                crisis.RequiredBedCoveragePercent = 0;
                crisis.CollapseIncapacitatedCitizens = scenario.InitialCitizens;
                PrototypeRuntimeSession session = new(
                    scenario,
                    _bundle.RoleQuotas.Roles,
                    resourceDefinitions: _bundle.Resources.Resources);
                session.Initialize(8.0f);
                while (!session.Crisis!.IsTerminal)
                {
                    _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
                }

                return session;
            }

            public void Save(PrototypeRuntimeSession session, PrototypeRuntimeSnapshot snapshot)
            {
                PrototypeWorldSummary worldSummary = PrototypeWorldSummaryBuilder.Build(
                    session,
                    terrain: null,
                    session.ActiveResourceSnapshots);
                _ = Manager.SaveArtifacts(session, snapshot, worldSummary);
            }

            public void Rebind(
                string artifactPath,
                string bindingPropertyName,
                int? eventCount = null)
            {
                byte[] artifactBytes = File.ReadAllBytes(artifactPath);
                PrototypeArtifactGenerationManifest manifest =
                    JsonSerializer.Deserialize<PrototypeArtifactGenerationManifest>(
                        File.ReadAllText(Paths.GenerationManifestPath))!;
                PrototypeArtifactFileBinding binding = new()
                {
                    FileName = Path.GetFileName(artifactPath),
                    ByteLength = artifactBytes.LongLength,
                    Sha256 = Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant()
                };
                switch (bindingPropertyName)
                {
                    case nameof(PrototypeArtifactGenerationManifest.Snapshot):
                        manifest.Snapshot = binding;
                        break;
                    case nameof(PrototypeArtifactGenerationManifest.EventLog):
                        manifest.EventLog = binding;
                        if (eventCount.HasValue)
                        {
                            manifest.EventCount = eventCount.Value;
                        }
                        break;
                    case nameof(PrototypeArtifactGenerationManifest.RunSummary):
                        manifest.RunSummary = binding;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(bindingPropertyName));
                }

                File.WriteAllText(
                    Paths.GenerationManifestPath,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }

            public void Dispose()
            {
                System.Environment.SetEnvironmentVariable(
                    OutputEnvironmentVariable,
                    _previousOutputDirectory);
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, recursive: true);
                }
            }

            private static PrototypeCatalogBundle LoadCatalogs()
            {
                string? current = AppContext.BaseDirectory;
                while (!string.IsNullOrWhiteSpace(current))
                {
                    string candidate = Path.Combine(current, "src", "societies", "data");
                    if (System.IO.Directory.Exists(candidate))
                    {
                        return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                    }

                    current = System.IO.Directory.GetParent(current)?.FullName;
                }

                throw new DirectoryNotFoundException("Could not find src/societies/data.");
            }
        }

        private static string EventIdentity(PrototypeEventRecord entry) =>
            $"{entry.Tick}|{entry.EventType}|{entry.Message}";
    }
}
