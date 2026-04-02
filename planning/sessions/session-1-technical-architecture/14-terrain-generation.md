# Session 1: Technical Architecture - Terrain Generation Specification

> **Navigation**: [← Previous: Security & Authentication](12-security-spec.md) | [Index]([AGENTS-READ-FIRST]-index.md) | [Next: Error Handling](11-error-handling.md)
> 
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

## 14. Terrain Generation Specification

### Executive Summary

Societies employs a **deterministic, multi-layer procedural terrain generation** system for its voxel world. The system uses FastNoiseLite-based 3D noise to generate diverse, realistic terrain within performance constraints appropriate for a multiplayer ecosystem simulation.

**Core Technical Decisions**:
- **FastNoiseLite**: Selected for superior performance (3-5x faster than Godot SimplexNoise) and 3D noise support [r1-godot-noise-research.md]
- **Seed-based determinism**: Same seed = identical world, enabling reproduction for debugging and sharing
- **Multi-octave layering**: Continental → erosion → biome → surface → geology → caves → resources
- **16×16×256 chunks**: Optimal balance between generation granularity and rendering efficiency
- **Background generation**: Priority queue system prevents blocking gameplay during chunk loading

**Key Parameters**:
| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Block size | 1m³ | Standard voxel scale |
| Chunk size | 16×16×256 | 65,536 blocks/chunk; optimal for Godot memory patterns |
| World surface | 0.5 km² (MVP) / 2 km² (post-MVP) | 512×512 blocks (MVP), 2048×2048 (post-MVP) |
| Vertical range | -200 to +56 (256 blocks) | Accommodates deep mining + mountains |
| Generation time budget | <50ms per chunk | Maintains 20 TPS target [r1-performance-budget.md] |

**Generation Approach Summary**:
```
World Seed → 3D Noise (continental, erosion, biome, caves)
     ↓
Multi-Layer Sampling → Biome Determination → Surface/Geology
     ↓
Resource Placement → Chunk Generation → Cache/Store
```

---

## 14.1 Noise Generation Architecture

### Noise Library Selection

**FastNoiseLite Selected** [r1-godot-noise-research.md]:
- **Performance**: 3-5x faster than Godot's built-in SimplexNoise
- **Features**: 3D support, multiple noise types (Simplex, Perlin, Value), fractal layers
- **License**: MIT (compatible with Societies)
- **C# Support**: Native .NET integration with Godot 4.x

**Noise Types Used**:
| Purpose | Noise Type | Octaves | Frequency | Persistence |
|---------|-----------|---------|-----------|-------------|
| Continental | Simplex | 2 | 0.002 | 0.5 |
| Erosion | Simplex | 4 | 0.008 | 0.5 |
| Biome temperature | Simplex | 3 | 0.001 | 0.5 |
| Biome humidity | Simplex | 3 | 0.001 | 0.5 |
| Cave system | 3D Simplex | 2 | 0.015 | 0.5 |
| Ore distribution | Value | 3 | 0.03 | 0.5 |

### 3D vs 2D Noise Usage

**2D Noise (XY-plane, elevation-based)**:
- Continental shapes
- Biome climate (temperature, humidity)
- Surface features (roughness)

**3D Noise (XYZ volume)**:
- Cave system generation
- Ore vein distribution
- Underground geological features
- Overhang/cliff formations

**Rationale**: 3D noise is 50-100% more expensive than 2D [r1-godot-noise-research.md]. Use 2D where possible, reserve 3D for volumetric features.

### Seed-Based Determinism

**World Seed Architecture**:
```csharp
public class WorldSeed
{
    public long BaseSeed { get; }
    public long ContinentalSeed => BaseSeed;
    public long ErosionSeed => BaseSeed + 1;
    public long TemperatureSeed => BaseSeed + 1000;
    public long HumiditySeed => BaseSeed + 2000;
    public long CaveSeed => BaseSeed + 3000;
    public long OreSeed => BaseSeed + 4000;
    
    public WorldSeed(long baseSeed) => BaseSeed = baseSeed;
}
```

**Deterministic Requirements**:
1. Same seed + same coordinate = identical block
2. Chunk generation order does not affect results
3. Thread-safe noise sampling (no RNG state issues)
4. Deterministic across runs (no time-based noise)

---

## 14.2 Terrain Generation Layers

Terrain is generated in **7 sequential layers**, each building upon the previous:

### Layer 1: Base Terrain Shape (Continentalness)

Determines fundamental elevation patterns—mountains, plains, valleys.

**Parameters**:
```csharp
public class ContinentalParams
{
    public float SeaLevel { get; set; } = 0.0f;  // Y = 0
    public float MinHeight { get; set; } = -200f;  // Deepest cave/underground
    public float MaxHeight { get; set; } = 56f;    // Mountain peaks (256 total blocks)
    
    // Noise configuration
    public int Octaves { get; set; } = 2;
    public float Frequency { get; set; } = 0.002f;
    public float Amplitude { get; set; } = 80f;  // Max height variation
}
```

**Generation Formula**:
```csharp
float GetBaseHeight(float x, float z)
{
    // Low-frequency continental noise
    float continental = _continentalNoise.GetNoise(x, z);
    
    // Map to world height range
    float height = continental * _params.Amplitude;
    
    // Add sea-level offset
    height += _params.SeaLevel;
    
    // Clamp to valid range
    return Mathf.Clamp(height, _params.MinHeight, _params.MaxHeight);
}
```

**Output**: Base height field (2D array of float elevation values)

---

### Layer 2: Erosion & Detail

Adds realistic surface roughness—hills, ravines, cliffs.

**Parameters**:
```csharp
public class ErosionParams
{
    public int Octaves { get; set; } = 4;
    public float Frequency { get; set; } = 0.008f;
    public float Amplitude { get; set; } = 15f;  // Smaller variations
    public float RidgeWeight { get; set; } = 0.3f;  // Mountain ridges
}
```

