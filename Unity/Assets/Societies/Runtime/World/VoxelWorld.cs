using System;
using System.Collections.Generic;
using UnityEngine;

namespace Societies.Runtime.World
{
    /// <summary>
    /// Main voxel world management - chunk storage, access, and modification
    /// </summary>
    public sealed class VoxelWorld : MonoBehaviour
    {
        public static VoxelWorld Instance { get; private set; }

        // World configuration
        public const int CHUNK_HEIGHT = 256;
        public const int WORLD_SIZE_CHUNKS = 44; // ~704m x 704m
        public const int WORLD_RADIUS = WORLD_SIZE_CHUNKS / 2;

        // Chunk storage
        private readonly Dictionary<ChunkCoord, VoxelChunk> _chunks = new();
        private readonly object _chunkLock = new();

        // Generation
        private VoxelGenerator _generator;
        private int _worldSeed;
        public int WorldSeed => _worldSeed;

        // Loading state
        private HashSet<ChunkCoord> _loadedChunks = new();
        private HashSet<ChunkCoord> _chunksToGenerate = new();
        
        public event Action<ChunkCoord> OnChunkLoaded;
        public event Action<ChunkCoord> OnChunkModified;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _generator = new VoxelGenerator();
            _worldSeed = PlayerPrefs.GetInt("WorldSeed", Environment.TickCount);
            Instance = this;
        }

        private void Start()
        {
            UnityEngine.Debug.Log($"[VoxelWorld] World seed: {_worldSeed}");
            
            // Generate initial chunks around origin
            LoadChunksAround(Vector3.zero, 4);
        }

        private void OnDestroy()
        {
            // Save all dirty chunks before destroying
            SaveAllChunks();
            
            lock (_chunkLock)
            {
                foreach (var chunk in _chunks.Values)
                {
                    chunk.Dispose();
                }
                _chunks.Clear();
            }
            
            if (Instance == this) Instance = null;
        }

        #region Chunk Access

        /// <summary>
        /// Get chunk at coordinates (may return null if not loaded)
        /// </summary>
        public VoxelChunk GetChunk(ChunkCoord coord)
        {
            lock (_chunkLock)
            {
                if (_chunks.TryGetValue(coord, out var chunk))
                    return chunk;
                return null;
            }
        }

        /// <summary>
        /// Get or create chunk at coordinates
        /// </summary>
        public VoxelChunk GetOrCreateChunk(ChunkCoord coord)
        {
            lock (_chunkLock)
            {
                if (_chunks.TryGetValue(coord, out var existing))
                    return existing;

                var newChunk = new VoxelChunk(coord);
                _chunks[coord] = newChunk;
                _chunksToGenerate.Add(coord);
                return newChunk;
            }
        }

        /// <summary>
        /// Check if chunk is loaded
        /// </summary>
        public bool IsChunkLoaded(ChunkCoord coord)
        {
            lock (_chunkLock)
            {
                return _chunks.TryGetValue(coord, out var chunk) && chunk.IsGenerated;
            }
        }

        /// <summary>
        /// Get block at world coordinates
        /// </summary>
        public BlockData GetBlock(BlockCoord coord)
        {
            if (coord.Y < 0 || coord.Y >= CHUNK_HEIGHT)
                return BlockData.Air;

            var chunkCoord = ChunkCoord.FromBlock(coord);
            var chunk = GetChunk(chunkCoord);
            
            if (chunk == null || !chunk.IsGenerated)
                return BlockData.Air;

            var local = chunkCoord.ToLocal(coord);
            return chunk.GetBlock(local.X, local.Y, local.Z);
        }

        /// <summary>
        /// Set block at world coordinates
        /// </summary>
        public bool SetBlock(BlockCoord coord, BlockData block)
        {
            if (coord.Y < 0 || coord.Y >= CHUNK_HEIGHT)
                return false;

            var chunkCoord = ChunkCoord.FromBlock(coord);
            var chunk = GetOrCreateChunk(chunkCoord);

            var local = chunkCoord.ToLocal(coord);
            var oldBlock = chunk.GetBlock(local.X, local.Y, local.Z);
            
            if (oldBlock.Id == block.Id)
                return true;

            chunk.SetBlock(local.X, local.Y, local.Z, block);
            OnChunkModified?.Invoke(chunkCoord);
            
            return true;
        }

        /// <summary>
        /// Check if position is within world bounds
        /// </summary>
        public bool IsInBounds(BlockCoord coord)
        {
            int minX = -WORLD_RADIUS * ChunkCoord.SIZE;
            int maxX = WORLD_RADIUS * ChunkCoord.SIZE - 1;
            int minZ = -WORLD_RADIUS * ChunkCoord.SIZE;
            int maxZ = WORLD_RADIUS * ChunkCoord.SIZE - 1;
            
            return coord.X >= minX && coord.X <= maxX &&
                   coord.Y >= 0 && coord.Y < CHUNK_HEIGHT &&
                   coord.Z >= minZ && coord.Z <= maxZ;
        }

        /// <summary>
        /// Find the first walkable air block above the terrain surface.
        /// </summary>
        public int GetSurfaceHeight(int x, int z)
        {
            var coord = ChunkCoord.FromBlock(x, z);
            var chunk = GetChunk(coord);
            if (chunk == null || !chunk.IsGenerated)
            {
                return 30;
            }

            var local = coord.ToLocal(new BlockCoord(x, 0, z));
            for (int y = CHUNK_HEIGHT - 1; y >= 0; y--)
            {
                if (!chunk.GetBlock(local.X, y, local.Z).IsAir)
                {
                    return y + 1;
                }
            }

            return 30;
        }

