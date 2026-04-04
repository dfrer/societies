using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeVoxelSpikeTests
    {
        [Fact]
        public void EditedChunkHashes_AreDeterministicForSameSeedAndEdits()
        {
            List<VoxelEdit> edits = new()
            {
                new VoxelEdit { WorldX = 2, WorldY = 10, WorldZ = 3, Material = VoxelMaterialId.Air },
                new VoxelEdit { WorldX = 18, WorldY = 9, WorldZ = 21, Material = VoxelMaterialId.Wood },
                new VoxelEdit { WorldX = 31, WorldY = 8, WorldZ = 7, Material = VoxelMaterialId.Stone }
            };

            VoxelWorldStore first = new(1337);
            VoxelWorldStore second = new(1337);
            foreach (VoxelEdit edit in edits)
            {
                first.ApplyEdit(edit);
                second.ApplyEdit(edit);
            }

            Assert.Equal(first.CaptureEditedChunkHashes(), second.CaptureEditedChunkHashes());
        }

        [Fact]
        public void SnapshotRestore_PreservesEditedChunkHashes()
        {
            VoxelWorldStore store = new(2048);
            store.ApplyEdit(new VoxelEdit { WorldX = 4, WorldY = 9, WorldZ = 4, Material = VoxelMaterialId.Air });
            store.ApplyEdit(new VoxelEdit { WorldX = 17, WorldY = 9, WorldZ = 17, Material = VoxelMaterialId.Wood });

            VoxelWorldSnapshot snapshot = store.CaptureSnapshot();
            VoxelWorldStore restored = VoxelWorldStore.Restore(snapshot);

            Assert.Equal(store.CaptureEditedChunkHashes(), restored.CaptureEditedChunkHashes());
        }

        [Fact]
        public void DirtyRebuildSet_IncludesTouchedChunkAndDirectNeighborsOnly()
        {
            VoxelWorldStore store = new(9001);
            VoxelChunkCoord touched = new(2, 0, 3);

            IReadOnlyList<VoxelChunkCoord> dirty = store.GetDirtyRebuildSet(touched);

            Assert.Equal(7, dirty.Count);
            Assert.Contains(touched, dirty);
            Assert.DoesNotContain(new VoxelChunkCoord(4, 0, 3), dirty);
        }

        [Fact]
        public void WalkabilityMask_IsDeterministicAfterRestore()
        {
            VoxelWorldStore store = new(777);
            store.ApplyEdit(new VoxelEdit { WorldX = 6, WorldY = 12, WorldZ = 6, Material = VoxelMaterialId.Air });
            store.ApplyEdit(new VoxelEdit { WorldX = 6, WorldY = 13, WorldZ = 6, Material = VoxelMaterialId.Air });

            VoxelWalkabilityMask first = store.BuildWalkabilityMask();
            VoxelWorldStore restored = VoxelWorldStore.Restore(store.CaptureSnapshot());
            VoxelWalkabilityMask second = restored.BuildWalkabilityMask();

            Assert.Equal(first.Hash, second.Hash);
        }
    }
}