**Generation**:
```csharp
float ApplyErosion(float baseHeight, float x, float z)
{
    // High-frequency erosion noise
    float erosion = _erosionNoise.GetNoise(x, z);
    
    // Ridge formation (absolute value creates peaks)
    float ridges = Mathf.Abs(_erosionNoise.GetNoise(x + 1000, z + 1000));
    
    // Combine with base height
    float detail = erosion * _params.Amplitude;
    detail += ridges * _params.RidgeWeight * _params.Amplitude;
    
    return baseHeight + detail;
}
```

---

### Layer 3: Biome Determination

Assigns biomes based on climate parameters and elevation.

**Climate Parameters**:
```csharp
public struct ClimatePoint
{
    public float Temperature;    // -1.0 (cold) to 1.0 (hot)
    public float Humidity;       // -1.0 (dry) to 1.0 (wet)
    public float Elevation;      // Normalized 0.0 to 1.0
}
```

**Biome Selection Matrix**:

| Biome | Temperature | Humidity | Elevation Range | Characteristics |
|-------|-------------|----------|-----------------|-----------------|
| **Boreal Forest** | -0.5 to 0.2 | -0.3 to 0.5 | 0-800m | Coniferous, seasonal snow |
| **Subtropical Desert** | 0.3 to 1.0 | -1.0 to -0.2 | 0-600m | Arid, extreme temperature swings |
| **Jungle** | 0.2 to 1.0 | 0.3 to 1.0 | 0-1000m | Dense vegetation, high precipitation |

**Biome Selection Algorithm**:
```csharp
public BiomeType SelectBiome(ClimatePoint climate)
{
    // Temperature-based classification
    bool isHot = climate.Temperature > 0.2f;
    bool isCold = climate.Temperature < -0.3f;
    bool isHumid = climate.Humidity > 0.0f;
    bool isArid = climate.Humidity < -0.3f;
    
    // Elevation-based sub-biome considerations handled in Layer 5
    
    if (isHot && isArid)
        return BiomeType.Desert;
    else if (isHot && isHumid)
        return BiomeType.Jungle;
    else if (isCold || (!isHot && !isArid))
        return BiomeType.Boreal;
    else
        return BiomeType.Boreal;  // Default fallback
}
```

**Biome Boundary Smoothing**:
- 5-block transition zone between biomes
- Linear interpolation of block types (e.g., grass → sand)
- Climate noise smoothing: 0.05 frequency noise for natural transitions

---

### Layer 4: Surface Layer Generation

Generates surface blocks based on biome and elevation.

**Surface Block Types by Biome**:

```csharp
public SurfaceLayers GetSurfaceLayers(BiomeType biome, float elevation)
{
    return biome switch
    {
        BiomeType.Boreal => new SurfaceLayers
        {
            TopLayer = BlockType.Grass,
            SubsurfaceLayer = BlockType.Dirt,
            TransitionLayer = BlockType.Stone,
            TopLayerDepth = 1,
            SubsurfaceDepth = 3
        },
        BiomeType.Desert => new SurfaceLayers
        {
            TopLayer = BlockType.Sand,
            SubsurfaceLayer = BlockType.Sandstone,
            TransitionLayer = BlockType.Stone,
            TopLayerDepth = 3,
            SubsurfaceDepth = 5
        },
        BiomeType.Jungle => new SurfaceLayers
        {
            TopLayer = BlockType.RichSoil,
            SubsurfaceLayer = BlockType.Dirt,
            TransitionLayer = BlockType.Stone,
            TopLayerDepth = 2,
            SubsurfaceDepth = 4
        },
        _ => GetDefaultLayers()
    };
}
```

**Elevation-Based Snow Coverage** (Boreal biome):
- Permanent snow above 800m elevation
- Seasonal snow below 800m (based on game time)
- Snow depth increases with elevation

---

### Layer 5: Underground/Geological Layers

**Stratified Underground Structure**:

| Depth Range | Layer | Block Types | Features |
|-------------|-------|-------------|----------|
| 0 to -10 | Topsoil/Subsurface | Dirt, sand, rich soil | Roots, shallow ores |
| -10 to -30 | Weathered rock | Stone, gravel | Surface ore veins |
| -30 to -80 | Bedrock layer | Hard stone, basalt | Deep ore deposits |
| -80 to -150 | Deep crust | Granite, gneiss | Rare minerals |
| -150 to -200 | Mantle boundary | Obsidian, magma blocks | Deepest resources |

**Geological Strata Generation**:
```csharp
public BlockType GetUndergroundBlock(float surfaceY, float currentY, BiomeType biome)
{
    float depth = surfaceY - currentY;  // How deep below surface
    
    if (depth < 10)
    {
        // Surface layer - biome dependent
        return GetBiomeSubsurface(biome);
    }
    else if (depth < 30)
    {
        // Transition to stone with variation
        float transitionNoise = _strataNoise.GetNoise(currentX, currentY, currentZ);
        return transitionNoise > 0.3f ? BlockType.Gravel : BlockType.Stone;
    }
    else if (depth < 80)
    {
        // Deep stone with occasional ore
        return BlockType.Stone;
    }
    else if (depth < 150)
    {
        // Granite layer
        return BlockType.Granite;
    }
    else
    {
        // Deepest layer
        return BlockType.Obsidian;
    }
}
```

---

### Layer 6: Cave Systems

**3D Noise-Based Cave Generation**:

