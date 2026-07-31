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
        public void SchemaV7ArtifactsRoundTripAsOneCommittedGeneration()
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
        public void SchemaV7LoadRejectsMissingManifestAndMissingCompanion()
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
        public void SchemaV7LoadRejectsStaleOrTamperedCompanions()
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
        public void SchemaV7LoadRejectsInverseTerminalEventEvenWhenHashesAndCountsMatch()
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
        public void SchemaV7LoadRejectsMalformedAndFutureSummaryWithValidBinding()
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
            summary[nameof(PrototypeRunSummary.SchemaVersion)] = 8;
            File.WriteAllText(fixture.Paths.LegacyRunSummaryPath, summary.ToJsonString());
            fixture.Rebind(
                fixture.Paths.LegacyRunSummaryPath,
                nameof(PrototypeArtifactGenerationManifest.RunSummary));
            Assert.Throws<InvalidDataException>(() => fixture.Manager.LoadLatestArtifacts());
        }

        [Fact]
        public void SchemaV7LoadRejectsNullEventRowWithValidBinding()
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
        public void SchemaV7LoadRejectsNegativeEventTickWithValidBinding()
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
        public void SchemaV7LoadRejectsOversizedEventLogBeforeParsing()
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

            public PrototypeRuntimeSession CreateSession()
            {
                PrototypeScenarioDefinition scenario = _bundle.Scenarios.Resolve("balanced_basin");
                PrototypeRuntimeSession session = new(
                    scenario,
                    _bundle.RoleQuotas.Roles,
                    resourceDefinitions: _bundle.Resources.Resources);
                session.Initialize(8.0f);
                _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
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

            public void Rebind(string artifactPath, string bindingPropertyName)
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
                    case nameof(PrototypeArtifactGenerationManifest.EventLog):
                        manifest.EventLog = binding;
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
    }
}