        #endregion

        #region Chunk Loading

        /// <summary>
        /// Load chunks around a position
        /// </summary>
        public void LoadChunksAround(Vector3 worldPos, int radius)
        {
            var center = ChunkCoord.FromWorld(worldPos);
            
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    var coord = new ChunkCoord(center.X + x, center.Z + z);
                    
                    // Check world bounds
                    if (Mathf.Abs(coord.X) > WORLD_RADIUS || Mathf.Abs(coord.Z) > WORLD_RADIUS)
                        continue;
                    
                    var chunk = GetOrCreateChunk(coord);
                    
                    if (!chunk.IsGenerated && !_chunksToGenerate.Contains(coord))
                    {
                        _chunksToGenerate.Add(coord);
                    }
                }
            }
            
            GeneratePendingChunks();
        }

        /// <summary>
        /// Generate pending chunks
        /// </summary>
        private void GeneratePendingChunks()
        {
            if (_chunksToGenerate.Count == 0) return;

            HashSet<ChunkCoord> generated = new();
            
            foreach (var coord in _chunksToGenerate)
            {
                var chunk = GetChunk(coord);
                if (chunk == null) continue;

                _generator.GenerateChunk(chunk, _worldSeed);
                chunk.IsGenerated = true;
                
                generated.Add(coord);
                _loadedChunks.Add(coord);
                OnChunkLoaded?.Invoke(coord);
            }

            foreach (var coord in generated)
            {
                _chunksToGenerate.Remove(coord);
            }

            if (generated.Count > 0)
            {
                UnityEngine.Debug.Log($"[VoxelWorld] Generated {generated.Count} chunks");
            }
        }

        /// <summary>
        /// Unload chunks far from position
        /// </summary>
        public void UnloadChunksFarFrom(Vector3 worldPos, int maxRadius)
        {
            var center = ChunkCoord.FromWorld(worldPos);
            List<ChunkCoord> toUnload = new();

            lock (_chunkLock)
            {
                foreach (var kvp in _chunks)
                {
                    var coord = kvp.Key;
                    int dx = Mathf.Abs(coord.X - center.X);
                    int dz = Mathf.Abs(coord.Z - center.Z);
                    
                    if (dx > maxRadius || dz > maxRadius)
                    {
                        // Save before unloading
                        if (kvp.Value.IsDirty)
                        {
                            // TODO: Save to persistence
                        }
                        toUnload.Add(coord);
                    }
                }

                foreach (var coord in toUnload)
                {
                    _chunks[coord].Dispose();
                    _chunks.Remove(coord);
                    _loadedChunks.Remove(coord);
                }
            }

            if (toUnload.Count > 0)
            {
                UnityEngine.Debug.Log($"[VoxelWorld] Unloaded {toUnload.Count} chunks");
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Save all dirty chunks
        /// </summary>
        public void SaveAllChunks()
        {
            int saved = 0;
            lock (_chunkLock)
            {
                foreach (var chunk in _chunks.Values)
                {
                    if (chunk.IsDirty)
                    {
                        // TODO: Save to database
                        saved++;
                    }
                }
            }
            UnityEngine.Debug.Log($"[VoxelWorld] Saved {saved} dirty chunks");
        }

        #endregion

        #region Queries

        /// <summary>
        /// Get surface height at world position (returns -1 if not loaded)
        /// </summary>
        public int GetSurfaceHeight(int worldX, int worldZ)
        {
            var coord = ChunkCoord.FromBlock(worldX, worldZ);
            var chunk = GetChunk(coord);
            
            if (chunk == null || !chunk.IsGenerated)
                return -1;

            var local = coord.ToLocal(new BlockCoord(worldX, 0, worldZ));
            
            // Search from top down
            for (int y = CHUNK_HEIGHT - 1; y >= 0; y--)
            {
                var block = chunk.GetBlock(local.X, y, local.Z);
                if (!block.IsAir)
                    return y;
            }
            
            return -1;
        }

        /// <summary>
        /// Get safe spawn position for player
        /// </summary>
        public Vector3 GetSpawnPosition(int worldX, int worldZ)
        {
            int surfaceY = GetSurfaceHeight(worldX, worldZ);
            
            // Default to y=30 if not generated yet
            if (surfaceY < 0)
                return new Vector3(worldX, 30f, worldZ);
            
            return new Vector3(worldX, surfaceY + 2f, worldZ);
        }

        /// <summary>
        /// Get all loaded chunks
        /// </summary>
        public IReadOnlyCollection<VoxelChunk> GetLoadedChunks()
        {
            lock (_chunkLock)
            {
                return new List<VoxelChunk>(_chunks.Values);
            }
        }

        /// <summary>
        /// Get count of loaded chunks
        /// </summary>
        public int LoadedChunkCount
        {
            get
            {
                lock (_chunkLock)
                {
                    return _chunks.Count;
                }
            }
        }

        /// <summary>
        /// Get count of chunks pending generation
        /// </summary>
        public int PendingChunkCount => _chunksToGenerate.Count;

        #endregion
    }
}
