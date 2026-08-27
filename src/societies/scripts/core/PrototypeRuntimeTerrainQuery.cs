using Godot;
using System;

namespace Societies.Core
{
    /// <summary>
    /// Read-only terrain facts shared by runtime persistence and player placement.
    /// The wrapped heightfield or voxel module remains the single world authority.
    /// </summary>
    internal interface IPrototypeRuntimeTerrainQuery
    {
        string WorldModel { get; }

        int WorldSeed { get; }

        int WorldGenerationAttempt { get; }

        string WorldHash { get; }

        Vector3 SettlementAnchorPosition { get; }

        Vector3 ProjectToSurface(Vector3 horizontalPosition);
    }

    internal sealed class HeightfieldRuntimeTerrainQuery : IPrototypeRuntimeTerrainQuery
    {
        private readonly WorldGenerationResult _world;

        public HeightfieldRuntimeTerrainQuery(WorldGenerationResult world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public string WorldModel => PrototypeWorldModels.Heightfield;

        public int WorldSeed => _world.WorldSeed;

        public int WorldGenerationAttempt => _world.WorldGenerationAttempt;

        public string WorldHash => _world.WorldHash;

        public Vector3 SettlementAnchorPosition => _world.SettlementSpawn.AnchorPosition;

        public Vector3 ProjectToSurface(Vector3 horizontalPosition) =>
            _world.WorldMap.ProjectToSurface(horizontalPosition);
    }

    internal sealed class VoxelRuntimeTerrainQuery : IPrototypeRuntimeTerrainQuery
    {
        private readonly VoxelWorldModule _world;

        public VoxelRuntimeTerrainQuery(VoxelWorldModule world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public string WorldModel => PrototypeWorldModels.Voxel;

        public int WorldSeed => _world.Seed;

        public int WorldGenerationAttempt => 0;

        public string WorldHash => _world.WorldIdentity;

        public Vector3 SettlementAnchorPosition => ProjectToSurface(Vector3.Zero);

        public Vector3 ProjectToSurface(Vector3 horizontalPosition)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(horizontalPosition.X), VoxelWorldModule.MinX, VoxelWorldModule.MaxXExclusive - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(horizontalPosition.Z), VoxelWorldModule.MinZ, VoxelWorldModule.MaxZExclusive - 1);
            for (int y = VoxelWorldModule.MaxYExclusive - 1; y >= VoxelWorldModule.MinY; y--)
            {
                if (_world.GetMaterial(new VoxelCoord(x, y, z)) != VoxelMaterialId.Air)
                {
                    return new Vector3(horizontalPosition.X, y + 1.0f, horizontalPosition.Z);
                }
            }

            return new Vector3(horizontalPosition.X, VoxelWorldModule.MinY, horizontalPosition.Z);
        }
    }

    public static class PrototypeWorldModels
    {
        public const string Heightfield = "heightfield_v1";

        public const string Voxel = "voxel_v1";
    }
}