```csharp
public bool IsCave(float x, float y, float z)
{
    // 3D cave noise
    float caveNoise = _caveNoise.GetNoise(x, y, z);
    
    // Threshold for cave vs solid
    const float CAVE_THRESHOLD = 0.3f;
    
    // Depth-based cave frequency (more caves at certain depths)
    float depthFactor = GetCaveFrequencyAtDepth(y);
    
    return caveNoise > (CAVE_THRESHOLD / depthFactor);
}

float GetCaveFrequencyAtDepth(float y)
{
    // Peak cave density between -30 and -80
    if (y > -20) return 0.5f;  // Surface: fewer caves
    if (y > -100) return 1.0f; // Optimal depth
    return 0.7f;               // Deep: fewer caves
}
```

**Cave Features**:
- **Tunnels**: Wormhole patterns using 3D noise gradient following
- **Chambers**: Large open spaces (noise valleys)
- **Water pockets**: Caves below sea level fill with water
- **Lava pools**: Deepest caves (> -150m) contain lava

---

### Layer 7: Ore/Resource Distribution

**Resource Distribution Model**:

| Resource | Depth Range | Vein Size | Rarity | Biome Modifier |
|----------|-------------|-----------|--------|----------------|
| Coal | -10 to -80 | Medium | Common | None |
| Iron | -20 to -100 | Medium | Uncommon | +20% in Boreal |
| Copper | -30 to -120 | Small | Uncommon | +20% in Desert |
| Gold | -50 to -150 | Small | Rare | +20% in Desert |
| Gems | -80 to -200 | Tiny | Very Rare | None |
| Obsidian | -150 to -200 | Large | Common | None |

**Ore Generation Algorithm**:
```csharp
public OreType? GetOreAt(float x, float y, float z, BiomeType biome)
{
    // Check each ore type
    foreach (var oreDef in OreDefinitions.All)
    {
        if (!IsInDepthRange(y, oreDef.MinDepth, oreDef.MaxDepth))
            continue;
            
        // Sample ore noise (unique seed per ore type)
        float oreNoise = GetOreNoise(oreDef.Type, x, y, z);
        
        // Apply rarity threshold
        float threshold = oreDef.BaseRarity;
        
        // Apply biome modifiers
        threshold *= GetBiomeModifier(biome, oreDef.Type);
        
        // Apply depth frequency curve
        threshold *= GetDepthFrequency(y, oreDef.OptimalDepth);
        
        if (oreNoise > threshold)
            return oreDef.Type;
    }
    
    return null;
}
```

---

## 14.3 Biome Integration

### 3-Biome System Alignment

The terrain generation aligns with the existing 3-biome system from [01-architecture-overview.md]:

**Biome-to-Block Mapping**:

| Biome | Surface Block | Subsurface | Vegetation | Climate Noise |
|-------|--------------|------------|------------|---------------|
| **Boreal** | Grass (summer) / Snow (winter) | Dirt → Stone | Coniferous trees, berries | Cold temp, moderate humidity |
| **Desert** | Sand | Sandstone → Stone | Cacti, sparse shrubs | Hot temp, low humidity |
| **Jungle** | Rich soil | Dirt → Stone | Dense hardwood, vines | Hot temp, high humidity |

### Elevation-Based Sub-Biomes

Within each biome, elevation creates sub-variants:

**Boreal Sub-Biomes**:
- 0-200m: Taiga (spruce, marshy)
- 200-500m: Forest (mixed pine/spruce)
- 500-800m: Montane (stunted trees, rocky)
- 800m+: Alpine (tundra, no trees, permanent snow)

**Desert Sub-Biomes**:
- 0-100m: Salt flats (cracked earth)
- 100-300m: Dunes (shifting sands)
- 300-500m: Rocky desert (canyons, caves)
- 500m+: Desert mountains (cooler, sparse vegetation)

**Jungle Sub-Biomes**:
- 0-150m: Riverine (dense vegetation, water)
- 150-400m: Rainforest (canopy layers)
- 400-700m: Cloud forest (mist, epiphytes)
- 700m+: Peaks (cooler, pine-oak transition)

### Climate Parameters Integration

**Temperature Calculation**:
```csharp
float GetTemperature(float x, float z, float elevation)
{
    // Base biome temperature from 2D noise
    float biomeTemp = _tempNoise.GetNoise(x, z);  // -1 to 1
    
    // Elevation modifier: -6.5°C per 1000m
    float elevationModifier = (elevation / 1000f) * -6.5f;
    
    // Convert noise to Celsius
    float baseCelsius = biomeTemp switch
    {
        < -0.3f => -5f,   // Cold
        < 0.3f => 10f,    // Moderate
        _ => 25f          // Hot
    };
    
    return baseCelsius + elevationModifier;
}
```

**Humidity Calculation**:
```csharp
float GetHumidity(float x, float z, BiomeType biome)
{
    float humidityNoise = _humidityNoise.GetNoise(x, z);
    
    // Biome base humidity
    float baseHumidity = biome switch
    {
        BiomeType.Desert => 0.1f,
        BiomeType.Boreal => 0.4f,
        BiomeType.Jungle => 0.8f,
        _ => 0.5f
    };
    
    // Noise adds ±30% variation
    return Mathf.Clamp(baseHumidity + (humidityNoise * 0.3f), 0f, 1f);
}
```

---

## 14.4 Geological Strata System

### Layered Underground Structure

The underground is organized into distinct geological layers with realistic transitions:

```
Surface (0 to -10m)
├── Topsoil (0 to -1m): Biome-specific surface block
├── Subsoil (-1 to -5m): Dirt, sand, or rich soil
└── Weathered rock (-5 to -10m): Soft stone, gravel

Intermediate (-10 to -80m)
├── Sedimentary layer (-10 to -30m): Stone with surface ores
├── Transition zone (-30 to -50m): Mixed stone types
└── Bedrock contact (-50 to -80m): Harder stone, deep ores

Deep crust (-80 to -200m)
├── Granite layer (-80 to -120m): Igneous rock, rare minerals
├── Gneiss layer (-120 to -150m): Metamorphic, gems
└── Mantle boundary (-150 to -200m): Obsidian, volcanic features
```

