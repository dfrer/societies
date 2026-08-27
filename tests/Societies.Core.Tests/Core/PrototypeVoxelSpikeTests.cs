using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeVoxelSpikeTests
    {
        [Fact]
        public void Generation_IsFiniteEagerAndReadOrderIndependent()
        {
            VoxelWorldModule first = new(1337); VoxelWorldModule second = new(1337);
            Assert.Equal(64, first.CaptureSnapshot().Chunks.Count);
            foreach (int x in new[] { -64, -1, 0, 63 }) foreach (int z in new[] { 63, 0, -1, -64 }) _ = first.GetMaterial(new(x, 10, z));
            Assert.Equal(second.RootHash, first.RootHash);
            Assert.Equal(VoxelMaterialId.Air, first.GetMaterial(new(-65, 10, 0)));
            Assert.Equal(VoxelMaterialId.Bedrock, first.GetMaterial(new(-64, 0, -64)));

            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                VoxelCoord edited = FindEditable(first, 0, 0);
                Assert.True(first.Execute(new VoxelEditCommand { ActorId = "culture|probe", Tick = 12, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = edited, ExpectedBefore = first.GetMaterial(edited), After = VoxelMaterialId.Air }).Accepted);
                string identity = first.WorldIdentity;
                string root = first.RootHash;
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                CultureInfo.CurrentUICulture = new CultureInfo("ar-SA");
                Assert.Equal("-4:0:3", new VoxelChunkCoord(-4, 0, 3).ToString());
                Assert.Equal(identity, first.WorldIdentity);
                Assert.Equal(root, first.RootHash);
                Assert.Equal(identity, VoxelWorldModule.GetWorldIdentity(1337));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Fact]
        public void Edit_IsAtomicAndReportsExactHorizontalBoundaryDirtyChunks()
        {
            VoxelWorldModule world = new(22); VoxelCoord target = FindEditable(world, x: -49, z: -49);
            VoxelMaterialId before = world.GetMaterial(target); string root = world.RootHash;
            VoxelEditResult stale = world.Execute(new VoxelEditCommand { ActorId = "player", Tick = 1, ExpectedWorldRevision = 4, Kind = VoxelEditKind.Remove, Coord = target, ExpectedBefore = before, After = VoxelMaterialId.Air });
            Assert.False(stale.Accepted); Assert.Equal(VoxelEditRejection.StaleRevision, stale.Rejection); Assert.Equal(root, world.RootHash);
            VoxelEditResult success = world.Execute(new VoxelEditCommand { ActorId = "player", Tick = 2, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = target, ExpectedBefore = before, After = VoxelMaterialId.Air });
            Assert.True(success.Accepted); Assert.Equal(1, success.WorldRevision); Assert.Equal(VoxelMaterialId.Air, world.GetMaterial(target)); Assert.Contains(new VoxelChunkCoord(-4, 0, -4), success.DirtyChunks); Assert.Contains(new VoxelChunkCoord(-3, 0, -4), success.DirtyChunks); Assert.Contains(new VoxelChunkCoord(-4, 0, -3), success.DirtyChunks);
            Assert.Collection(success.DirtyChunks, _ => { }, _ => { }, _ => { }); Assert.Single(world.Events);
        }

        [Fact]
        public void Edit_RejectionsAreClosedAndInert()
        {
            VoxelWorldModule world = new(7); VoxelCoord bedrock = new(0, 0, 0); string before = world.RootHash;
            Assert.Equal(VoxelEditRejection.ImmutableBedrock, world.Execute(new VoxelEditCommand { ActorId = "p", ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = bedrock, ExpectedBefore = VoxelMaterialId.Bedrock, After = VoxelMaterialId.Air }).Rejection);
            Assert.Equal(VoxelEditRejection.OutOfBounds, world.Execute(new VoxelEditCommand { ActorId = "p", ExpectedWorldRevision = 0, Kind = VoxelEditKind.Place, Coord = new(64, 1, 0), ExpectedBefore = VoxelMaterialId.Air, After = VoxelMaterialId.Wood }).Rejection);
            Assert.Equal(VoxelEditRejection.InvalidMaterial, world.Execute(new VoxelEditCommand { ActorId = "p", ExpectedWorldRevision = 0, Kind = VoxelEditKind.Place, Coord = new(0, 31, 0), ExpectedBefore = VoxelMaterialId.Air, After = (VoxelMaterialId)99 }).Rejection);
            Assert.Equal(VoxelEditRejection.InvalidTick, world.Execute(new VoxelEditCommand { ActorId = "p", Tick = -1, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = FindEditable(world, 0, 0), ExpectedBefore = VoxelMaterialId.Soil, After = VoxelMaterialId.Air }).Rejection);
            Assert.Equal(VoxelEditRejection.InvalidEditKind, world.Execute(new VoxelEditCommand { ActorId = "p", ExpectedWorldRevision = 0, Kind = (VoxelEditKind)99, Coord = FindEditable(world, 0, 0), ExpectedBefore = world.GetMaterial(FindEditable(world, 0, 0)), After = VoxelMaterialId.Air }).Rejection);
            VoxelCoord air = FindAir(world, 0, 0);
            Assert.Equal(VoxelEditRejection.InvalidMaterial, world.Execute(new VoxelEditCommand { ActorId = "p", ExpectedWorldRevision = 0, Kind = VoxelEditKind.Place, Coord = air, ExpectedBefore = VoxelMaterialId.Air, After = VoxelMaterialId.Bedrock }).Rejection);
            Assert.Equal(before, world.RootHash); Assert.Equal(0, world.WorldRevision);

            PropertyInfo revision = typeof(VoxelWorldModule).GetProperty(nameof(VoxelWorldModule.WorldRevision))!;
            PropertyInfo sequence = typeof(VoxelWorldModule).GetProperty(nameof(VoxelWorldModule.EventSequence))!;
            revision.SetValue(world, VoxelWorldModule.MaximumEventCount);
            sequence.SetValue(world, VoxelWorldModule.MaximumEventCount);
            string capacityRoot = world.RootHash;
            VoxelCoord editable = FindEditable(world, 0, 0);
            VoxelEditResult capacity = world.Execute(new VoxelEditCommand { ActorId = "p", ExpectedWorldRevision = VoxelWorldModule.MaximumEventCount, Kind = VoxelEditKind.Remove, Coord = editable, ExpectedBefore = world.GetMaterial(editable), After = VoxelMaterialId.Air });
            Assert.Equal(VoxelEditRejection.EventCapacityReached, capacity.Rejection);
            Assert.Equal(capacityRoot, world.RootHash);
            Assert.Equal(VoxelWorldModule.MaximumEventCount, world.WorldRevision);
        }

        [Fact]
        public void SnapshotRestore_RejectsCorruptionWithoutMutatingLiveWorld()
        {
            VoxelWorldModule world = new(91); VoxelCoord target = FindEditable(world, 0, 0); VoxelMaterialId material = world.GetMaterial(target);
            Assert.True(world.Execute(new VoxelEditCommand { ActorId = "player", Tick = 10, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = target, ExpectedBefore = material, After = VoxelMaterialId.Air }).Accepted);
            VoxelWorldSnapshot snapshot = world.CaptureSnapshot(); VoxelWorldModule restored = VoxelWorldModule.Restore(snapshot);
            Assert.Equal(world.RootHash, restored.RootHash); Assert.Equal(world.WorldRevision, restored.WorldRevision); Assert.Equal(VoxelMaterialId.Air, restored.GetMaterial(target));
            Assert.Equal(world.EventSequence, restored.EventSequence); Assert.Equal(world.Events, restored.Events);
            Assert.All(snapshot.Chunks.SelectMany(chunk => chunk.PayloadSegments), segment => Assert.InRange(segment.Length, 1, 1024));
            string root = world.RootHash; snapshot.EventSequence = 0;
            Assert.Throws<InvalidOperationException>(() => VoxelWorldModule.Restore(snapshot)); Assert.Equal(root, world.RootHash);
            snapshot = world.CaptureSnapshot(); snapshot.Events[0] = snapshot.Events[0] with { Before = VoxelMaterialId.Air };
            Assert.Throws<InvalidOperationException>(() => VoxelWorldModule.Restore(snapshot)); Assert.Equal(root, world.RootHash);
            snapshot = world.CaptureSnapshot(); snapshot.Chunks[0].PayloadSegments[0] = "AQI=";
            Assert.Throws<InvalidOperationException>(() => VoxelWorldModule.Restore(snapshot)); Assert.Equal(root, world.RootHash);
        }

        [Fact]
        public void EditContract_RejectsGapFormingAndFloatingEditsButAllowsSurfaceRoundTrip()
        {
            VoxelWorldModule world = new(73);
            VoxelCoord top = FindEditable(world, 0, 0);
            VoxelCoord interior = top with { Y = top.Y - 1 };
            Assert.NotEqual(VoxelMaterialId.Air, world.GetMaterial(interior));

            VoxelEditResult interiorRemove = world.Execute(new VoxelEditCommand
            {
                ActorId = "player",
                Tick = 0,
                ExpectedWorldRevision = 0,
                Kind = VoxelEditKind.Remove,
                Coord = interior,
                ExpectedBefore = world.GetMaterial(interior),
                After = VoxelMaterialId.Air
            });
            Assert.False(interiorRemove.Accepted);
            Assert.Equal(VoxelEditRejection.NonSurfaceEdit, interiorRemove.Rejection);

            VoxelCoord floating = top with { Y = top.Y + 2 };
            VoxelEditResult floatingPlace = world.Execute(new VoxelEditCommand
            {
                ActorId = "player",
                Tick = 0,
                ExpectedWorldRevision = 0,
                Kind = VoxelEditKind.Place,
                Coord = floating,
                ExpectedBefore = VoxelMaterialId.Air,
                After = VoxelMaterialId.Wood
            });
            Assert.False(floatingPlace.Accepted);
            Assert.Equal(VoxelEditRejection.UnsupportedPlacement, floatingPlace.Rejection);

            VoxelMaterialId original = world.GetMaterial(top);
            Assert.True(world.Execute(new VoxelEditCommand
            {
                ActorId = "player",
                Tick = 0,
                ExpectedWorldRevision = 0,
                Kind = VoxelEditKind.Remove,
                Coord = top,
                ExpectedBefore = original,
                After = VoxelMaterialId.Air
            }).Accepted);
            Assert.True(world.Execute(new VoxelEditCommand
            {
                ActorId = "player",
                Tick = 0,
                ExpectedWorldRevision = 1,
                Kind = VoxelEditKind.Place,
                Coord = top,
                ExpectedBefore = VoxelMaterialId.Air,
                After = original
            }).Accepted);
            Assert.Equal(original, world.GetMaterial(top));
        }

        [Fact]
        public void LegacyV1Events_RestoreGapAndOverhangAsExactOccupiedRuns()
        {
            VoxelWorldModule legacy = CreateLegacyV1World(260827);
            VoxelCoord top = FindEditable(legacy, 0, 0);
            VoxelCoord interior = top with { Y = top.Y - 1 };
            VoxelCoord floating = top with { Y = top.Y + 2 };
            Assert.True(ExecuteLegacyFixture(legacy, new VoxelEditCommand
            {
                ActorId = "legacy-player", Tick = 0, ExpectedWorldRevision = 0,
                Kind = VoxelEditKind.Remove, Coord = interior,
                ExpectedBefore = legacy.GetMaterial(interior), After = VoxelMaterialId.Air
            }).Accepted);
            Assert.True(ExecuteLegacyFixture(legacy, new VoxelEditCommand
            {
                ActorId = "legacy-player", Tick = 1, ExpectedWorldRevision = 1,
                Kind = VoxelEditKind.Place, Coord = floating,
                ExpectedBefore = VoxelMaterialId.Air, After = VoxelMaterialId.Wood
            }).Accepted);

            VoxelWorldModule restored = VoxelWorldModule.Restore(legacy.CaptureSnapshot());
            VoxelChunkGeometryProjection chunk = Assert.Single(restored.CaptureProjection(new[] { new VoxelChunkCoord(0, 0, 0) }).Chunks);
            VoxelVerticalRunProjection[] columnRuns = chunk.OccupiedRuns.Where(run => run.X == 0 && run.Z == 0).ToArray();
            Assert.Contains(columnRuns, run => run.MinYInclusive <= interior.Y - 1 && run.MaxYExclusive == interior.Y);
            Assert.Contains(columnRuns, run => run.MinYInclusive == interior.Y + 1 && run.MaxYExclusive == top.Y + 1);
            Assert.Contains(columnRuns, run => run.MinYInclusive == floating.Y && run.MaxYExclusive == floating.Y + 1);

            VoxelWorldSnapshot resaved = restored.CaptureSnapshot();
            Assert.Equal(VoxelWorldModule.LegacyGeneratorIdentity, resaved.Generator);
            VoxelWorldModule reloaded = VoxelWorldModule.Restore(resaved);
            Assert.Equal(restored.RootHash, reloaded.RootHash);
            Assert.Equal(VoxelMaterialId.Air, reloaded.GetMaterial(interior));
            Assert.Equal(VoxelMaterialId.Wood, reloaded.GetMaterial(floating));

            VoxelWorldModule.ReplayCursor cursor = restored.CreateReplayCursor(1);
            Assert.Equal(VoxelWorldModule.LegacyGeneratorIdentity, cursor.World.CaptureSnapshot().Generator);
            Assert.Equal(VoxelMaterialId.Air, cursor.World.GetMaterial(interior));
            Assert.Equal(VoxelMaterialId.Air, cursor.World.GetMaterial(floating));
            cursor.AdvanceTo(2);
            Assert.Equal(1, cursor.WorldGenerationCount);
            Assert.Equal(2, cursor.AppliedEventCount);
            Assert.Equal(restored.RootHash, cursor.World.RootHash);
        }

        [Fact]
        public void LegacyV1SafeSpawn_UsesDeterministicMinimumReliefFallback()
        {
            VoxelWorldModule legacy = CreateLegacyV1World(260827);
            VoxelSafeSpawn actual = legacy.FindSafePlayerSpawn();
            Assert.Equal(VoxelSafeSpawnContract.MinimumReliefFallback, actual.Contract);

            var candidates = Enumerable.Range(VoxelWorldModule.MinX + 2, VoxelWorldModule.MaxXExclusive - VoxelWorldModule.MinX - 4)
                .SelectMany(x => Enumerable.Range(VoxelWorldModule.MinZ + 2, VoxelWorldModule.MaxZExclusive - VoxelWorldModule.MinZ - 4)
                    .Select(z => EvaluateFallbackCandidate(legacy, x, z)))
                .Where(candidate => candidate.MaximumNeighborRise <= 1 && candidate.CameraClearanceCells >= 2)
                .OrderBy(candidate => candidate.Relief)
                .ThenBy(candidate => (candidate.X * candidate.X) + (candidate.Z * candidate.Z))
                .ThenBy(candidate => candidate.Z)
                .ThenBy(candidate => candidate.X)
                .ToArray();
            Assert.NotEmpty(candidates);
            var expected = candidates[0];
            Assert.Equal((expected.X, expected.Z, expected.SurfaceY), (actual.X, actual.Z, actual.SurfaceY));
            Assert.Equal(expected.Relief, actual.NeighborhoodRelief);
            Assert.Equal(expected.MaximumNeighborRise, actual.MaximumNeighborRise);
            Assert.True(actual.CameraClearanceCells >= 2);
            Assert.Equal(VoxelMaterialId.Air, legacy.GetMaterial(new VoxelCoord(actual.X, actual.SurfaceY, actual.Z)));
            Assert.Equal(VoxelMaterialId.Air, legacy.GetMaterial(new VoxelCoord(actual.X, actual.SurfaceY + 1, actual.Z)));

            VoxelWorldModule currentWorld = new(260827);
            VoxelSafeSpawn current = currentWorld.FindSafePlayerSpawn();
            Assert.Equal(VoxelSafeSpawnContract.StrictFiveByFiveClearing, current.Contract);
            Assert.Equal(0, current.NeighborhoodRelief);
            for (int x = -VoxelWorldModule.SpawnClearingRadius; x <= VoxelWorldModule.SpawnClearingRadius; x++)
                for (int z = -VoxelWorldModule.SpawnClearingRadius; z <= VoxelWorldModule.SpawnClearingRadius; z++)
                    Assert.Equal(current.SurfaceY, FindSurfaceY(currentWorld, x, z));
        }

        [Fact]
        public void Projection_HasIndexedVisibleFacesAndCrossChunkWalkability()
        {
            VoxelWorldModule world = new(18); VoxelWorldProjection projection = world.CaptureProjection(new[] { new VoxelChunkCoord(-1, 0, -1), new VoxelChunkCoord(0, 0, 0) });
            Assert.Equal(2, projection.Chunks.Count); Assert.All(projection.Chunks, chunk => { Assert.True(chunk.Indices.Count > 0); Assert.Equal(0, chunk.Indices.Count % 6); Assert.All(chunk.Indices, index => Assert.InRange(index, 0, chunk.Vertices.Count - 1)); Assert.DoesNotContain(chunk.Vertices, vertex => (byte)vertex.Material > 4); });
            Assert.Contains(projection.Walkable, span => span.X == -1 || span.X == 0);
            Assert.All(projection.Walkable, span => Assert.True(span.X >= -16 && span.X < 16 && span.Z >= -16 && span.Z < 16));
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            VoxelWorldProjection invalidScope = world.CaptureProjection(
                Enumerable.Range(0, 100_000).Select(index => new VoxelChunkCoord(1_000 + index, 99, -1_000 - index)));
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            Assert.Empty(invalidScope.Chunks); Assert.Empty(invalidScope.Walkable);
            Assert.InRange(allocatedBytes, 0, 1_000_000);
        }

        [Fact]
        public void FaceWinding_PointsOutwardForEveryAxisDirection()
        {
            foreach ((int dx, int dy, int dz) direction in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                List<VoxelVertex> vertices = new(); List<int> indices = new();
                VoxelWorldModule.AddFace(vertices, indices, new VoxelCoord(2, 3, 4), VoxelMaterialId.Soil, direction.dx, direction.dy, direction.dz);
                VoxelVertex a = vertices[indices[0]], b = vertices[indices[1]], c = vertices[indices[2]];
                (float X, float Y, float Z) ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
                (float X, float Y, float Z) ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
                (float X, float Y, float Z) normal = (ab.Y * ac.Z - ab.Z * ac.Y, ab.Z * ac.X - ab.X * ac.Z, ab.X * ac.Y - ab.Y * ac.X);
                float dot = normal.X * direction.dx + normal.Y * direction.dy + normal.Z * direction.dz;
                Assert.True(dot < 0.0f, $"Face {direction} is not clockwise when viewed from outside.");
            }
        }

        [Fact]
        public void ContinuousAndSnapshotResume_ProduceTheSameRoot()
        {
            VoxelWorldModule continuous = new(501); VoxelCoord first = FindEditable(continuous, -1, -1); VoxelMaterialId firstMaterial = continuous.GetMaterial(first);
            string identity = continuous.WorldIdentity; string initialRoot = continuous.RootHash;
            Assert.True(continuous.Execute(new VoxelEditCommand { ActorId = "player", Tick = 1, ExpectedWorldRevision = 0, Kind = VoxelEditKind.Remove, Coord = first, ExpectedBefore = firstMaterial, After = VoxelMaterialId.Air }).Accepted);
            Assert.Equal(identity, continuous.WorldIdentity); Assert.NotEqual(initialRoot, continuous.RootHash);
            VoxelWorldModule resumed = VoxelWorldModule.Restore(continuous.CaptureSnapshot()); VoxelCoord second = FindEditable(continuous, 1, 1); VoxelMaterialId secondMaterial = continuous.GetMaterial(second);
            VoxelEditCommand next = new() { ActorId = "player", Tick = 2, ExpectedWorldRevision = 1, Kind = VoxelEditKind.Remove, Coord = second, ExpectedBefore = secondMaterial, After = VoxelMaterialId.Air };
            Assert.True(continuous.Execute(next).Accepted); Assert.True(resumed.Execute(next).Accepted);
            Assert.Equal(continuous.RootHash, resumed.RootHash); Assert.Equal(continuous.WorldRevision, resumed.WorldRevision);
        }

        private static VoxelCoord FindEditable(VoxelWorldModule world, int x, int z)
        {
            for (int y = VoxelWorldModule.MaxYExclusive - 1; y >= 1; y--) if (world.GetMaterial(new(x, y, z)) is VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood) return new(x, y, z);
            throw new InvalidOperationException("Test seed unexpectedly has no editable material.");
        }

        private static VoxelCoord FindAir(VoxelWorldModule world, int x, int z)
        {
            for (int y = VoxelWorldModule.MaxYExclusive - 1; y >= 1; y--) if (world.GetMaterial(new(x, y, z)) == VoxelMaterialId.Air) return new(x, y, z);
            throw new InvalidOperationException("Test seed unexpectedly has no air cell.");
        }

        private static VoxelWorldModule CreateLegacyV1World(int seed)
        {
            ConstructorInfo constructor = typeof(VoxelWorldModule).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(string) }, null)
                ?? throw new InvalidOperationException("Legacy fixture constructor is unavailable.");
            return (VoxelWorldModule)constructor.Invoke(new object[] { seed, VoxelWorldModule.LegacyGeneratorIdentity });
        }

        private static VoxelEditResult ExecuteLegacyFixture(VoxelWorldModule world, VoxelEditCommand command)
        {
            MethodInfo method = typeof(VoxelWorldModule).GetMethod("ExecuteValidated", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Legacy fixture edit seam is unavailable.");
            return (VoxelEditResult)method.Invoke(world, new object[] { command, true })!;
        }

        private static (int X, int Z, int SurfaceY, int Relief, int MaximumNeighborRise, int CameraClearanceCells) EvaluateFallbackCandidate(VoxelWorldModule world, int x, int z)
        {
            int surface = FindSurfaceY(world, x, z);
            int[] neighborhood = Enumerable.Range(-2, 5).SelectMany(dx => Enumerable.Range(-2, 5).Select(dz => FindSurfaceY(world, x + dx, z + dz))).ToArray();
            int clearance = 0;
            for (int y = surface; y < VoxelWorldModule.MaxYExclusive && world.GetMaterial(new VoxelCoord(x, y, z)) == VoxelMaterialId.Air; y++) clearance++;
            return (x, z, surface, neighborhood.Max() - neighborhood.Min(), neighborhood.Max() - surface, clearance);
        }

        private static int FindSurfaceY(VoxelWorldModule world, int x, int z)
        {
            for (int y = VoxelWorldModule.MaxYExclusive - 1; y >= VoxelWorldModule.MinY; y--)
                if (world.GetMaterial(new VoxelCoord(x, y, z)) != VoxelMaterialId.Air) return y + 1;
            return VoxelWorldModule.MinY;
        }
    }
}
