using UnityEngine;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Block types available in the MVP
    /// </summary>
    public enum BlockType : ushort
    {
        Air = 0,
        Dirt = 1,
        Grass = 2,
        Stone = 3,
        Coal = 4,
        CopperOre = 5,
        IronOre = 6,
        Wood = 7,
        Leaves = 8,
        Sand = 9,
        Clay = 10,
        Water = 11,
        Snow = 12,
        
        // Building blocks
        WoodPlank = 20,
        StoneBrick = 21,
        Brick = 22,
        
        // Ores (processed)
        CopperIngot = 30,
        IronIngot = 31,
        CoalBlock = 32,
        
        MAX
    }

    /// <summary>
    /// Metadata for special block states (orientation, growth, etc.)
    /// </summary>
    public struct BlockMetadata
    {
        public byte Orientation;      // 0-3 for rotation
        public byte GrowthStage;      // 0-7 for crops/plants
        public byte Moisture;         // 0-255 for farming
        public byte Flags;            // Misc flags
        
        public static readonly BlockMetadata Default = new()
        {
            Orientation = 0,
            GrowthStage = 0,
            Moisture = 128,
            Flags = 0
        };
    }

    /// <summary>
    /// Single block of voxel data
    /// </summary>
    public struct BlockData
    {
        public ushort Id;           // BlockType as ushort
        public BlockMetadata Metadata;
        public byte State;          // Additional state (light level, etc.)

        public bool IsAir => Id == 0;
        public bool IsSolid => Id != 0;

        public static BlockData Air => new() 
        { 
            Id = 0, 
            Metadata = BlockMetadata.Default, 
            State = 0 
        };

        public static BlockData FromType(BlockType type) => new()
        {
            Id = (ushort)type,
            Metadata = BlockMetadata.Default,
            State = 0
        };

        public override string ToString() => $"Block({(BlockType)Id}, meta:{Metadata.Orientation})";
    }

    /// <summary>
    /// World coordinates for a block position
    /// </summary>
    public readonly struct BlockCoord : System.IEquatable<BlockCoord>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public BlockCoord(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static BlockCoord Zero => new(0, 0, 0);
        
        public BlockCoord Offset(int dx, int dy, int dz) => new(X + dx, Y + dy, Z + dz);
        
        public float DistanceTo(BlockCoord other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            int dz = Z - other.Z;
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override bool Equals(object obj) => obj is BlockCoord other && Equals(other);
        public bool Equals(BlockCoord other) => X == other.X && Y == other.Y && Z == other.Z;
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        
        public static bool operator ==(BlockCoord left, BlockCoord right) => left.Equals(right);
        public static bool operator !=(BlockCoord left, BlockCoord right) => !left.Equals(right);
        
        public override string ToString() => $"({X}, {Y}, {Z})";
        
        public Vector3Int ToVector3Int() => new(X, Y, Z);
        public static BlockCoord FromVector3Int(Vector3Int v) => new(v.x, v.y, v.z);
    }
}