### Depth-Based Ore Distribution

**Distribution Curves**:

Each ore type has a bell-curve distribution by depth:

```csharp
float GetOreFrequencyAtDepth(OreType ore, float depth)
{
    var def = OreDefinitions.Get(ore);
    
    // Gaussian distribution around optimal depth
    float diff = depth - def.OptimalDepth;
    float variance = def.DepthVariance;
    
    // Gaussian: e^(-(x-μ)² / 2σ²)
    return Mathf.Exp(-(diff * diff) / (2 * variance * variance));
}
```

**Example: Iron Distribution**:
- Optimal depth: -60m
- Variance: ±25m
- Frequency at -60m: 100%
- Frequency at -35m: 60%
- Frequency at -85m: 60%
- Frequency at -100m: 25%

### Realistic Geology Simulation

**Strata Continuity**:
- Geological layers maintain continuity across chunk boundaries
- Noise sampling uses world coordinates (not chunk-local) for seamless strata
- Transition zones blend between layers over 5-10 block vertical span

**Fault Lines** (post-MVP):
- Occasional vertical displacement of strata
- Creates interesting mining challenges
- Implemented via secondary fault noise

---

## 14.5 Resource Distribution

### Resource Node Placement

**Resource Node Types**:

| Node Type | Biome | Elevation | Density | Distribution |
|-----------|-------|-----------|---------|--------------|
| Trees | Boreal | 0-600m | Medium | Clustered |
| Cacti | Desert | 0-400m | Sparse | Random |
| Hardwood | Jungle | 0-700m | Dense | Even |
| Berry bushes | Boreal | 100-500m | Medium | Random |
| Surface stone | All | 400m+ | High | Even |
| Surface ores | All | 50-300m | Low | Veined |

**Tree Distribution Algorithm**:
```csharp
public void GenerateTrees(Chunk chunk, BiomeType biome)
{
    // Use Poisson disk sampling for natural spacing
    var treePositions = PoissonDiskSampling.Generate(
        chunk.Bounds, 
        minRadius: 5f,  // Minimum 5m between trees
        maxAttempts: 30
    );
    
    foreach (var pos in treePositions)
    {
        // Check if valid location (surface, not water)
        if (!IsValidTreeLocation(pos, chunk))
            continue;
            
        // Tree type based on biome + elevation
        var treeType = GetTreeType(biome, pos.Y);
        
        // Density check - not all valid positions get trees
        float densityNoise = _treeNoise.GetNoise(pos.X, pos.Z);
        if (densityNoise > 0.4f)  // 60% of valid positions
        {
            chunk.AddEntity(new TreeEntity(treeType, pos));
        }
    }
}
```

### Ore Vein Generation

**Vein Structure**:
- **Size**: Small (10-30 blocks), Medium (50-150), Large (200-500)
- **Shape**: Irregular 3D clusters following simplex noise gradients
- **Distribution**: Poisson point process for vein centers

**Vein Generation**:
```csharp
public class OreVein
{
    public Vector3 Center { get; set; }
    public OreType Ore { get; set; }
    public float Radius { get; set; }
    public float Density { get; set; }  // 0.0 to 1.0
    
    public bool Contains(Vector3 pos)
    {
        float dist = Center.DistanceTo(pos);
        if (dist > Radius) return false;
        
        // Density falls off from center
        float localDensity = Density * (1 - dist / Radius);
        
        // Add noise for irregular edges
        float edgeNoise = _veinNoise.GetNoise(pos.X, pos.Y, pos.Z);
        
        return edgeNoise < localDensity;
    }
}
```

### Rare Resource Spawning

**Rare Resource Rules**:
1. **Distance-based scarcity**: Rare resources spawn farther from spawn
2. **Depth correlation**: Deepest resources are rarest
3. **Biome exclusivity**: Some resources only in specific biomes

**Spawn Probability**:
```csharp
float GetRareResourceProbability(ResourceType resource, Vector3 pos, float distanceFromSpawn)
{
    float baseProb = resource.BaseRarity;  // 0.001 to 0.1
    
    // Distance factor: 2x more likely at max distance
    float distanceFactor = 1 + (distanceFromSpawn / MaxWorldDistance);
    
    // Depth factor (for underground resources)
    float depthFactor = 1 + (Mathf.Abs(pos.Y) / 200f);
    
    // Biome factor
    float biomeFactor = GetBiomeRarityModifier(resource, GetBiomeAt(pos));
    
    return baseProb * distanceFactor * depthFactor * biomeFactor;
}
```

---

## 14.6 Generation Pipeline

### Chunk Generation Workflow

```
1. REQUEST
   Player/camera enters chunk radius
        ↓
2. PRIORITY QUEUE
   Chunk added to priority queue (distance-based)
        ↓
3. TERRAIN GENERATION (Background Thread)
   Layers 1-7 applied sequentially
   ~10-30ms for full generation
        ↓
4. MESH GENERATION
   Surface mesh + collision generated
   ~5-15ms for mesh
        ↓
5. ENTITY PLACEMENT
   Trees, ores, features added
   ~2-5ms
        ↓
6. CACHE & NOTIFY
   Store in chunk cache
   Notify clients of new chunk
        ↓
7. UNLOAD (when distant)
   Serialize modifications
   Remove from cache
```

### Background Thread Generation

