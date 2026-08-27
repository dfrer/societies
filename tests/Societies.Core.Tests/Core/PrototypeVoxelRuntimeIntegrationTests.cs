using Godot;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeVoxelRuntimeIntegrationTests
    {
        [Fact]
        public void Catalog_RegistersExactlyOneZeroCitizenVoxelScenario()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = Assert.Single(bundle.Scenarios.Scenarios.Where(candidate => candidate.WorldModel == PrototypeWorldModels.Voxel));

            Assert.Equal("snow_globe_voxel", scenario.Id);
            Assert.Equal(0, scenario.InitialCitizens);
            Assert.Equal(PrototypeWorldModels.Heightfield, bundle.Scenarios.Resolve("balanced_basin").WorldModel);
        }

        [Fact]
        public void Initialize_SelectsOneWorldAuthorityAndPreservesLegacyHeightfieldPath()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession voxel = CreateVoxelSession(bundle);
            voxel.Initialize(8.0f);

            Assert.True(voxel.UsesVoxelWorld);
            Assert.Null(voxel.World);
            Assert.Empty(voxel.Workers);
            Assert.Empty(voxel.ResourceSnapshots);
            Assert.Equal(260827, voxel.WorldSeed);
            Assert.Equal(0, voxel.WorldGenerationAttempt);
            Assert.Equal(64, voxel.WorldHash.Length);
            Assert.Equal(64, voxel.VoxelStateHash.Length);
            Assert.NotEmpty(voxel.CaptureVoxelProjection(new[] { new VoxelChunkCoord(0, 0, 0) }).Chunks);
            voxel.Scenario.WorldModel = PrototypeWorldModels.Heightfield;
            Assert.True(voxel.UsesVoxelWorld);
            Assert.Equal(10, voxel.CaptureSnapshot(Vector3.Zero).SchemaVersion);
            Assert.Null(voxel.World);

            PrototypeScenarioDefinition legacyScenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession legacy = new(legacyScenario, bundle.RoleQuotas.Roles, resourceDefinitions: bundle.Resources.Resources);
            legacy.Initialize(8.0f);
            Assert.False(legacy.UsesVoxelWorld);
            Assert.NotNull(legacy.World);
            Assert.Equal(legacyScenario.InitialCitizens, legacy.Workers.Count);
        }

        [Fact]
        public void EditSnapshotRestoreAndReplay_PreserveStateHashAndProjection()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession continuous = CreateVoxelSession(bundle);
            continuous.Initialize(8.0f);
            VoxelCoord target = FindEditable(continuous, 0, 0);
            string worldIdentity = continuous.WorldHash;
            string initialStateHash = continuous.VoxelStateHash;
            VoxelMaterialId initialMaterial = continuous.GetVoxelMaterial(target);

            VoxelEditResult futureCommand = continuous.ExecuteVoxelEdit(new VoxelEditCommand
            {
                ActorId = "player",
                Tick = 1,
                ExpectedWorldRevision = 0,
                Kind = VoxelEditKind.Remove,
                Coord = target,
                ExpectedBefore = initialMaterial,
                After = VoxelMaterialId.Air
            });
            Assert.False(futureCommand.Accepted);
            Assert.Equal(VoxelEditRejection.TickMismatch, futureCommand.Rejection);
            Assert.Equal(initialStateHash, continuous.VoxelStateHash);
            Assert.Equal(initialMaterial, continuous.GetVoxelMaterial(target));

            VoxelEditResult removed = continuous.ExecuteVoxelEdit(new VoxelEditCommand
            {
                ActorId = "player",
                Tick = 0,
                ExpectedWorldRevision = 0,
                Kind = VoxelEditKind.Remove,
                Coord = target,
                ExpectedBefore = initialMaterial,
                After = VoxelMaterialId.Air
            });
            Assert.True(removed.Accepted);
            Assert.Equal(worldIdentity, continuous.WorldHash);
            Assert.NotEqual(initialStateHash, continuous.VoxelStateHash);
            Assert.Equal(VoxelMaterialId.Air, continuous.GetVoxelMaterial(target));
            Assert.NotEmpty(continuous.CaptureVoxelProjection(removed.DirtyChunks).Chunks);

            PrototypeRuntimeSnapshot checkpoint = continuous.CaptureSnapshot(Vector3.Zero);
            string json = PrototypePersistenceService.SerializeSnapshot(checkpoint);
            Assert.True(Encoding.UTF8.GetByteCount(json) < PrototypeRunArtifactManager.MaximumSnapshotBytes);
            PrototypeRuntimeSnapshot roundTripped = PrototypePersistenceService.DeserializeSnapshot(json);
            PrototypeRuntimeSession resumed = CreateVoxelSession(bundle);
            resumed.ApplySnapshot(roundTripped);

            Assert.Equal(continuous.WorldHash, resumed.WorldHash);
            Assert.Equal(continuous.VoxelStateHash, resumed.VoxelStateHash);
            Assert.Equal(ProjectionFingerprint(continuous.CaptureVoxelProjection()), ProjectionFingerprint(resumed.CaptureVoxelProjection()));

            string resumedBeforeInvalid = PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero));
            PrototypeRuntimeSnapshot foreignIdentity = PrototypePersistenceService.DeserializeSnapshot(json);
            VoxelWorldModule foreignWorld = new(foreignIdentity.WorldSeed + 1);
            foreignIdentity.VoxelWorld = foreignWorld.CaptureSnapshot();
            foreignIdentity.WorldHash = foreignWorld.WorldIdentity;
            Assert.Throws<InvalidDataException>(() => resumed.ApplySnapshot(foreignIdentity));
            Assert.Equal(resumedBeforeInvalid, PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));

            PrototypeRuntimeSnapshot heightfieldShell = PrototypePersistenceService.DeserializeSnapshot(json);
            heightfieldShell.Settlement!.ProducedResources["logs"] = 1;
            string malformedShellJson = PrototypePersistenceService.SerializeSnapshot(heightfieldShell);
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot(malformedShellJson));
            Assert.Throws<InvalidDataException>(() => resumed.ApplySnapshot(heightfieldShell));
            Assert.Equal(resumedBeforeInvalid, PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));

            PrototypeRuntimeSnapshot futureEvent = PrototypePersistenceService.DeserializeSnapshot(json);
            futureEvent.VoxelWorld!.Events[0] = futureEvent.VoxelWorld.Events[0] with { Tick = 1 };
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(futureEvent)));
            Assert.Throws<InvalidDataException>(() => resumed.ApplySnapshot(futureEvent));
            Assert.Equal(resumedBeforeInvalid, PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));

            continuous.Advance(0.05f, 600.0f);
            resumed.Advance(0.05f, 600.0f);

            VoxelEditCommand replayedPlace = new()
            {
                ActorId = "player",
                Tick = 1,
                ExpectedWorldRevision = 1,
                Kind = VoxelEditKind.Place,
                Coord = target,
                ExpectedBefore = VoxelMaterialId.Air,
                After = VoxelMaterialId.Stone
            };
            Assert.True(continuous.ExecuteVoxelEdit(replayedPlace).Accepted);
            Assert.True(resumed.ExecuteVoxelEdit(replayedPlace).Accepted);
            Assert.Equal(continuous.VoxelStateHash, resumed.VoxelStateHash);

            PrototypeRuntimeSnapshot outOfOrderEvents = continuous.CaptureSnapshot(Vector3.Zero);
            outOfOrderEvents.VoxelWorld!.Events[0] = outOfOrderEvents.VoxelWorld.Events[0] with { Tick = 1 };
            outOfOrderEvents.VoxelWorld.Events[1] = outOfOrderEvents.VoxelWorld.Events[1] with { Tick = 0 };
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(outOfOrderEvents)));
            string beforeOutOfOrder = PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero));
            Assert.Throws<InvalidDataException>(() => resumed.ApplySnapshot(outOfOrderEvents));
            Assert.Equal(beforeOutOfOrder, PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));

            string inertHash = resumed.VoxelStateHash;
            VoxelEditResult stale = resumed.ExecuteVoxelEdit(replayedPlace);
            Assert.False(stale.Accepted);
            Assert.Equal(VoxelEditRejection.StaleRevision, stale.Rejection);
            Assert.Equal(inertHash, resumed.VoxelStateHash);
        }

        [Fact]
        public void SchemaV10Artifacts_RoundTripWithinExistingBoundedEnvelope()
        {
            string outputDirectory = Path.Combine(Path.GetTempPath(), $"societies-voxel-{Guid.NewGuid():N}");
            string? original = System.Environment.GetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR");
            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
                PrototypeCatalogBundle bundle = LoadCatalogs();
                PrototypeRuntimeSession session = CreateVoxelSession(bundle);
                session.Initialize(8.0f);
                PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
                PrototypeWorldSummary summary = PrototypeWorldSummaryBuilder.Build(session, null, session.ActiveResourceSnapshots);
                PrototypeRunArtifactManager manager = new();

                string path = manager.SaveArtifacts(session, snapshot, summary);
                PrototypeLoadedArtifacts loaded = Assert.IsType<PrototypeLoadedArtifacts>(manager.LoadLatestArtifacts());

                Assert.True(File.Exists(path));
                Assert.Equal(10, loaded.Snapshot.SchemaVersion);
                Assert.Equal(snapshot.WorldHash, loaded.Snapshot.WorldHash);
                Assert.Equal(PrototypeWorldModels.Voxel, summary.TerrainMode);
                Assert.True(summary.GridWidth > 0 && summary.GridHeight > 0 && summary.BuildableCellCount > 0);
                Assert.Equal(snapshot.WorldHash, summary.WorldHash);
                string persistedBeforeInvalid = File.ReadAllText(path);

                PrototypeRuntimeSnapshot malformedShell = PrototypePersistenceService.DeserializeSnapshot(
                    PrototypePersistenceService.SerializeSnapshot(snapshot));
                malformedShell.Settlement!.CentralDepot.Position = new PrototypeSerializableVector3 { X = 1.0f };
                Assert.Throws<InvalidDataException>(() => manager.SaveArtifacts(session, malformedShell, summary));
                Assert.Equal(persistedBeforeInvalid, File.ReadAllText(path));

                VoxelCoord target = FindEditable(session, 0, 0);
                VoxelMaterialId material = session.GetVoxelMaterial(target);
                Assert.True(session.ExecuteVoxelEdit(new VoxelEditCommand { ActorId = "player", Tick = 0, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = target, ExpectedBefore = material, After = VoxelMaterialId.Air }).Accepted);
                session.Advance(0.05f, 600.0f);
                Assert.True(session.ExecuteVoxelEdit(new VoxelEditCommand { ActorId = "player", Tick = 1, ExpectedWorldRevision = 1, Kind = VoxelEditKind.Place, Coord = target, ExpectedBefore = VoxelMaterialId.Air, After = material }).Accepted);
                PrototypeRuntimeSnapshot outOfOrderEvents = session.CaptureSnapshot(Vector3.Zero);
                outOfOrderEvents.VoxelWorld!.Events[0] = outOfOrderEvents.VoxelWorld.Events[0] with { Tick = 1 };
                outOfOrderEvents.VoxelWorld.Events[1] = outOfOrderEvents.VoxelWorld.Events[1] with { Tick = 0 };
                Assert.Throws<InvalidDataException>(() => manager.SaveArtifacts(session, outOfOrderEvents, summary));
                Assert.Equal(persistedBeforeInvalid, File.ReadAllText(path));

                PrototypeRuntimeSnapshot foreignIdentity = PrototypePersistenceService.DeserializeSnapshot(
                    PrototypePersistenceService.SerializeSnapshot(snapshot));
                VoxelWorldModule foreignWorld = new(snapshot.WorldSeed + 1);
                foreignIdentity.VoxelWorld = foreignWorld.CaptureSnapshot();
                foreignIdentity.WorldHash = foreignWorld.WorldIdentity;
                Assert.Throws<InvalidDataException>(() => manager.SaveArtifacts(session, foreignIdentity, summary));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", original);
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void SchemaV10NestedPayload_IsStrictAndBounded()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = CreateVoxelSession(bundle);
            session.Initialize(8.0f);
            VoxelCoord target = FindEditable(session, 0, 0);
            Assert.True(session.ExecuteVoxelEdit(new VoxelEditCommand { ActorId = "player", Tick = 0, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = target, ExpectedBefore = session.GetVoxelMaterial(target), After = VoxelMaterialId.Air }).Accepted);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            string canonical = PrototypePersistenceService.SerializeSnapshot(snapshot);

            void AssertMissingRejected(Action<JsonObject> mutate)
            {
                JsonObject root = JsonNode.Parse(canonical)!.AsObject();
                mutate(root);
                Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot(root.ToJsonString()));
            }

            foreach (string property in typeof(VoxelWorldSnapshot).GetProperties().Select(value => value.Name))
            {
                AssertMissingRejected(root => root[nameof(PrototypeRuntimeSnapshot.VoxelWorld)]!.AsObject().Remove(property));
            }
            foreach (string property in typeof(VoxelChunkSnapshot).GetProperties().Select(value => value.Name))
            {
                AssertMissingRejected(root => root[nameof(PrototypeRuntimeSnapshot.VoxelWorld)]![nameof(VoxelWorldSnapshot.Chunks)]!.AsArray()[0]!.AsObject().Remove(property));
            }
            foreach (string property in typeof(VoxelChangeEvent).GetProperties().Select(value => value.Name))
            {
                AssertMissingRejected(root => root[nameof(PrototypeRuntimeSnapshot.VoxelWorld)]![nameof(VoxelWorldSnapshot.Events)]!.AsArray()[0]!.AsObject().Remove(property));
            }
            foreach (string property in typeof(VoxelCoord).GetProperties().Select(value => value.Name))
            {
                AssertMissingRejected(root => root[nameof(PrototypeRuntimeSnapshot.VoxelWorld)]![nameof(VoxelWorldSnapshot.Events)]!.AsArray()[0]![nameof(VoxelChangeEvent.Coord)]!.AsObject().Remove(property));
            }

            snapshot.VoxelWorld!.Chunks[0].PayloadSegments[0] = new string('A', PrototypeRunArtifactManager.MaximumMessageLength + 1);

            Assert.Throws<InvalidDataException>(() =>
                PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(snapshot)));
        }

        [Fact]
        public void EditCapacity_ExactLimitSavesAndRestoresWhileLimitPlusOneIsInert()
        {
            string snapshotPath = Path.Combine(Path.GetTempPath(), $"societies-voxel-capacity-{Guid.NewGuid():N}.json");
            try
            {
                PrototypeCatalogBundle bundle = LoadCatalogs();
                PrototypeRuntimeSession session = CreateVoxelSession(bundle);
                session.Initialize(8.0f);
                VoxelCoord target = FindEditable(session, 0, 0);
                VoxelMaterialId placedMaterial = session.GetVoxelMaterial(target);

                for (long revision = 0; revision < VoxelWorldModule.MaximumEventCount; revision++)
                {
                    bool remove = revision % 2 == 0;
                    VoxelEditResult result = session.ExecuteVoxelEdit(new VoxelEditCommand
                    {
                        ActorId = "capacity-test",
                        Tick = 0,
                        ExpectedWorldRevision = revision,
                        Kind = remove ? VoxelEditKind.Remove : VoxelEditKind.Place,
                        Coord = target,
                        ExpectedBefore = remove ? placedMaterial : VoxelMaterialId.Air,
                        After = remove ? VoxelMaterialId.Air : placedMaterial
                    });
                    Assert.True(result.Accepted, $"Edit {revision + 1} should remain persistable and accepted.");
                }

                PrototypeRuntimeSnapshot atLimit = session.CaptureSnapshot(Vector3.Zero);
                Assert.Equal(PrototypePersistenceBounds.MaximumSnapshotRows, atLimit.VoxelWorld!.Events.Count);
                PrototypePersistenceService.SaveSnapshot(snapshotPath, atLimit);
                PrototypeRuntimeSnapshot loaded = PrototypePersistenceService.LoadSnapshot(snapshotPath);
                PrototypeRuntimeSession restored = CreateVoxelSession(bundle);
                restored.ApplySnapshot(loaded);
                Assert.Equal(session.VoxelStateHash, restored.VoxelStateHash);
                Assert.Equal(session.VoxelWorldRevision, restored.VoxelWorldRevision);

                string beforeRejected = restored.VoxelStateHash;
                VoxelEditResult rejected = restored.ExecuteVoxelEdit(new VoxelEditCommand
                {
                    ActorId = "capacity-test",
                    Tick = 0,
                    ExpectedWorldRevision = VoxelWorldModule.MaximumEventCount,
                    Kind = VoxelEditKind.Remove,
                    Coord = target,
                    ExpectedBefore = placedMaterial,
                    After = VoxelMaterialId.Air
                });
                Assert.False(rejected.Accepted);
                Assert.Equal(VoxelEditRejection.EventCapacityReached, rejected.Rejection);
                Assert.Equal(beforeRejected, restored.VoxelStateHash);
                Assert.Equal(VoxelWorldModule.MaximumEventCount, restored.VoxelWorldRevision);
                Assert.Equal(placedMaterial, restored.GetVoxelMaterial(target));
            }
            finally
            {
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                }
            }
        }

        private static PrototypeRuntimeSession CreateVoxelSession(PrototypeCatalogBundle bundle) =>
            new(bundle.Scenarios.Resolve("snow_globe_voxel"), bundle.RoleQuotas.Roles, resourceDefinitions: bundle.Resources.Resources);

        private static VoxelCoord FindEditable(PrototypeRuntimeSession session, int x, int z)
        {
            for (int y = 1; y < VoxelWorldModule.MaxYExclusive; y++)
            {
                VoxelCoord coord = new(x, y, z);
                if (session.GetVoxelMaterial(coord) is VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood)
                {
                    return coord;
                }
            }

            throw new InvalidOperationException("Voxel scenario unexpectedly has no editable material.");
        }

        private static string ProjectionFingerprint(VoxelWorldProjection projection)
        {
            StringBuilder builder = new();
            builder.Append(projection.Revision.ToString(CultureInfo.InvariantCulture));
            foreach (VoxelChunkGeometryProjection chunk in projection.Chunks)
            {
                builder.Append('|').Append(chunk.Coord);
                foreach (VoxelVertex vertex in chunk.Vertices)
                {
                    builder.Append(';').Append(vertex.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(vertex.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(vertex.Z.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append((byte)vertex.Material);
                }
                builder.Append(':').AppendJoin(',', chunk.Indices);
            }
            foreach (VoxelWalkableSpan span in projection.Walkable)
            {
                builder.Append('/').Append(span.X).Append(',').Append(span.Z).Append(',').Append(span.SupportY);
            }
            return builder.ToString();
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
            throw new DirectoryNotFoundException("Could not locate prototype catalogs.");
        }
    }
}
