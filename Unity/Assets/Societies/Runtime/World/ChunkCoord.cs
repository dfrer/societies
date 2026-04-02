using System;
using UnityEngine;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Chunk coordinates (2D XZ plane)
    /// </summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public const int SIZE = 16;
        
        public readonly int X;
        public readonly int Z;

        public ChunkCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>
        /// Create chunk coord from world block position
        /// </summary>
        public static ChunkCoord FromBlock(int blockX, int blockZ)
        {
            return new ChunkCoord(
                Mathf.FloorToInt((float)blockX / SIZE),
                Mathf.FloorToInt((float)blockZ / SIZE)
            );
        }

        /// <summary>
        /// Create chunk coord from world block position
        /// </summary>
        public static ChunkCoord FromBlock(BlockCoord block)
        {
            return FromBlock(block.X, block.Z);
        }

        /// <summary>
        /// Create chunk coord from world position
        /// </summary>
        public static ChunkCoord FromWorld(Vector3 worldPos)
        {
            return FromBlock(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
        }

        /// <summary>
        /// Get local block coordinates within chunk
        /// </summary>
        public static (int localX, int localZ) GetLocal(int blockX, int blockZ)
        {
            int cx = Mathf.FloorToInt((float)blockX / SIZE);
            int cz = Mathf.FloorToInt((float)blockZ / SIZE);
            int lx = ((blockX % SIZE) + SIZE) % SIZE;
            int lz = ((blockZ % SIZE) + SIZE) % SIZE;
            return (lx, lz);
        }

        /// <summary>
        /// Get local block coordinate within chunk
        /// </summary>
        public BlockCoord ToLocal(BlockCoord worldBlock)
        {
            int cx = Mathf.FloorToInt((float)worldBlock.X / SIZE);
            int cz = Mathf.FloorToInt((float)worldBlock.Z / SIZE);
            int lx = ((worldBlock.X % SIZE) + SIZE) % SIZE;
            int lz = ((worldBlock.Z % SIZE) + SIZE) % SIZE;
            return new BlockCoord(lx, worldBlock.Y, lz);
        }

        public Vector3 CenterWorldPosition()
        {
            return new Vector3(
                X * SIZE + SIZE / 2f,
                0,
                Z * SIZE + SIZE / 2f
            );
        }

        public int WorldMinX() => X * SIZE;
        public int WorldMinZ() => Z * SIZE;
        public int WorldMaxX() => X * SIZE + SIZE - 1;
        public int WorldMaxZ() => Z * SIZE + SIZE - 1;

        public bool IsAdjacentTo(ChunkCoord other)
        {
            int dx = Mathf.Abs(X - other.X);
            int dz = Mathf.Abs(Z - other.Z);
            return (dx <= 1 && dz <= 1) && (dx + dz > 0);
        }

        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);
        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;
        public override int GetHashCode() => HashCode.Combine(X, Z);
        
        public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);
        public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);
        
        public override string ToString() => $"Chunk({X}, {Z})";
    }
}