**Threading Architecture**:
```csharp
public class TerrainGenerator
{
    private Thread _generationThread;
    private ConcurrentQueue<ChunkRequest> _requestQueue;
    private ConcurrentDictionary<Vector2I, Chunk> _completedChunks;
    private bool _isRunning;
    
    public void Start()
    {
        _isRunning = true;
        _generationThread = new Thread(GenerationLoop);
        _generationThread.Start();
    }
    
    private void GenerationLoop()
    {
        while (_isRunning)
        {
            if (_requestQueue.TryDequeue(out var request))
            {
                var chunk = GenerateChunk(request.ChunkX, request.ChunkZ);
                _completedChunks[new Vector2I(request.ChunkX, request.ChunkZ)] = chunk;
            }
            else
            {
                Thread.Sleep(1);  // Prevent CPU spinning
            }
        }
    }
}
```

**Safety Considerations**:
- Noise generators are thread-safe (FastNoiseLite stateless)
- Chunk data structures copied to main thread via queue
- No direct scene tree modification from background thread

### Chunk Priority Queue

**Priority Calculation**:
```csharp
public float CalculatePriority(ChunkRequest request, Vector3 playerPosition)
{
    float distance = request.Center.DistanceTo(playerPosition);
    
    // Exponential priority: closer = much higher
    float distancePriority = 1.0f / (1.0f + distance * 0.1f);
    
    // View direction bonus (chunks in front of player)
    float viewBonus = IsInViewDirection(request, playerPosition) ? 1.5f : 1.0f;
    
    // Age factor (older requests lose priority slightly)
    float ageFactor = 1.0f - (request.Age * 0.01f);
    
    return distancePriority * viewBonus * Mathf.Max(0.5f, ageFactor);
}
```

**Queue Management**:
- Max queue size: 100 chunks
- Expire old requests (> 30 seconds)
- Cancel requests for unloaded chunks

### Cache Management

**Chunk Cache Architecture**:
```csharp
public class ChunkCache
{
    private LRUCache<Vector2I, Chunk> _cache;
    private int _maxCachedChunks;
    
    public ChunkCache(int maxChunks = 1024)
    {
        _maxCachedChunks = maxChunks;
        _cache = new LRUCache<Vector2I, Chunk>(maxChunks);
    }
    
    public Chunk GetChunk(int x, int z)
    {
        var key = new Vector2I(x, z);
        if (_cache.TryGetValue(key, out var chunk))
            return chunk;
        return null;
    }
    
    public void StoreChunk(Chunk chunk)
    {
        _cache.Add(chunk.Coordinates, chunk);
    }
}
```

**Cache Eviction Policy**:
- LRU (Least Recently Used) eviction
- Modified chunks saved before eviction
- Max memory: ~1024 chunks = ~68MB (65KB per chunk compressed)

---

## 14.7 Persistence Strategy

### Generated vs Modified Chunks

**Chunk State Tracking**:
```csharp
public class ChunkMetadata
{
    public Vector2I Coordinates { get; set; }
    public bool IsModified { get; set; }  // Player modified?
    public DateTime LastAccessed { get; set; }
    public long GenerationTick { get; set; }
    public int ModificationCount { get; set; }
}
```

**Storage Strategy**:
- **Generated chunks**: Regenerated from seed on demand (not stored)
- **Modified chunks**: Store delta (only changes) in database
- **Critical chunks** (spawn, settlements): Full backup

**Delta Compression**:
```csharp
public class ChunkDelta
{
    public Vector2I Coordinates { get; set; }
    public List<BlockChange> Changes { get; set; }
    
    public byte[] Serialize()
    {
        // Run-length encoding for empty sections
        // Only store modified blocks
        // Typical: 50-200 bytes per modified chunk
    }
}
```

### Storage Format

**Modified Chunk Storage** (PostgreSQL):
```sql
CREATE TABLE chunk_modifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
    chunk_x INTEGER NOT NULL,
    chunk_z INTEGER NOT NULL,
    delta_data BYTEA NOT NULL,  -- Compressed delta
    modified_at TIMESTAMP DEFAULT NOW(),
    modification_count INTEGER DEFAULT 1,
    
    UNIQUE(world_id, chunk_x, chunk_z)
);

CREATE INDEX idx_chunks_world_coords ON chunk_modifications(world_id, chunk_x, chunk_z);
```

**Delta Format**:
```csharp
[Serializable]
public struct BlockChange
{
    public ushort BlockIndex;    // Index into 16×16×256 array
    public byte BlockType;       // New block type
    public byte Metadata;        // Orientation, state, etc.
}
```

### World Seed Storage

**World Metadata**:
```csharp
public class WorldMetadata
{
    public long Seed { get; set; }
    public string GenerationVersion { get; set; }  // "1.0.0"
    public int ChunkSize { get; set; } = 16;
    public int WorldHeight { get; set; } = 256;
    public Dictionary<string, float> NoiseParams { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Stored in `worlds.settings` JSONB field:
```json
{
  "terrain": {
    "seed": 123456789,
    "generation_version": "1.0.0",
    "noise_params": {
      "continental_frequency": 0.002,
      "erosion_octaves": 4,
      "cave_threshold": 0.3
    }
  }
}
```

---

## 14.8 C# Implementation

### TerrainGenerator Class Structure

```csharp
using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading;

public partial class TerrainGenerator : Node
{
    // Noise instances (one per layer)
    private FastNoiseLite _continentalNoise;
    private FastNoiseLite _erosionNoise;
    private FastNoiseLite _temperatureNoise;
    private FastNoiseLite _humidityNoise;
    private FastNoiseLite _caveNoise;
    private FastNoiseLite _oreNoise;
    
    // Configuration
    private TerrainParams _params;
    private WorldSeed _worldSeed;
    
    // Threading
    private Thread _generationThread;
    private ConcurrentQueue<ChunkRequest> _requestQueue;
    private ConcurrentDictionary<Vector2I, Chunk> _completedChunks;
    private volatile bool _isRunning;
    
    // Events
    [Signal] public delegate void ChunkGeneratedEventHandler(Vector2I coords, Chunk chunk);
    [Signal] public delegate void GenerationErrorEventHandler(Vector2I coords, string error);
    
