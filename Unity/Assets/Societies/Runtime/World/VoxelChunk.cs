using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Single chunk of voxel data (16x16x256 blocks)
    /// </summary>
    public sealed class VoxelChunk : IDisposable
    {
        public const int WIDTH = 16;
        public const int HEIGHT = 256;
        public const int VOLUME = WIDTH * WIDTH * HEIGHT; // 65,536 blocks

        public ChunkCoord Coord { get; }
        
        // Raw block data - using NativeArray for Burst compatibility
        private NativeArray<BlockData> _blocks;
        
        public bool IsDirty { get; set; }
        public ulong LastModifiedTick { get; set; }
        public bool IsGenerated { get; set; }
        public bool IsLoaded { get; set; }

        // Mesh state
        public int MeshVersion { get; set; }
        public bool NeedsMeshRebuild { get; set; }

        public VoxelChunk(ChunkCoord coord)
        {
            Coord = coord;
            _blocks = new NativeArray<BlockData>(VOLUME, Allocator.Persistent);
            IsDirty = false;
            IsGenerated = false;
            IsLoaded = false;
            MeshVersion = 0;
            NeedsMeshRebuild = true;
        }

        /// <summary>
        /// Get block at local coordinates (0-15, 0-255, 0-15)
        /// </summary>
        public BlockData GetBlock(int localX, int y, int localZ)
        {
            int index = GetIndex(localX, y, localZ);
            return _blocks[index];
        }

        /// <summary>
        /// Set block at local coordinates
        /// </summary>
        public void SetBlock(int localX, int y, int localZ, BlockData block)
        {
            int index = GetIndex(localX, y, localZ);
            if (_blocks[index].Id != block.Id)
            {
                _blocks[index] = block;
                IsDirty = true;
                NeedsMeshRebuild = true;
                LastModifiedTick = Simulation.SimulationClock.Instance?.CurrentTick ?? 0;
            }
        }

        /// <summary>
        /// Get block at local coordinates (returns air if out of bounds)
        /// </summary>
        public BlockData GetBlockSafe(int localX, int y, int localZ)
        {
            if (localX < 0 || localX >= WIDTH || y < 0 || y >= HEIGHT || localZ < 0 || localZ >= WIDTH)
                return BlockData.Air;
            return GetBlock(localX, y, localZ);
        }

        /// <summary>
        /// Get raw block array (for meshing)
        /// </summary>
        public NativeArray<BlockData> GetBlocks() => _blocks;

        /// <summary>
        /// Convert 3D coordinates to 1D index
        /// </summary>
        public static int GetIndex(int x, int y, int z)
        {
            // X then Z then Y for cache-friendly iteration patterns
            return (y * WIDTH + z) * WIDTH + x;
            // Equivalent: y * 256 + z * 16 + x
        }

        /// <summary>
        /// Convert 1D index to 3D coordinates
        /// </summary>
        public static (int x, int y, int z) GetCoords(int index)
        {
            int y = index / (WIDTH * WIDTH);
            int remainder = index % (WIDTH * WIDTH);
            int z = remainder / WIDTH;
            int x = remainder % WIDTH;
            return (x, y, z);
        }

        public void Dispose()
        {
            if (_blocks.IsCreated)
                _blocks.Dispose();
        }

        /// <summary>
        /// Clear all blocks to air
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < VOLUME; i++)
            {
                _blocks[i] = BlockData.Air;
            }
            IsDirty = true;
            NeedsMeshRebuild = true;
        }

        /// <summary>
        /// Check if chunk is empty (all air)
        /// </summary>
        public bool IsEmpty()
        {
            for (int i = 0; i < VOLUME; i++)
            {
                if (!_blocks[i].IsAir) return false;
            }
            return true;
        }
    }
}