using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class VoxelWorldcraftTests
    {
        [Fact]
        public void Gather_IsAtomicAndBoundedInventoryRejectsWithoutMutatingWorld()
        {
            PrototypeRuntimeSession session = Create();
            VoxelCoord wood = FindExposed(session, VoxelMaterialId.Wood);
            string hash = session.VoxelStateHash;
            WorldcraftGatherResult first = session.GatherVoxel(wood);
            Assert.True(first.Accepted); Assert.Equal("wood", first.ItemId); Assert.Equal(1, session.Inventory.GetCount("wood")); Assert.NotEqual(hash, session.VoxelStateHash);
            session.Inventory.AddItem("wood", 255);
            VoxelCoord secondWood = FindExposed(session, VoxelMaterialId.Wood);
            string before = session.VoxelStateHash;
            WorldcraftGatherResult full = session.GatherVoxel(secondWood);
            Assert.False(full.Accepted); Assert.Equal(WorldcraftRejection.InventoryFull, full.Rejection); Assert.Equal(before, session.VoxelStateHash); Assert.Equal(VoxelMaterialId.Wood, session.GetVoxelMaterial(secondWood));
        }

        [Fact]
        public void PlaceRotateOverlapAndDismantle_AreAuthoritativeAndAtomic()
        {
            PrototypeRuntimeSession session = Create();
            VoxelCoord source = FindExposed(session, VoxelMaterialId.Wood);
            Assert.True(session.GatherVoxel(source).Accepted);
            WorldcraftPlacementCommand post = new() { ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0, PieceId = "wood_post", Anchor = source, ActorCell = source };
            Assert.True(session.EvaluateWorldcraftPlacement(post).IsValid);
            WorldcraftCommandResult placed = session.PlaceWorldcraftPiece(post);
            Assert.True(placed.Accepted); Assert.Equal(0, session.Inventory.GetCount("wood")); Assert.Single(session.ConstructionPieces);
            WorldcraftCommandResult overlap = session.PlaceWorldcraftPiece(post);
            Assert.False(overlap.Accepted); Assert.Equal(WorldcraftRejection.StaleRevision, overlap.Rejection);
            WorldcraftDismantleCommand dismantle = new() { ActorId = "player", Tick = 0, ExpectedConstructionRevision = session.ConstructionRevision, PieceInstanceId = placed.Piece!.InstanceId, ActorCell = source };
            Assert.True(session.DismantleWorldcraftPiece(dismantle).Accepted); Assert.Empty(session.ConstructionPieces); Assert.Equal(1, session.Inventory.GetCount("wood"));

            WorldcraftPlacementCommand invalidRotation = new() { ActorId = "player", Tick = 0, ExpectedConstructionRevision = session.ConstructionRevision, PieceId = "wood_post", RotationQuarterTurns = 1, Anchor = source, ActorCell = source };
            Assert.Equal(WorldcraftRejection.InvalidRotation, session.EvaluateWorldcraftPlacement(invalidRotation).Rejection);
            WorldcraftPlacementCommand far = new() { ActorId = "player", Tick = 0, ExpectedConstructionRevision = session.ConstructionRevision, PieceId = "wood_post", Anchor = new VoxelCoord(source.X + 20, source.Y, source.Z), ActorCell = source };
            Assert.Equal(WorldcraftRejection.OutOfRange, session.EvaluateWorldcraftPlacement(far).Rejection);
        }

        [Fact]
        public void SaveResumeAndV10Migration_PreserveOrCreateCanonicalConstructionState()
        {
            PrototypeRuntimeSession continuous = Create();
            VoxelCoord source = FindExposed(continuous, VoxelMaterialId.Wood);
            Assert.True(continuous.GatherVoxel(source).Accepted);
            Assert.True(continuous.PlaceWorldcraftPiece(new WorldcraftPlacementCommand { ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0, PieceId = "wood_post", Anchor = source, ActorCell = source }).Accepted);
            PrototypeRuntimeSnapshot saved = continuous.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(12, saved.SchemaVersion); Assert.NotNull(saved.Causeway); Assert.Single(saved.Construction!.Pieces); Assert.Equal(2, saved.Construction.Events.Count); Assert.Contains(saved.Construction.Events, value => value.Kind == "gather");
            PrototypeRuntimeSession resumed = Create(); resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(saved)));
            Assert.Equal(continuous.ConstructionRevision, resumed.ConstructionRevision); Assert.Equal(continuous.ConstructionPieces.Single().InstanceId, resumed.ConstructionPieces.Single().InstanceId);

            PrototypeRuntimeSnapshot v10 = continuous.CaptureSnapshot(Vector3.Zero); v10.SchemaVersion = 10; v10.Construction = null; v10.Inventory.Clear();
            PrototypeRuntimeSession migrated = Create(); migrated.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(v10)));
            PrototypeRuntimeSnapshot migratedV12 = migrated.CaptureSnapshot(Vector3.Zero);
            Assert.Empty(migrated.ConstructionPieces); Assert.Empty(migrated.Inventory.Items); Assert.Equal(12, migratedV12.SchemaVersion); Assert.NotNull(migratedV12.Causeway);
            Assert.Equal(10, migratedV12.Causeway!.MigrationSourceSchemaVersion);
            Assert.Equal(v10.WorldHash, migratedV12.WorldHash);
            Assert.Equal(v10.VoxelWorld!.RootHash, migratedV12.VoxelWorld!.RootHash);

            PrototypeRuntimeSnapshot malformed = continuous.CaptureSnapshot(Vector3.Zero); malformed.Construction!.Pieces[0].PieceId = "unknown";
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(malformed)));
        }

        [Fact]
        public void BoundedPolicy_IsAtomicAndApplySnapshotInstallsItWithoutInitialize()
        {
            InventoryComponent inventory = new();
            inventory.AddItem("legacy", 1);
            Assert.Throws<InvalidOperationException>(() => inventory.ConfigureBoundedStorage(8, 32, VoxelWorldcraftCatalog.HotbarOrder));
            inventory.AddItem("still-legacy", 1);
            Assert.Equal(1, inventory.GetCount("legacy"));

            PrototypeRuntimeSnapshot snapshot = Create().CaptureSnapshot(Vector3.Zero);
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession uninitialized = new(bundle.Scenarios.Resolve("snow_globe_voxel"), bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            uninitialized.Inventory.AddItem("pre-load-junk", 5);
            uninitialized.ApplySnapshot(snapshot);
            Assert.Equal(VoxelWorldcraftCatalog.HotbarSlots, uninitialized.Inventory.SlotLimit);
            Assert.Equal(VoxelWorldcraftCatalog.StackLimit, uninitialized.Inventory.StackLimit);
            Assert.Empty(uninitialized.Inventory.Items);
            Assert.False(uninitialized.Inventory.CanAddItem("unknown", 1));
            Assert.False(uninitialized.Inventory.CanAddItem("wood", 257));
            Assert.Throws<InvalidOperationException>(() => uninitialized.Inventory.AddItem("unknown", 1));

            snapshot.Inventory["unknown"] = 1;
            Assert.Throws<InvalidDataException>(() => PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(snapshot)));
        }

        [Fact]
        public void ConstructionProjectionsAndResults_AreDetachedFromAuthority()
        {
            PrototypeRuntimeSession session = Create();
            VoxelCoord wood = FindExposed(session, VoxelMaterialId.Wood);
            Assert.True(session.GatherVoxel(wood).Accepted);
            WorldcraftPlacementCommand command = new()
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0,
                PieceId = "wood_post", Anchor = wood, ActorCell = wood
            };
            WorldcraftPlacementEvaluation evaluation = session.EvaluateWorldcraftPlacement(command);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<VoxelCoord>)evaluation.Cells)[0] = new VoxelCoord(99, 99, 99));
            WorldcraftCommandResult result = session.PlaceWorldcraftPiece(command);
            result.Piece!.PieceId = "tampered";
            WorldcraftPieceSnapshot projected = session.ConstructionPieces.Single();
            projected.PieceId = "also-tampered";
            Assert.Equal("wood_post", session.CaptureSnapshot(Vector3.Zero).Construction!.Pieces.Single().PieceId);
        }

        [Fact]
        public void ConstructionReplay_RejectsTamperingFutureTicksAndSequenceDrift()
        {
            PrototypeRuntimeSession session = Create();
            VoxelCoord wood = FindExposed(session, VoxelMaterialId.Wood);
            Assert.True(session.GatherVoxel(wood).Accepted);
            Assert.True(session.PlaceWorldcraftPiece(new WorldcraftPlacementCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0,
                PieceId = "wood_post", Anchor = wood, ActorCell = wood
            }).Accepted);
            PrototypeRuntimeSnapshot canonical = session.CaptureSnapshot(Vector3.Zero);

            AssertRejected(CloneWith(canonical, clone => clone.Construction!.Events[1].Anchor = new VoxelCoord(9, 9, 9)));
            AssertRejected(CloneWith(canonical, clone => clone.Construction!.Events[1].InventoryDeltas["wood"] = -2));
            AssertRejected(CloneWith(canonical, clone => clone.Construction!.Events[0].WorldRevision = 2));
            AssertRejected(CloneWith(canonical, clone => clone.Construction!.Events[0].Tick = clone.SimulationTick + 1));
            AssertRejected(CloneWith(canonical, clone => clone.Construction!.NextPieceSequence = 9));
            AssertRejected(CloneWith(canonical, clone => clone.Construction!.Pieces[0].InstanceId = "piece-999999"));

            PrototypeRuntimeSession live = Create();
            string liveHash = live.VoxelStateHash;
            PrototypeRuntimeSnapshot future = CloneWith(canonical, clone => clone.Construction!.Events[0].Tick = clone.SimulationTick + 1);
            Assert.Throws<InvalidDataException>(() => live.ApplySnapshot(future));
            Assert.Equal(liveHash, live.VoxelStateHash);
            Assert.Empty(live.Inventory.Items);
            Assert.Empty(live.ConstructionPieces);
        }

        [Fact]
        public void ConstructionReplay_RejectsImpossibleInactivePieceHistoryAndIntermediateRevisionTampering()
        {
            PrototypeRuntimeSession session = Create();
            VoxelCoord wood = FindExposed(session, VoxelMaterialId.Wood);
            Assert.True(session.GatherVoxel(wood).Accepted);
            WorldcraftCommandResult placed = session.PlaceWorldcraftPiece(new WorldcraftPlacementCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0,
                PieceId = "wood_post", Anchor = wood, ActorCell = wood
            });
            Assert.True(placed.Accepted);
            Assert.True(session.DismantleWorldcraftPiece(new WorldcraftDismantleCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 1,
                PieceInstanceId = placed.Piece!.InstanceId, ActorCell = wood
            }).Accepted);
            PrototypeRuntimeSnapshot inactive = session.CaptureSnapshot(Vector3.Zero);
            Assert.Empty(inactive.Construction!.Pieces);

            AssertRejected(CloneWith(inactive, clone =>
            {
                clone.Construction!.Events[1].Anchor = new VoxelCoord(VoxelWorldModule.MaxXExclusive, 5, 0);
                clone.Construction.Events[2].Anchor = clone.Construction.Events[1].Anchor;
            }));
            AssertRejected(CloneWith(inactive, clone =>
            {
                clone.Construction!.Events[1].Anchor = new VoxelCoord(0, VoxelWorldModule.MaxYExclusive - 3, 0);
                clone.Construction.Events[2].Anchor = clone.Construction.Events[1].Anchor;
            }));
            AssertRejected(CloneWith(inactive, clone => clone.Construction!.Events[1].WorldRevision = 0));
            AssertRejected(CloneWith(inactive, clone => clone.Construction!.Events[2].WorldRevision = 0));
        }

        [Fact]
        public void GatherAndDismantle_RejectOrphaningAndEventCapacityBeforeMutation()
        {
            PrototypeRuntimeSession session = Create();
            VoxelCoord support = FindExposed(session, VoxelMaterialId.Wood);
            Assert.True(session.GatherVoxel(support).Accepted);
            WorldcraftCommandResult post = session.PlaceWorldcraftPiece(new WorldcraftPlacementCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0,
                PieceId = "wood_post", Anchor = support, ActorCell = support
            });
            Assert.True(post.Accepted);
            VoxelCoord below = support with { Y = support.Y - 1 };
            string beforeOrphanAttempt = session.VoxelStateHash;
            Assert.Equal(WorldcraftRejection.OrphanedSupport, session.GatherVoxel(below).Rejection);
            Assert.Equal(beforeOrphanAttempt, session.VoxelStateHash);

            PrototypeRuntimeSession stacked = Create();
            for (int i = 0; i < 4; i++) Assert.True(stacked.GatherVoxel(FindExposed(stacked, VoxelMaterialId.Wood)).Accepted);
            VoxelCoord anchor = FindExposed(stacked, VoxelMaterialId.Soil);
            Assert.True(stacked.GatherVoxel(anchor).Accepted);
            WorldcraftCommandResult floor = stacked.PlaceWorldcraftPiece(new WorldcraftPlacementCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 0,
                PieceId = "wood_floor", Anchor = anchor, ActorCell = anchor
            });
            Assert.True(floor.Accepted);
            Assert.True(stacked.PlaceWorldcraftPiece(new WorldcraftPlacementCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 1,
                PieceId = "wood_wall", Anchor = anchor with { Y = anchor.Y + 1 }, ActorCell = anchor
            }).Accepted);
            Assert.Equal(WorldcraftRejection.OrphanedSupport, stacked.DismantleWorldcraftPiece(new WorldcraftDismantleCommand
            {
                ActorId = "player", Tick = 0, ExpectedConstructionRevision = 2,
                PieceInstanceId = floor.Piece!.InstanceId, ActorCell = anchor
            }).Rejection);

            FieldInfo stateField = typeof(PrototypeRuntimeSession).GetField("_worldcraft", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object state = stateField.GetValue(stacked)!;
            PropertyInfo eventSequence = state.GetType().GetProperty("EventSequence")!;
            eventSequence.SetValue(state, PrototypePersistenceBounds.MaximumSnapshotRows);
            VoxelCoord capacityTarget = FindExposed(stacked, VoxelMaterialId.Wood);
            string capacityHash = stacked.VoxelStateHash;
            Assert.Equal(WorldcraftRejection.EventCapacityReached, stacked.GatherVoxel(capacityTarget).Rejection);
            Assert.Equal(capacityHash, stacked.VoxelStateHash);
        }

        [Fact]
        public void ConstructionRestore_MaximumTrackedEditHistoryUsesOneLinearReplayCursor()
        {
            PrototypeRuntimeSession session = Create();
            Assert.True(session.GatherVoxel(FindExposed(session, VoxelMaterialId.Wood)).Accepted);
            VoxelCoord target = FindExposed(session, VoxelMaterialId.Soil);
            for (int index = 1; index < PrototypePersistenceBounds.MaximumSnapshotRows; index++)
            {
                VoxelMaterialId before = session.GetVoxelMaterial(target);
                bool remove = before != VoxelMaterialId.Air;
                VoxelEditResult result = session.ExecuteVoxelEdit(new VoxelEditCommand
                {
                    ActorId = "player", Tick = session.SimulationTick, ExpectedWorldRevision = index,
                    Kind = remove ? VoxelEditKind.Remove : VoxelEditKind.Place, Coord = target,
                    ExpectedBefore = before, After = remove ? VoxelMaterialId.Air : VoxelMaterialId.Soil
                });
                Assert.True(result.Accepted, $"Tracked edit {index} was rejected as {result.Rejection}.");
            }

            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(PrototypePersistenceBounds.MaximumSnapshotRows, snapshot.VoxelWorld!.Events.Count);
            Assert.Equal(PrototypePersistenceBounds.MaximumSnapshotRows, snapshot.Construction!.Events.Count);
            VoxelWorldModule restoredWorld = VoxelWorldModule.Restore(snapshot.VoxelWorld);
            WorldcraftConstructionState restored = WorldcraftConstructionState.Restore(snapshot.Construction,
                restoredWorld, snapshot.Inventory, snapshot.SimulationTick, out WorldcraftRestoreReplayWork replayWork);

            Assert.Equal(1, replayWork.WorldGenerationCount);
            Assert.Equal(PrototypePersistenceBounds.MaximumSnapshotRows, replayWork.AppliedVoxelEventCount);
            Assert.Equal(snapshot.VoxelWorld.RootHash, restoredWorld.RootHash);
            Assert.Equal(snapshot.Construction.EventSequence, restored.CaptureSnapshot().EventSequence);
            Assert.Empty(restored.CapturePieces());
        }

        private static PrototypeRuntimeSnapshot CloneWith(PrototypeRuntimeSnapshot snapshot, Action<PrototypeRuntimeSnapshot> mutate)
        {
            PrototypeRuntimeSnapshot clone = System.Text.Json.JsonSerializer.Deserialize<PrototypeRuntimeSnapshot>(
                System.Text.Json.JsonSerializer.Serialize(snapshot))!;
            mutate(clone);
            return clone;
        }

        private static void AssertRejected(PrototypeRuntimeSnapshot snapshot) => Assert.Throws<InvalidDataException>(() =>
            PrototypePersistenceService.DeserializeSnapshot(PrototypePersistenceService.SerializeSnapshot(snapshot)));

        private static PrototypeRuntimeSession Create()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("snow_globe_voxel"), bundle.RoleQuotas.Roles, resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f); return session;
        }

        private static VoxelCoord FindExposed(PrototypeRuntimeSession session, VoxelMaterialId wanted)
        {
            for (int x = VoxelWorldModule.MinX; x < VoxelWorldModule.MaxXExclusive; x++) for (int z = VoxelWorldModule.MinZ; z < VoxelWorldModule.MaxZExclusive; z++)
            for (int y = VoxelWorldModule.MaxYExclusive - 1; y >= 1; y--) if (session.GetVoxelMaterial(new VoxelCoord(x, y, z)) == wanted && session.GetVoxelMaterial(new VoxelCoord(x, y + 1, z)) == VoxelMaterialId.Air) return new VoxelCoord(x, y, z);
            throw new InvalidOperationException("Required exposed voxel is unavailable.");
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current)) { string candidate = Path.Combine(current, "src", "societies", "data"); if (Directory.Exists(candidate)) return PrototypeCatalogLoader.LoadFromDirectory(candidate); current = Directory.GetParent(current)?.FullName; }
            throw new DirectoryNotFoundException();
        }
    }
}