    public override void _Ready()
    {
        InitializeNoises();
        StartGenerationThread();
    }
    
    public void Initialize(long worldSeed)
    {
        _worldSeed = new WorldSeed(worldSeed);
        InitializeNoises();
    }
    
    private void InitializeNoises()
    {
        // Continental (base terrain)
        _continentalNoise = new FastNoiseLite();
        _continentalNoise.Seed = (int)_worldSeed.ContinentalSeed;
        _continentalNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _continentalNoise.Frequency = 0.002f;
        _continentalNoise.FractalOctaves = 2;
        
        // Erosion (detail)
        _erosionNoise = new FastNoiseLite();
        _erosionNoise.Seed = (int)_worldSeed.ErosionSeed;
        _erosionNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _erosionNoise.Frequency = 0.008f;
        _erosionNoise.FractalOctaves = 4;
        
        // Climate
        _temperatureNoise = new FastNoiseLite();
        _temperatureNoise.Seed = (int)_worldSeed.TemperatureSeed;
        _temperatureNoise.Frequency = 0.001f;
        
        _humidityNoise = new FastNoiseLite();
        _humidityNoise.Seed = (int)_worldSeed.HumiditySeed;
        _humidityNoise.Frequency = 0.001f;
        
        // Caves (3D)
        _caveNoise = new FastNoiseLite();
        _caveNoise.Seed = (int)_worldSeed.CaveSeed;
        _caveNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _caveNoise.Frequency = 0.015f;
        
        // Ore
        _oreNoise = new FastNoiseLite();
        _oreNoise.Seed = (int)_worldSeed.OreSeed;
        _oreNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Value;
        _oreNoise.Frequency = 0.03f;
    }
}
```

### Noise Sampling Code

```csharp
public partial class TerrainGenerator
{
    /// <summary>
    /// Get final height at world position (all layers combined)
    /// </summary>
    public float GetHeight(float worldX, float worldZ)
    {
        // Layer 1: Continental
        float continental = _continentalNoise.GetNoise(worldX, worldZ);
        float height = continental * _params.ContinentalAmplitude;
        
        // Layer 2: Erosion
        float erosion = _erosionNoise.GetNoise(worldX, worldZ);
        float ridges = Mathf.Abs(_erosionNoise.GetNoise(worldX + 1000, worldZ + 1000));
        height += erosion * _params.ErosionAmplitude;
        height += ridges * _params.RidgeWeight * _params.ErosionAmplitude;
        
        // Sea level offset
        height += _params.SeaLevel;
        
        // Clamp to valid range
        return Mathf.Clamp(height, _params.MinHeight, _params.MaxHeight);
    }
    
    /// <summary>
    /// Get climate at world position
    /// </summary>
    public ClimatePoint GetClimate(float worldX, float worldZ, float elevation)
    {
        float temp = _temperatureNoise.GetNoise(worldX, worldZ);
        float humidity = _humidityNoise.GetNoise(worldX, worldZ);
        
        // Normalize to 0-1
        temp = (temp + 1f) * 0.5f;
        humidity = (humidity + 1f) * 0.5f;
        
        return new ClimatePoint
        {
            Temperature = temp,
            Humidity = humidity,
            Elevation = (elevation - _params.MinHeight) / (_params.MaxHeight - _params.MinHeight)
        };
    }
    
    /// <summary>
    /// Check if position is cave (3D noise)
    /// </summary>
    public bool IsCave(float worldX, float worldY, float worldZ)
    {
        float caveNoise = _caveNoise.GetNoise(worldX, worldY, worldZ);
        float threshold = _params.CaveThreshold;
        
        // Depth-based cave frequency
        if (worldY > -20) threshold *= 2f;  // Surface: fewer caves
        else if (worldY < -100) threshold *= 1.4f;  // Deep: fewer caves
        
        return caveNoise > threshold;
    }
}
```

### Biome Selection Algorithm

```csharp
public partial class TerrainGenerator
{
    /// <summary>
    /// Determine biome at world position
    /// </summary>
    public BiomeType GetBiome(float worldX, float worldZ, float elevation)
    {
        var climate = GetClimate(worldX, worldZ, elevation);
        
        // Temperature ranges
        bool isHot = climate.Temperature > 0.6f;
        bool isCold = climate.Temperature < 0.35f;
        bool isHumid = climate.Humidity > 0.55f;
        bool isArid = climate.Humidity < 0.35f;
        
        // Biome selection
        if (isHot && isArid)
            return BiomeType.Desert;
        else if (isHot && isHumid)
            return BiomeType.Jungle;
        else if (isCold || (climate.Temperature >= 0.35f && climate.Temperature <= 0.6f && !isArid))
            return BiomeType.Boreal;
        else
            return BiomeType.Boreal;  // Default
    }
    
    /// <summary>
    /// Get sub-biome based on elevation within base biome
    /// </summary>
    public SubBiomeType GetSubBiome(BiomeType baseBiome, float elevation)
    {
        return baseBiome switch
        {
            BiomeType.Boreal => elevation switch
            {
                < 200f => SubBiomeType.LowlandTaiga,
                < 500f => SubBiomeType.MidElevationForest,
                < 800f => SubBiomeType.MontaneForest,
                _ => SubBiomeType.AlpineTundra
            },
            BiomeType.Desert => elevation switch
            {
                < 100f => SubBiomeType.SaltFlats,
                < 300f => SubBiomeType.SandDunes,
                < 500f => SubBiomeType.RockyDesert,
                _ => SubBiomeType.DesertMountains
            },
            BiomeType.Jungle => elevation switch
            {
                < 150f => SubBiomeType.RiverineJungle,
                < 400f => SubBiomeType.LowlandRainforest,
                < 700f => SubBiomeType.CloudForest,
                _ => SubBiomeType.JunglePeaks
            },
            _ => SubBiomeType.Unknown
        };
    }
}
```

### Resource Placement Logic

```csharp
public partial class TerrainGenerator
{
    /// <summary>
    /// Place resources in chunk after terrain generation
    /// </summary>
    public void PlaceResources(Chunk chunk)
    {
        var biome = chunk.Biome;
        var subBiome = chunk.SubBiome;
        
        // Trees and vegetation
        PlaceVegetation(chunk, biome, subBiome);
        
        // Surface ores and features
        PlaceSurfaceFeatures(chunk, biome);
        
        // Underground ores
        PlaceUndergroundOres(chunk, biome);
    }
    
