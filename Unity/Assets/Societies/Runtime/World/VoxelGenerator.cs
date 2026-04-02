using System;
using UnityEngine;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Voxel terrain generator using layered noise
    /// </summary>
    public sealed class VoxelGenerator
    {
        // Simple noise implementation for terrain generation
        private SimplexNoise _heightNoise;
        private SimplexNoise _detailNoise;
        private SimplexNoise _temperatureNoise;
        private SimplexNoise _humidityNoise;

        public VoxelGenerator()
        {
            // Seed will be set when generating
        }

        public void GenerateChunk(VoxelChunk chunk, int seed)
        {
            // Initialize noise generators with offset for variety
            _heightNoise = new SimplexNoise(seed);
            _detailNoise = new SimplexNoise(seed + 1000);
            _temperatureNoise = new SimplexNoise(seed + 2000);
            _humidityNoise = new SimplexNoise(seed + 3000);

            int chunkX = chunk.Coord.X * ChunkCoord.SIZE;
            int chunkZ = chunk.Coord.Z * ChunkCoord.SIZE;

            for (int localX = 0; localX < ChunkCoord.SIZE; localX++)
            {
                for (int localZ = 0; localZ < ChunkCoord.SIZE; localZ++)
                {
                    int worldX = chunkX + localX;
                    int worldZ = chunkZ + localZ;

                    // Generate height for this column
                    int surfaceHeight = GetSurfaceHeight(worldX, worldZ);
                    
                    // Determine biome
                    BiomeType biome = GetBiome(worldX, worldZ);

                    // Fill column
                    for (int y = 0; y < VoxelWorld.CHUNK_HEIGHT; y++)
                    {
                        BlockData block = GetBlockForDepth(y, surfaceHeight, biome);
                        
                        if (!block.IsAir)
                        {
                            chunk.SetBlock(localX, y, localZ, block);
                        }
                    }

                    // Place surface block
                    if (surfaceHeight >= 0 && surfaceHeight < VoxelWorld.CHUNK_HEIGHT)
                    {
                        BlockData surfaceBlock = GetSurfaceBlock(biome);
                        chunk.SetBlock(localX, surfaceHeight, localZ, surfaceBlock);
                        
                        // Add vegetation
                        if (surfaceBlock.Id == (ushort)BlockType.Grass)
                        {
                            TryPlaceVegetation(chunk, localX, surfaceHeight + 1, localZ, biome);
                        }
                    }
                }
            }

            chunk.IsGenerated = true;
        }

        private int GetSurfaceHeight(int x, int z)
        {
            // Base height from noise
            float height = _heightNoise.noise(x * 0.01f, z * 0.01f) * 20f;
            height += _detailNoise.noise(x * 0.05f, z * 0.05f) * 5f;
            
            // Add some variation
            height = height * 0.5f + 30f; // Base ground level
            
            return Mathf.Clamp(Mathf.FloorToInt(height), 0, VoxelWorld.CHUNK_HEIGHT - 1);
        }

        private BiomeType GetBiome(int x, int z)
        {
            float temp = _temperatureNoise.noise(x * 0.008f, z * 0.008f);
            float humidity = _humidityNoise.noise(x * 0.008f, z * 0.008f);

            // Simple biome determination
            if (temp < -0.3f) return BiomeType.Snow;
            if (temp < 0.3f)
            {
                if (humidity > 0.2f) return BiomeType.BorealForest;
                return BiomeType.Tundra;
            }
            else
            {
                if (humidity > 0.4f) return BiomeType.Jungle;
                if (humidity > 0.0f) return BiomeType.BorealForest;
                return BiomeType.Desert;
            }
        }

        private BlockData GetBlockForDepth(int y, int surfaceHeight, BiomeType biome)
        {
            if (y > surfaceHeight)
                return BlockData.Air;

            int depth = surfaceHeight - y;

            if (depth < 3)
            {
                // Top soil layers
                return BlockData.FromType(BlockType.Dirt);
            }
            else if (depth < 10)
            {
                // Transition layer
                return BlockData.FromType(BlockType.Dirt);
            }
            else if (depth < 30)
            {
                // Stone with occasional ores
                if (depth == 15 && UnityEngine.Random.value < 0.05f)
                    return BlockData.FromType(BlockType.Coal);
                if (depth == 25 && UnityEngine.Random.value < 0.03f)
                    return BlockData.FromType(BlockType.CopperOre);
                if (depth == 28 && UnityEngine.Random.value < 0.02f)
                    return BlockData.FromType(BlockType.IronOre);
                    
                return BlockData.FromType(BlockType.Stone);
            }
            else
            {
                // Deep stone
                return BlockData.FromType(BlockType.Stone);
            }
        }

        private BlockData GetSurfaceBlock(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Desert => BlockData.FromType(BlockType.Sand),
                BiomeType.Snow => BlockData.FromType(BlockType.Snow),
                BiomeType.Jungle => BlockData.FromType(BlockType.Grass),
                BiomeType.BorealForest => BlockData.FromType(BlockType.Grass),
                _ => BlockData.FromType(BlockType.Grass)
            };
        }

        private void TryPlaceVegetation(VoxelChunk chunk, int x, int y, int z, BiomeType biome)
        {
            if (y >= VoxelWorld.CHUNK_HEIGHT - 1) return;

            // Simple tree generation - just a few blocks for now
            float treeNoise = _detailNoise.noise((chunk.Coord.X * ChunkCoord.SIZE + x) * 0.1f, 
                                                  (chunk.Coord.Z * ChunkCoord.SIZE + z) * 0.1f);
            
            if (treeNoise > 0.6f)
            {
                // Place trunk
                chunk.SetBlock(x, y, z, BlockData.FromType(BlockType.Wood));
                chunk.SetBlock(x, y + 1, z, BlockData.FromType(BlockType.Wood));
                
                // Place leaves
                for (int ly = 2; ly <= 3; ly++)
                {
                    for (int lx = -1; lx <= 1; lx++)
                    {
                        for (int lz = -1; lz <= 1; lz++)
                        {
                            if (lx == 0 && lz == 0 && ly == 2) continue;
                            chunk.SetBlock(x + lx, y + ly, z + lz, BlockData.FromType(BlockType.Leaves));
                        }
                    }
                }
            }
        }

        private enum BiomeType
        {
            Desert,
            BorealForest,
            Jungle,
            Tundra,
            Snow
        }
    }

    /// <summary>
    /// Simple simplex noise implementation
    /// </summary>
    public class SimplexNoise
    {
        private readonly int _seed;
        private readonly float _scale;

        public SimplexNoise(int seed, float scale = 1f)
        {
            _seed = seed;
            _scale = scale;
        }

        public float noise(float x, float y)
        {
            // Simple value noise for MVP - can be replaced with full simplex
            float result = 0f;
            float frequency = _scale;
            float amplitude = 1f;
            float maxValue = 0f;

            for (int i = 0; i < 4; i++)
            {
                result += smoothNoise(x * frequency + _seed, y * frequency + _seed) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return result / maxValue;
        }

        private float smoothNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            
            float xf = x - xi;
            float yf = y - yi;

            // Simple hash
            int n00 = hash(xi, yi);
            int n10 = hash(xi + 1, yi);
            int n01 = hash(xi, yi + 1);
            int n11 = hash(xi + 1, yi + 1);

            // Smooth interpolation
            float xs = fade(xf);
            float ys = fade(yf);

            float nx0 = lerp(hashToFloat(n00), hashToFloat(n10), xs);
            float nx1 = lerp(hashToFloat(n01), hashToFloat(n11), xs);

            return lerp(nx0, nx1, ys);
        }

        private float fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private float lerp(float a, float b, float t) => a + t * (b - a);
        private int hash(int x, int y) => (x * 374761393 + y * 668265263 + _seed) ^ (x * 1274126177);
        private float hashToFloat(int h) => (h & 0xFFFFFF) / (float)0xFFFFFF;
    }
}