using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace Societies.Core
{
    public enum VoxelMaterialId : byte
    {
        Air = 0,
        Soil = 1,
        Stone = 2,
        Wood = 3,
        WaterBlocked = 4
    }

    public readonly record struct VoxelChunkCoord(int X, int Y, int Z)
    {
        public override string ToString() => $"{X}:{Y}:{Z}";
    }

    public sealed class VoxelEdit
    {
        public int WorldX { get; set; }

        public int WorldY { get; set; }

        public int WorldZ { get; set; }

        public VoxelMaterialId Material { get; set; }
    }

    public sealed class VoxelChunk
    {
        private readonly byte[] _voxels;

        public VoxelChunk(VoxelChunkCoord coord, int width, int depth, int height)
        {
            Coord = coord;
            Width = width;
            Depth = depth;
            Height = height;
            _voxels = new byte[width * depth * height];
        }

        public VoxelChunkCoord Coord { get; }

        public int Width { get; }

        public int Depth { get; }

        public int Height { get; }

        public byte[] Voxels => _voxels;

        public VoxelMaterialId Get(int x, int y, int z)
        {
            return (VoxelMaterialId)_voxels[GetIndex(x, y, z)];
        }

        public void Set(int x, int y, int z, VoxelMaterialId material)
        {
            _voxels[GetIndex(x, y, z)] = (byte)material;
        }

        public string ComputeHash()
        {
            return Convert.ToHexString(SHA256.HashData(_voxels)).ToLowerInvariant();
        }

        private int GetIndex(int x, int y, int z)
        {
            return (y * Width * Depth) + (z * Width) + x;
        }
    }

    public sealed class VoxelChunkSnapshot
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        public byte[] Voxels { get; set; } = Array.Empty<byte>();
    }

    public sealed class VoxelWorldSnapshot
    {
        public int Seed { get; set; }

        public List<VoxelChunkSnapshot> Chunks { get; set; } = new();
    }

    public sealed class VoxelMesherResult
    {
        public int QuadCount { get; set; }

        public int CollidableFaceCount { get; set; }

        public IReadOnlyList<VoxelChunkCoord> RebuiltChunks { get; set; } = Array.Empty<VoxelChunkCoord>();
    }

    public sealed class VoxelWalkabilityMask
    {
        public int Width { get; set; }

        public int Depth { get; set; }

        public bool[] Walkable { get; set; } = Array.Empty<bool>();

        public string Hash => Convert.ToHexString(SHA256.HashData(Walkable.Select(value => value ? (byte)1 : (byte)0).ToArray())).ToLowerInvariant();
    }

    public sealed class VoxelSpikeReport
    {
        public int Seed { get; set; }

        public int EditedChunkCount { get; set; }

        public int GeneratedChunkCount { get; set; }

        public string SnapshotHash { get; set; } = string.Empty;

        public string WalkabilityHash { get; set; } = string.Empty;

        public double GenerationMilliseconds { get; set; }

        public double MeshingMilliseconds { get; set; }

        public double ReplayMilliseconds { get; set; }
    }

    public sealed class VoxelWorldStore
    {
        public const int ChunkWidth = 16;
        public const int ChunkDepth = 16;
        public const int ChunkHeight = 32;
        public const int VisibleFieldWidth = 8;
        public const int VisibleFieldDepth = 8;

        private readonly int _seed;
        private readonly Dictionary<VoxelChunkCoord, VoxelChunk> _chunks = new();
        private readonly HashSet<VoxelChunkCoord> _editedChunkCoords = new();

        public VoxelWorldStore(int seed)
        {
            _seed = seed;
        }

        public int Seed => _seed;

        public int GeneratedChunkCount => _chunks.Count;

        public VoxelChunk GetChunk(VoxelChunkCoord coord)
        {
            if (_chunks.TryGetValue(coord, out VoxelChunk? existing))
            {
                return existing;
            }

            VoxelChunk chunk = GenerateChunk(coord);
            _chunks[coord] = chunk;
            return chunk;
        }

        public void ApplyEdit(VoxelEdit edit)
        {
            VoxelChunkCoord coord = WorldToChunk(edit.WorldX, edit.WorldY, edit.WorldZ);
            (int localX, int localY, int localZ) = WorldToLocal(edit.WorldX, edit.WorldY, edit.WorldZ);
            VoxelChunk chunk = GetChunk(coord);
            chunk.Set(localX, localY, localZ, edit.Material);
            _editedChunkCoords.Add(coord);
        }

        public IReadOnlyList<VoxelChunkCoord> GetDirtyRebuildSet(VoxelChunkCoord touchedChunk)
        {
            HashSet<VoxelChunkCoord> dirty = new()
            {
                touchedChunk
            };

            int[] offsets = { -1, 1 };
            foreach (int offset in offsets)
            {
                dirty.Add(new VoxelChunkCoord(touchedChunk.X + offset, touchedChunk.Y, touchedChunk.Z));
                dirty.Add(new VoxelChunkCoord(touchedChunk.X, touchedChunk.Y + offset, touchedChunk.Z));
                dirty.Add(new VoxelChunkCoord(touchedChunk.X, touchedChunk.Y, touchedChunk.Z + offset));
            }

            return dirty.OrderBy(coord => coord.X).ThenBy(coord => coord.Y).ThenBy(coord => coord.Z).ToList();
        }

        public VoxelMesherResult BuildMesherResult(IEnumerable<VoxelChunkCoord> chunkCoords)
        {
            int quadCount = 0;
            int collidableFaces = 0;
            List<VoxelChunkCoord> rebuilt = new();

            foreach (VoxelChunkCoord coord in chunkCoords.Distinct())
            {
                VoxelChunk chunk = GetChunk(coord);
                rebuilt.Add(coord);

                for (int y = 0; y < ChunkHeight; y++)
                {
                    for (int z = 0; z < ChunkDepth; z++)
                    {
                        for (int x = 0; x < ChunkWidth; x++)
                        {
                            VoxelMaterialId material = chunk.Get(x, y, z);
                            if (material == VoxelMaterialId.Air)
                            {
                                continue;
                            }

                            foreach ((int offsetX, int offsetY, int offsetZ) in NeighborOffsets())
                            {
                                VoxelMaterialId neighbor = GetWorldMaterial(
                                    (coord.X * ChunkWidth) + x + offsetX,
                                    (coord.Y * ChunkHeight) + y + offsetY,
                                    (coord.Z * ChunkDepth) + z + offsetZ);
                                if (neighbor == VoxelMaterialId.Air)
                                {
                                    quadCount++;
                                    collidableFaces++;
                                }
                            }
                        }
                    }
                }
            }

            return new VoxelMesherResult
            {
                QuadCount = quadCount,
                CollidableFaceCount = collidableFaces,
                RebuiltChunks = rebuilt
            };
        }

        public VoxelWorldSnapshot CaptureSnapshot()
        {
            return new VoxelWorldSnapshot
            {
                Seed = _seed,
                Chunks = _editedChunkCoords
                    .OrderBy(coord => coord.X)
                    .ThenBy(coord => coord.Y)
                    .ThenBy(coord => coord.Z)
                    .Select(coord =>
                    {
                        VoxelChunk chunk = GetChunk(coord);
                        return new VoxelChunkSnapshot
                        {
                            X = coord.X,
                            Y = coord.Y,
                            Z = coord.Z,
                            Voxels = chunk.Voxels.ToArray()
                        };
                    })
                    .ToList()
            };
        }

        public static VoxelWorldStore Restore(VoxelWorldSnapshot snapshot)
        {
            VoxelWorldStore store = new(snapshot.Seed);
            foreach (VoxelChunkSnapshot chunkSnapshot in snapshot.Chunks)
            {
                VoxelChunkCoord coord = new(chunkSnapshot.X, chunkSnapshot.Y, chunkSnapshot.Z);
                VoxelChunk chunk = new(coord, ChunkWidth, ChunkDepth, ChunkHeight);
                chunkSnapshot.Voxels.CopyTo(chunk.Voxels, 0);
                store._chunks[coord] = chunk;
                store._editedChunkCoords.Add(coord);
            }

            return store;
        }

        public Dictionary<string, string> CaptureEditedChunkHashes()
        {
            return _editedChunkCoords
                .OrderBy(coord => coord.X)
                .ThenBy(coord => coord.Y)
                .ThenBy(coord => coord.Z)
                .ToDictionary(coord => coord.ToString(), coord => GetChunk(coord).ComputeHash(), StringComparer.Ordinal);
        }

        public VoxelWalkabilityMask BuildWalkabilityMask()
        {
            bool[] walkable = new bool[VisibleFieldWidth * ChunkWidth * VisibleFieldDepth * ChunkDepth];
            int width = VisibleFieldWidth * ChunkWidth;
            int depth = VisibleFieldDepth * ChunkDepth;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool canStand = false;
                    for (int y = ChunkHeight - 3; y >= 0; y--)
                    {
                        VoxelMaterialId floor = GetWorldMaterial(x, y, z);
                        if (floor == VoxelMaterialId.Air || floor == VoxelMaterialId.WaterBlocked)
                        {
                            continue;
                        }

                        VoxelMaterialId head = GetWorldMaterial(x, y + 1, z);
                        VoxelMaterialId aboveHead = GetWorldMaterial(x, y + 2, z);
                        canStand = head == VoxelMaterialId.Air && aboveHead == VoxelMaterialId.Air;
                        break;
                    }

                    walkable[(z * width) + x] = canStand;
                }
            }

            return new VoxelWalkabilityMask
            {
                Width = width,
                Depth = depth,
                Walkable = walkable
            };
        }

        public static VoxelSpikeReport RunSpike(int seed, IReadOnlyList<VoxelEdit> edits)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            VoxelWorldStore store = new(seed);
            for (int z = 0; z < VisibleFieldDepth; z++)
            {
                for (int x = 0; x < VisibleFieldWidth; x++)
                {
                    store.GetChunk(new VoxelChunkCoord(x, 0, z));
                }
            }

            stopwatch.Stop();
            double generationMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            foreach (VoxelEdit edit in edits)
            {
                store.ApplyEdit(edit);
            }

            VoxelMesherResult mesherResult = store.BuildMesherResult(store._editedChunkCoords.Count > 0
                ? store._editedChunkCoords.SelectMany(store.GetDirtyRebuildSet)
                : new[] { new VoxelChunkCoord(0, 0, 0) });
            stopwatch.Stop();
            double meshingMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            VoxelWorldSnapshot snapshot = store.CaptureSnapshot();
            VoxelWorldStore restored = Restore(snapshot);
            Dictionary<string, string> snapshotHashes = restored.CaptureEditedChunkHashes();
            VoxelWalkabilityMask walkability = restored.BuildWalkabilityMask();
            stopwatch.Stop();

            string snapshotHash = Convert.ToHexString(SHA256.HashData(
                string.Join('|', snapshotHashes.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))
                    .Select(ch => (byte)ch)
                    .ToArray())).ToLowerInvariant();

            return new VoxelSpikeReport
            {
                Seed = seed,
                EditedChunkCount = snapshot.Chunks.Count,
                GeneratedChunkCount = store.GeneratedChunkCount,
                SnapshotHash = snapshotHash,
                WalkabilityHash = walkability.Hash,
                GenerationMilliseconds = generationMs,
                MeshingMilliseconds = meshingMs + mesherResult.QuadCount * 0.0,
                ReplayMilliseconds = stopwatch.Elapsed.TotalMilliseconds
            };
        }

        private VoxelChunk GenerateChunk(VoxelChunkCoord coord)
        {
            VoxelChunk chunk = new(coord, ChunkWidth, ChunkDepth, ChunkHeight);
            int seed = PrototypeSeedDerivation.Derive(_seed, "voxel.chunk", HashCoord(coord));

            for (int z = 0; z < ChunkDepth; z++)
            {
                for (int x = 0; x < ChunkWidth; x++)
                {
                    int worldX = (coord.X * ChunkWidth) + x;
                    int worldZ = (coord.Z * ChunkDepth) + z;
                    int columnHeight = 9 + (int)MathF.Round(
                        6.0f * MathF.Sin((worldX + seed) * 0.07f) +
                        5.0f * MathF.Cos((worldZ - seed) * 0.05f));
                    int waterHeight = 8;

                    for (int y = 0; y < ChunkHeight; y++)
                    {
                        int worldY = (coord.Y * ChunkHeight) + y;
                        VoxelMaterialId material = VoxelMaterialId.Air;
                        if (worldY <= columnHeight - 4)
                        {
                            material = VoxelMaterialId.Stone;
                        }
                        else if (worldY <= columnHeight)
                        {
                            material = VoxelMaterialId.Soil;
                        }
                        else if (worldY <= waterHeight)
                        {
                            material = VoxelMaterialId.WaterBlocked;
                        }

                        if ((worldX + worldZ + seed) % 29 == 0 && worldY > columnHeight && worldY <= columnHeight + 2)
                        {
                            material = VoxelMaterialId.Wood;
                        }

                        chunk.Set(x, y, z, material);
                    }
                }
            }

            return chunk;
        }

        private VoxelMaterialId GetWorldMaterial(int worldX, int worldY, int worldZ)
        {
            if (worldY < 0)
            {
                return VoxelMaterialId.Stone;
            }

            VoxelChunkCoord coord = WorldToChunk(worldX, worldY, worldZ);
            (int localX, int localY, int localZ) = WorldToLocal(worldX, worldY, worldZ);
            if (localY < 0 || localY >= ChunkHeight)
            {
                return VoxelMaterialId.Air;
            }

            return GetChunk(coord).Get(localX, localY, localZ);
        }

        private static IEnumerable<(int offsetX, int offsetY, int offsetZ)> NeighborOffsets()
        {
            yield return (1, 0, 0);
            yield return (-1, 0, 0);
            yield return (0, 1, 0);
            yield return (0, -1, 0);
            yield return (0, 0, 1);
            yield return (0, 0, -1);
        }

        private static VoxelChunkCoord WorldToChunk(int worldX, int worldY, int worldZ)
        {
            return new VoxelChunkCoord(
                FloorDiv(worldX, ChunkWidth),
                FloorDiv(worldY, ChunkHeight),
                FloorDiv(worldZ, ChunkDepth));
        }

        private static (int localX, int localY, int localZ) WorldToLocal(int worldX, int worldY, int worldZ)
        {
            int localX = Mod(worldX, ChunkWidth);
            int localY = Mod(worldY, ChunkHeight);
            int localZ = Mod(worldZ, ChunkDepth);
            return (localX, localY, localZ);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && ((remainder < 0) ^ (divisor < 0)))
            {
                quotient--;
            }

            return quotient;
        }

        private static int Mod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static int HashCoord(VoxelChunkCoord coord)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + coord.X;
                hash = (hash * 31) + coord.Y;
                hash = (hash * 31) + coord.Z;
                return hash;
            }
        }
    }
}