    private void PlaceVegetation(Chunk chunk, BiomeType biome, SubBiomeType subBiome)
    {
        // Poisson disk sampling for natural spacing
        var positions = PoissonDiskSampling.Generate(
            chunk.Bounds, 
            GetMinTreeSpacing(biome),
            maxAttempts: 30
        );
        
        foreach (var pos in positions)
        {
            if (!IsValidVegetationLocation(pos, chunk))
                continue;
                
            // Density check
            float density = _treeNoise.GetNoise(pos.X, pos.Z);
            float threshold = GetTreeDensityThreshold(biome, subBiome);
            
            if (density > threshold)
            {
                var treeType = GetTreeType(biome, subBiome, pos.Y);
                chunk.AddEntity(new TreeEntity(treeType, pos));
            }
        }
    }
    
    private void PlaceUndergroundOres(Chunk chunk, BiomeType biome)
    {
        foreach (var oreDef in OreDefinitions.All)
        {
            // Check depth range
            if (!chunk.DepthRange.Overlaps(oreDef.DepthRange))
                continue;
                
            // Sample ore noise
            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int y = chunk.MinY; y < chunk.MaxY; y++)
                {
                    for (int z = 0; z < Chunk.Depth; z++)
                    {
                        float oreNoise = _oreNoise.GetNoise(
                            chunk.WorldX + x, 
                            y, 
                            chunk.WorldZ + z
                        );
                        
                        float threshold = oreDef.Rarity;
                        threshold *= GetBiomeOreModifier(biome, oreDef.Type);
                        threshold *= GetDepthOreModifier(y, oreDef.OptimalDepth);
                        
                        if (oreNoise > threshold)
                        {
                            chunk.SetBlock(x, y, z, BlockType.Ore, oreDef.Type);
                        }
                    }
                }
            }
        }
    }
}
```

### Chunk Generation Entry Point

```csharp
public partial class TerrainGenerator
{
    /// <summary>
    /// Generate complete chunk (called from background thread)
    /// </summary>
    private Chunk GenerateChunk(int chunkX, int chunkZ)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var chunk = new Chunk(chunkX, chunkZ);
            int worldX = chunkX * Chunk.Width;
            int worldZ = chunkZ * Chunk.Depth;
            
            // Sample height and climate at chunk corners for biome selection
            float centerHeight = GetHeight(worldX + Chunk.Width/2, worldZ + Chunk.Depth/2);
            var climate = GetClimate(worldX + Chunk.Width/2, worldZ + Chunk.Depth/2, centerHeight);
            
            chunk.Biome = GetBiome(worldX + Chunk.Width/2, worldZ + Chunk.Depth/2, centerHeight);
            chunk.SubBiome = GetSubBiome(chunk.Biome, centerHeight);
            
            // Generate each column
            for (int x = 0; x < Chunk.Width; x++)
            {
                for (int z = 0; z < Chunk.Depth; z++)
                {
                    GenerateColumn(chunk, x, z, worldX + x, worldZ + z);
                }
            }
            
            // Place resources
            PlaceResources(chunk);
            
            chunk.GenerationTime = stopwatch.ElapsedMilliseconds;
            chunk.IsGenerated = true;
            
