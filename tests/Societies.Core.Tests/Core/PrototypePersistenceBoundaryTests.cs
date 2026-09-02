using Godot;
using System.Text.Json;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypePersistenceBoundaryTests
    {
        [Fact]
        public void PublicFileLoadersRejectOversizedFilesBeforeDecoding()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            string snapshotPath = fixture.CreateSparseFile(
                "snapshot.json",
                PrototypeRunArtifactManager.MaximumSnapshotBytes + 1);
            string eventLogPath = fixture.CreateSparseFile(
                "event-log.json",
                PrototypeRunArtifactManager.MaximumEventLogBytes + 1);
            string runSummaryPath = fixture.CreateSparseFile(
                "run-summary.json",
                PrototypeRunArtifactManager.MaximumRunSummaryBytes + 1);
            string worldSummaryPath = fixture.CreateSparseFile(
                "world-summary.json",
                PrototypeRunArtifactManager.MaximumWorldSummaryBytes + 1);

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.LoadSnapshot(snapshotPath));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.LoadEventLog(eventLogPath));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.LoadRunSummary(runSummaryPath));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.LoadWorldSummary(worldSummaryPath));
        }

        [Fact]
        public void PublicEventLogDeserializerRejectsNullRowsNegativeTicksAndOutOfOrderTicks()
        {
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeEventLog("null"));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeEventLog("[null]"));

            string negativeTick = JsonSerializer.Serialize(new[]
            {
                new PrototypeEventRecord
                {
                    Tick = -1,
                    EventType = "test.event",
                    Message = "negative"
                }
            });
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeEventLog(negativeTick));

            string outOfOrder = JsonSerializer.Serialize(new[]
            {
                new PrototypeEventRecord
                {
                    Tick = 2,
                    EventType = "test.event",
                    Message = "first"
                },
                new PrototypeEventRecord
                {
                    Tick = 1,
                    EventType = "test.event",
                    Message = "second"
                }
            });
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeEventLog(outOfOrder));

            string oversizedMessage = JsonSerializer.Serialize(new[]
            {
                new PrototypeEventRecord
                {
                    Tick = 0,
                    EventType = "test.event",
                    Message = new string(
                        'x',
                        PrototypeRunArtifactManager.MaximumMessageLength + 1)
                }
            });
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeEventLog(oversizedMessage));

            string excessiveRows = $"[{string.Join(
                ',',
                Enumerable.Repeat(
                    "{}",
                    PrototypeRunArtifactManager.MaximumEventRows + 1))}]";
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeEventLog(excessiveRows));
        }

        [Fact]
        public void PublicRunSummaryDeserializerRejectsNullFutureNegativeAndOversizedState()
        {
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary("null"));

            PrototypeRunSummary currentSummary = PrototypePersistenceService.DeserializeRunSummary(
                PrototypePersistenceService.SerializeRunSummary(
                    new PrototypeRunSummary
                    {
                        SchemaVersion = 12,
                        EndHour = 8.0f,
                        Causeway = new PrototypeCausewayState(new PrototypeCausewayDefinition()).CaptureSnapshot(),
                        CausewayEvents = new List<PrototypeEventRecord>()
                    }));
            Assert.Equal(12, currentSummary.SchemaVersion);
            Assert.NotNull(currentSummary.Causeway);

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary(
                    PrototypePersistenceService.SerializeRunSummary(
                        new PrototypeRunSummary { SchemaVersion = 13 })));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary(
                    PrototypePersistenceService.SerializeRunSummary(
                        new PrototypeRunSummary { SimulationTick = -1 })));

            PrototypeRunSummary oversizedDictionary = new();
            for (int index = 0;
                index <= PrototypeRunArtifactManager.MaximumDictionaryEntries;
                index++)
            {
                oversizedDictionary.EventCountsByType[$"e{index}"] = 1;
            }
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary(
                    PrototypePersistenceService.SerializeRunSummary(
                        oversizedDictionary)));

            PrototypeRunSummary oversizedString = new()
            {
                ScenarioDisplayName = new string(
                    'x',
                    PrototypeRunArtifactManager.MaximumMessageLength + 1)
            };
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeRunSummary(
                    PrototypePersistenceService.SerializeRunSummary(
                        oversizedString)));
        }

        [Fact]
        public void PublicFileLoadersRoundTripCurrentAndLegacyContracts()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            foreach (int schemaVersion in new[] { 5, 6, 7, 8 })
            {
                PrototypeRuntimeSnapshot snapshot = new()
                {
                    SchemaVersion = schemaVersion
                };
                string snapshotPath = fixture.GetPath($"snapshot-v{schemaVersion}.json");
                PrototypePersistenceService.SaveSnapshot(snapshotPath, snapshot);
                Assert.Equal(
                    schemaVersion,
                    PrototypePersistenceService.LoadSnapshot(snapshotPath).SchemaVersion);

                PrototypeRunSummary summary = new()
                {
                    SchemaVersion = schemaVersion
                };
                string summaryPath = fixture.GetPath($"summary-v{schemaVersion}.json");
                PrototypePersistenceService.SaveRunSummary(summaryPath, summary);
                Assert.Equal(
                    schemaVersion,
                    PrototypePersistenceService.LoadRunSummary(summaryPath).SchemaVersion);
            }

            PrototypeEventLog eventLog = new();
            eventLog.Record(0, "test.current", "current");
            string eventLogPath = fixture.GetPath("event-log.json");
            PrototypePersistenceService.SaveEventLog(eventLogPath, eventLog);
            List<PrototypeEventRecord> restored =
                PrototypePersistenceService.LoadEventLog(eventLogPath);
            Assert.Single(restored);
            Assert.Equal(0, restored[0].Tick);

            foreach (int schemaVersion in new[] { 1, 2, 3 })
            {
                PrototypeWorldSummary worldSummary = new()
                {
                    SchemaVersion = schemaVersion
                };
                string worldSummaryPath = fixture.GetPath(
                    $"world-summary-v{schemaVersion}.json");
                PrototypePersistenceService.SaveWorldSummary(
                    worldSummaryPath,
                    worldSummary);
                Assert.Equal(
                    schemaVersion,
                    PrototypePersistenceService.LoadWorldSummary(
                        worldSummaryPath).SchemaVersion);
            }
        }

        [Fact]
        public void PublicSnapshotSaveRejectsInvalidOverwriteAndPreservesLoadableFile()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            string path = fixture.GetPath("snapshot.json");
            PrototypePersistenceService.SaveSnapshot(path, new PrototypeRuntimeSnapshot());
            byte[] committed = File.ReadAllBytes(path);
            PrototypeRuntimeSnapshot oversized = new()
            {
                WorldHash = new string(
                    'x',
                    PrototypeRunArtifactManager.MaximumMessageLength + 1)
            };

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.SaveSnapshot(path, oversized));
            Assert.Equal(committed, File.ReadAllBytes(path));
            Assert.Equal(9, PrototypePersistenceService.LoadSnapshot(path).SchemaVersion);
        }

        [Fact]
        public void PublicEventLogSaveRejectsInvalidOverwriteAndPreservesLoadableFile()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            string path = fixture.GetPath("event-log.json");
            PrototypeEventLog committedLog = new();
            committedLog.Record(0, "test.valid", "valid");
            PrototypePersistenceService.SaveEventLog(path, committedLog);
            byte[] committed = File.ReadAllBytes(path);
            PrototypeEventLog excessiveLog = new();
            for (int index = 0;
                index <= PrototypeRunArtifactManager.MaximumEventRows;
                index++)
            {
                excessiveLog.Record(index, "test.excessive", "excessive");
            }

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.SaveEventLog(path, excessiveLog));
            Assert.Equal(committed, File.ReadAllBytes(path));
            Assert.Single(PrototypePersistenceService.LoadEventLog(path));
        }

        [Fact]
        public void PublicRunSummarySaveRejectsInvalidOverwriteAndPreservesLoadableFile()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            string path = fixture.GetPath("run-summary.json");
            PrototypePersistenceService.SaveRunSummary(path, new PrototypeRunSummary());
            byte[] committed = File.ReadAllBytes(path);
            PrototypeRunSummary excessiveSummary = new();
            for (int index = 0;
                index <= PrototypeRunArtifactManager.MaximumDictionaryEntries;
                index++)
            {
                excessiveSummary.EventCountsByType[$"e{index}"] = 1;
            }

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.SaveRunSummary(path, excessiveSummary));
            Assert.Equal(committed, File.ReadAllBytes(path));
            Assert.Equal(9, PrototypePersistenceService.LoadRunSummary(path).SchemaVersion);
        }

        [Fact]
        public void PublicWorldSummaryPairRejectsInvalidOverwriteAndPreservesLoadableFile()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            string path = fixture.GetPath("world-summary.json");
            PrototypePersistenceService.SaveWorldSummary(path, new PrototypeWorldSummary());
            byte[] committed = File.ReadAllBytes(path);
            PrototypeWorldSummary oversized = new()
            {
                TerrainMode = new string(
                    'x',
                    PrototypeRunArtifactManager.MaximumMessageLength + 1)
            };

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.SaveWorldSummary(path, oversized));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeWorldSummary("null"));
            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeWorldSummary(
                    PrototypePersistenceService.SerializeWorldSummary(
                        new PrototypeWorldSummary { SchemaVersion = 4 })));
            Assert.Equal(committed, File.ReadAllBytes(path));
            Assert.Equal(3, PrototypePersistenceService.LoadWorldSummary(path).SchemaVersion);
        }

        [Fact]
        public void PublicWorldSummarySaveRejectsNegativeMetricsAndPreservesLoadableFile()
        {
            using TemporaryDirectory fixture = TemporaryDirectory.Create();
            string path = fixture.GetPath("world-summary.json");
            PrototypePersistenceService.SaveWorldSummary(path, new PrototypeWorldSummary());
            byte[] committed = File.ReadAllBytes(path);
            PrototypeWorldSummary[] invalidSummaries =
            {
                new() { WorldSize = -0.01f },
                new() { CellSizeMeters = -0.01f },
                new() { AverageMovementCost = -0.01f }
            };

            foreach (PrototypeWorldSummary invalid in invalidSummaries)
            {
                Assert.Throws<InvalidDataException>(() =>
                    PrototypePersistenceService.SaveWorldSummary(path, invalid));
                Assert.Equal(committed, File.ReadAllBytes(path));
                Assert.Equal(
                    3,
                    PrototypePersistenceService.LoadWorldSummary(path).SchemaVersion);
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            private TemporaryDirectory(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public static TemporaryDirectory Create()
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"societies-public-persistence-tests-{Guid.NewGuid():N}");
                Directory.CreateDirectory(path);
                return new TemporaryDirectory(path);
            }

            public string GetPath(string fileName)
            {
                return System.IO.Path.Combine(Path, fileName);
            }

            public string CreateSparseFile(string fileName, long byteLength)
            {
                string path = GetPath(fileName);
                using FileStream stream = new(
                    path,
                    FileMode.CreateNew,
                    System.IO.FileAccess.Write,
                    FileShare.None);
                stream.SetLength(byteLength);
                return path;
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}