            return chunk;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Chunk generation error at ({chunkX}, {chunkZ}): {ex}");
            CallDeferred(nameof(EmitGenerationError), chunkX, chunkZ, ex.Message);
            return null;
        }
    }
    
    private void GenerateColumn(Chunk chunk, int localX, int localZ, float worldX, float worldZ)
    {
        float surfaceHeight = GetHeight(worldX, worldZ);
        int surfaceY = Mathf.FloorToInt(surfaceHeight);
        
        for (int y = chunk.MinY; y <= chunk.MaxY; y++)
        {
            BlockType blockType;
            
            if (y > surfaceY)
            {
                // Air above surface
                blockType = BlockType.Air;
            }
            else if (y == surfaceY)
            {
                // Surface block (biome dependent)
                blockType = GetSurfaceBlock(chunk.Biome, surfaceHeight);
            }
            else if (IsCave(worldX, y, worldZ))
            {
                // Cave (air underground)
                blockType = BlockType.Air;
            }
            else
            {
                // Underground geological layers
                blockType = GetUndergroundBlock(worldX, y, worldZ, surfaceY, chunk.Biome);
            }
            
            chunk.SetBlock(localX, y, localZ, blockType);
        }
    }
}
```

---

## 14.9 Performance Considerations

### Generation Time Budget per Chunk

**Target Budgets**:
| Phase | Target Time | Max Time | Notes |
|-------|-------------|----------|-------|
| Terrain generation | 20ms | 30ms | All 7 layers |
| Mesh generation | 10ms | 15ms | Godot MeshInstance3D |
| Resource placement | 5ms | 10ms | Trees, ores |
| **Total** | **35ms** | **55ms** | Per chunk |

**Time Budget Enforcement**:
```csharp
private Chunk GenerateChunkWithTimeout(int chunkX, int chunkZ)
{
    var stopwatch = Stopwatch.StartNew();
    var chunk = new Chunk(chunkX, chunkZ);
    
    // Terrain generation with periodic timeout checks
    for (int x = 0; x < Chunk.Width && stopwatch.ElapsedMilliseconds < 25; x++)
    {
        for (int z = 0; z < Chunk.Depth; z++)
        {
            GenerateColumn(chunk, x, z);
        }
    }
    
    if (stopwatch.ElapsedMilliseconds > 30)
    {
        GD.PushWarning($"Chunk ({chunkX}, {chunkZ}) generation exceeded budget: {stopwatch.ElapsedMilliseconds}ms");
        _performanceMetrics.RecordBudgetViolation();
    }
    
    return chunk;
}
```

**If Over Budget**:
1. Reduce noise octaves for distant chunks
2. Skip non-essential resource placement
3. Defer to lower-priority queue

### Memory Usage During Generation

**Per-Chunk Memory**:
```
Block data: 16 × 16 × 256 × 2 bytes = 131,072 bytes (128 KB)
Metadata: ~16,384 bytes (16 KB)
Temporary noise buffers: ~64 KB
Mesh data (post-generation): ~256 KB
Total per chunk: ~464 KB
```

**Peak Memory During Generation**:
- 4 chunks actively generating: ~1.8 MB
- Noise buffers: ~512 KB
- Queue overhead: ~256 KB
- **Total generation peak: ~2.5 MB**

**Memory Optimization Strategies**:
1. **Object pooling**: Reuse Chunk objects
2. **Noise buffer reuse**: Single buffer, overwrite for each column
3. **Lazy mesh generation**: Only generate meshes for visible chunks
4. **Block compression**: RLE for homogeneous sections (e.g., deep stone)

### Optimization Strategies

**1. Early-Out Optimization**:
```csharp
private void GenerateColumnOptimized(Chunk chunk, int x, int z, float worldX, float worldZ)
{
    float surfaceHeight = GetHeight(worldX, worldZ);
    int surfaceY = Mathf.FloorToInt(surfaceHeight);
    
    // Quick fill: If column is entirely underground stone
    if (surfaceY < chunk.MinY + 10)
    {
        // Fill with stone, skip noise checks
        FillColumn(chunk, x, z, BlockType.Stone);
        return;
    }
    
    // Otherwise: full generation
    // ... detailed generation
}
```

**2. Noise Caching**:
```csharp
// Cache noise samples per chunk to avoid recalculation
private float[,] _heightCache;

private void PrecomputeChunkNoise(int chunkX, int chunkZ)
{
    _heightCache = new float[Chunk.Width + 2, Chunk.Depth + 2];
    
    for (int x = -1; x <= Chunk.Width; x++)
    {
        for (int z = -1; z <= Chunk.Depth; z++)
        {
            float worldX = (chunkX * Chunk.Width + x);
            float worldZ = (chunkZ * Chunk.Depth + z);
            _heightCache[x + 1, z + 1] = GetHeight(worldX, worldZ);
        }
    }
}
```

**3. LOD for Distant Chunks**:
- Near chunks (0-4): Full generation, high detail
- Mid chunks (5-12): Skip caves, simplified geology
- Far chunks (13+): Heightmap only, no underground

**4. Parallel Generation**:
- Multiple background threads (CPU cores - 1)
- Thread-safe noise sampling
- Lock-free queue for completed chunks

**5. Incremental Generation**:
```csharp
public enum GenerationPhase
{
    Heightmap,      // 2D height only
    Surface,        // Surface blocks
    Underground,    // Geological layers
    Caves,          // 3D cave carving
    Resources,      // Trees, ores
    Complete
}

// Generate phases progressively based on priority
if (chunk.Priority > 0.8f)
    GenerateToPhase(chunk, GenerationPhase.Complete);
else if (chunk.Priority > 0.5f)
    GenerateToPhase(chunk, GenerationPhase.Caves);
else
    GenerateToPhase(chunk, GenerationPhase.Surface);
```

---

## 14.10 Cross-References

### Dependencies
- **Requires**: [01-architecture-overview.md] - World size, biome definitions
- **Informs**: 
  - Session 3 (Core Gameplay) - Mining, building mechanics
  - Session 6 (Prototyping) - Terrain generation prototype validation

### Research Sources
| Section | Research Files | Key Finding |
|---------|---------------|-------------|
| Noise selection | r1-godot-noise-research.md | FastNoiseLite 3-5x faster |
| Performance | r1-performance-budget.md | 50ms generation budget |
| Biomes | 01-architecture-overview.md | 3-biome system |

### Integration Points
- **World system**: Seed storage in PostgreSQL `worlds.settings`
- **Persistence**: Chunk delta storage in `chunk_modifications` table
- **Rendering**: Godot MeshInstance3D generation from chunk data
- **Gameplay**: Resource nodes linked to `resource_nodes` table

---

## 14.11 Success Criteria

**Must Achieve**:
- [ ] Chunk generation <50ms average
- [ ] Deterministic: same seed = identical terrain
- [ ] Seamless chunk boundaries (no gaps)
- [ ] All 3 biomes visually distinct
- [ ] Cave systems navigable
- [ ] Ore distribution feels rewarding

**Should Achieve**:
- [ ] <30ms average generation time
- [ ] Sub-biome transitions visible
- [ ] Memory usage <2MB during generation
- [ ] Background threading stable
- [ ] Delta compression <100 bytes/modified chunk

**Nice to Have**:
- [ ] Dynamic LOD for distant chunks
- [ ] Geological fault lines
- [ ] Underground biome variation
- [ ] River/lake generation

---

**Navigation**: [← Previous: Security & Authentication](12-security-spec.md) | [Index]([AGENTS-READ-FIRST]-index.md) | [Next: Error Handling](11-error-handling.md)
