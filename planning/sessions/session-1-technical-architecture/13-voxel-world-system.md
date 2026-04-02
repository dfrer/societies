# Voxel World System Technical Specification

> **Navigation**: [Index]([AGENTS-READ-FIRST]-index.md) | [Previous: Security Specification](12-security-spec.md)
> 
> **Part of**: [Session 1: Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

**Planning Session**: 1 of 7  
**Status**: 📝 Draft  
**Date Started**: 2026-02-01  
**Date Completed**: TBD

---

## Executive Summary

The voxel world system forms the foundational spatial representation of Societies, enabling the block-based gameplay that underpins resource gathering, construction, and terrain modification. This document establishes the complete technical specification for a 3D block-based world inspired by games like Minecraft and Eco, with specific adaptations for Societies' unique requirements.

Key design decisions validated through analysis: **1m³ block size** matching Eco's real-world scale approach, enabling intuitive spatial reasoning and realistic resource quantities. **16×16 horizontal chunks with 256 vertical blocks** provide optimal memory locality while supporting deep geological strata. The vertical range of **-200 to +56 blocks** emphasizes subterranean exploration and resource extraction over sky-based construction, aligning with Societies' focus on ground-level civilization building and resource management.

All terrain is modifiable by players and AI agents except for the **bedrock layer at Y=-200**, which serves as an absolute world boundary and prevents infinite digging exploits. The system supports an MVP world size of **0.5 km² surface area** (44×44 chunks, ~1936 chunks total), scaling to **2 km² post-MVP** (88×88 chunks, ~7744 chunks total). Block data is stored in a compact **4-byte format**: 2 bytes for block ID (65,536 possible types), 1 byte for metadata (rotation, orientation, state flags), and 1 byte for lighting/state data.

---

## Table of Contents

1. [World Coordinate System](#1-world-coordinate-system)
2. [Block Data Structure](#2-block-data-structure)
3. [Chunk Architecture](#3-chunk-architecture)
4. [World Dimensions](#4-world-dimensions)
5. [Block Types Catalog](#5-block-types-catalog)
6. [Technical Implementation in Godot 4](#6-technical-implementation-in-godot-4)
7. [Integration with Other Systems](#7-integration-with-other-systems)
8. [Performance Budget](#8-performance-budget)

---

## 1. World Coordinate System

### 1.1 Coordinate Systems Overview

Societies uses a **dual coordinate system**: global world coordinates for absolute positioning and chunk-local coordinates for efficient block access within chunks.

#### Global Coordinates (World Space)

- **Range**: X and Z axes limited by world size (see [World Dimensions](#4-world-dimensions))
- **Y axis**: -200 (bedrock) to +56 (world ceiling) = 256 total blocks
- **Origin**: World center at (0, 0, 0), with negative X/Z extending west/south
- **Scale**: 1 unit = 1 meter (matching Eco's real-world scale approach)

#### Chunk-Local Coordinates

- **X**: 0-15 (16 blocks east-west)
- **Y**: 0-255 (256 blocks vertical)
- **Z**: 0-15 (16 blocks north-south)
- **Local to Global Conversion**: `Global = (ChunkCoord × 16) + LocalCoord`

### 1.2 Chunk Indexing Scheme

**Chunk Coordinates**:
- Chunks are addressed by (chunkX, chunkZ) integer pairs
- ChunkX = floor(GlobalX / 16)
- ChunkZ = floor(GlobalZ / 16)
- Example: Block at world (35, 10, -20) → Chunk (2, -2), Local (3, 10, 12)

**Chunk Hashing**:
```csharp
public static long GetChunkHash(int chunkX, int chunkZ)
{
    // Combine two 32-bit integers into one 64-bit hash
    // Handles negative coordinates correctly
    return ((long)chunkX << 32) | (uint)chunkZ;
}
```

**Chunk Storage Map**:
```csharp
public class World
{
    // Primary chunk storage: hash -> chunk data
    private Dictionary<long, VoxelChunk> _chunks = new();
    
    // Active chunks within simulation radius
    private HashSet<long> _activeChunks = new();
}
```

### 1.3 World Bounds and Limits

**Hard Limits** (enforced by server):

| World Size | X Range | Z Range | Chunk Range | Total Chunks |
|------------|---------|---------|-------------|--------------|
| **MVP** (0.5 km²) | -352 to +351 | -352 to +351 | -22 to +21 | 1,936 chunks |
| **Post-MVP** (2 km²) | -704 to +703 | -704 to +703 | -44 to +43 | 7,744 chunks |

**Vertical Limits**:
- **Y = -200**: Bedrock layer (unmodifiable, server-enforced)
- **Y = -199 to +55**: Modifiable terrain
- **Y = +56**: World ceiling (air above, no building allowed)

**Boundary Enforcement**:
```csharp
public bool IsValidBlockPosition(int x, int y, int z)
{
    // Check vertical bounds (always enforced)
    if (y < -200 || y > 56) return false;
    
    // Check horizontal bounds (world-size dependent)
    int worldRadius = GetWorldRadiusChunks();
    int minChunk = -worldRadius;
    int maxChunk = worldRadius - 1;
    
    int chunkX = Mathf.FloorToInt(x / 16f);
    int chunkZ = Mathf.FloorToInt(z / 16f);
    
    return chunkX >= minChunk && chunkX <= maxChunk &&
           chunkZ >= minChunk && chunkZ <= maxChunk;
}
```

---

## 2. Block Data Structure

### 2.1 Block Format: 4 Bytes Per Block

Each block in the world is represented by exactly **4 bytes** (32 bits), enabling memory-efficient storage while supporting rich block variety:

```
Bit Layout (32 bits total):
[15:0]  Block ID (16 bits) - 65,536 possible block types
[23:16] Metadata (8 bits) - rotation, state, orientation
[31:24] Light/State (8 bits) - light level, special flags
```

**C# Implementation**:
```csharp
public struct BlockData : IEquatable<BlockData>
{
    private uint _data;
    
    // Block ID: bits 0-15
    public ushort BlockId 
    { 
        get => (ushort)(_data & 0xFFFF);
        set => _data = (_data & 0xFFFF0000) | value;
    }
    
    // Metadata: bits 16-23
    public byte Metadata 
    { 
        get => (byte)((_data >> 16) & 0xFF);
        set => _data = (_data & 0xFF00FFFF) | ((uint)value << 16);
    }
    
    // Light/State: bits 24-31
    public byte LightAndState 
    { 
        get => (byte)((_data >> 24) & 0xFF);
        set => _data = (_data & 0x00FFFFFF) | ((uint)value << 24);
    }
    
    // Packed access for network serialization
    public uint PackedData => _data;
    
    public BlockData(ushort blockId, byte metadata = 0, byte lightState = 0)
    {
        _data = (uint)blockId | ((uint)metadata << 16) | ((uint)lightState << 24);
    }
    
    public bool Equals(BlockData other) => _data == other._data;
    public override bool Equals(object obj) => obj is BlockData other && Equals(other);
    public override int GetHashCode() => (int)_data;
    public static bool operator ==(BlockData left, BlockData right) => left.Equals(right);
    public static bool operator !=(BlockData left, BlockData right) => !left.Equals(right);
}
```

### 2.2 Block Registry System

**Block Definition**:
```csharp
public class BlockDefinition
{
    public ushort Id { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public BlockCategory Category { get; }
    
    // Physical properties
    public bool IsSolid { get; }
    public bool IsOpaque { get; }
    public bool IsReplaceable { get; }
    public float Hardness { get; }      // Mining difficulty
    public float BlastResistance { get; }
    
    // Gameplay properties
    public bool IsGatherable { get; }
    public bool IsBuildable { get; }
    public bool IsBedrock { get; }      // Unmodifiable if true
    
    // State properties (like Minecraft's BlockState)
    public IReadOnlyList<BlockStateProperty> StateProperties { get; }
    
    // Light emission
    public byte LightEmission { get; }  // 0-15
    
    public BlockDefinition(ushort id, string name, BlockProperties properties)
    {
        Id = id;
        Name = name;
        // ... initialize all properties
    }
}
```

**Block Registry**:
```csharp
public static class BlockRegistry
{
    private static readonly Dictionary<ushort, BlockDefinition> _blocks = new();
    private static readonly Dictionary<string, ushort> _nameToId = new();
    
    public static void Register(BlockDefinition block)
    {
        _blocks[block.Id] = block;
        _nameToId[block.Name] = block.Id;
    }
    
    public static BlockDefinition Get(ushort id) => _blocks.TryGetValue(id, out var block) ? block : null;
    public static BlockDefinition Get(string name) => _nameToId.TryGetValue(name, out var id) ? Get(id) : null;
    
    // Predefined block accessors (set during registration)
    public static BlockDefinition Air { get; private set; }
    public static BlockDefinition Bedrock { get; private set; }
    public static BlockDefinition Stone { get; private set; }
    // ... etc
}
```

### 2.3 Block State Properties

Similar to Minecraft's BlockState system, blocks can have state properties that modify behavior and appearance without requiring unique IDs:

**State Property Types**:

| Property Type | Values | Example Blocks |
|---------------|--------|----------------|
| **Orientation** | North, South, East, West | Stairs, logs, furniture |
| **Rotation** | 0°, 90°, 180°, 270° | Decorative blocks |
| **GrowthStage** | 0-7 (8 stages) | Crops, saplings |
| **Open** | true, false | Doors, gates |
| **Lit** | true, false | Furnaces, torches |
| **Powered** | true, false | Mechanical blocks |
| **Half** | Top, Bottom | Slabs, stairs |
| **Variant** | 0-3 (4 sub-types) | Wood types, stone types |

**State Encoding in Metadata Byte**:
```csharp
public static class BlockStateEncoding
{
    // Metadata byte layout (8 bits):
    // [7:6] Reserved for future use
    // [5:4] Variant (0-3)
    // [3]   Boolean flag (open/lit/powered)
    // [2:0] Orientation/Stage (0-7)
    
    public static byte PackState(BlockOrientation orientation, int variant = 0, bool flag = false)
    {
        byte metadata = (byte)((int)orientation & 0x07);           // bits 0-2
        metadata |= (byte)((variant & 0x03) << 4);                  // bits 4-5
        if (flag) metadata |= 0x08;                                 // bit 3
        return metadata;
    }
    
    public static BlockOrientation GetOrientation(byte metadata) => (BlockOrientation)(metadata & 0x07);
    public static int GetVariant(byte metadata) => (metadata >> 4) & 0x03;
    public static bool GetFlag(byte metadata) => (metadata & 0x08) != 0;
}

public enum BlockOrientation : byte
{
    North = 0,
    South = 1,
    East = 2,
    West = 3,
    Up = 4,
    Down = 5,
    None = 7
}
```

### 2.4 Bedrock Layer Specification

The bedrock layer serves as the immutable foundation of the world:

**Properties**:
- **Layer**: Y = -200 (single block layer)
- **Block ID**: 1 (reserved, see [Block Types Catalog](#5-block-types-catalog))
- **Modifiable**: **NEVER** - server rejects all modification attempts
- **Purpose**: World boundary, exploit prevention, performance optimization

**Server Enforcement**:
```csharp
public class BlockModificationValidator
{
    public ValidationResult ValidateBlockChange(
        World world, 
        int x, int y, int z, 
        BlockData newBlock, 
        Entity actor)
    {
        // Absolute protection for bedrock layer
        if (y == -200)
        {
            return ValidationResult.Rejected("Bedrock layer cannot be modified");
        }
        
        // Check if new block is bedrock (attempted spawn/hack)
        if (newBlock.BlockId == BlockRegistry.Bedrock.Id)
        {
            return ValidationResult.Rejected("Bedrock can only exist at world generation");
        }
        
        // Additional validation: ownership, permissions, etc.
        // ... jurisdiction checks, claim ownership, etc.
        
        return ValidationResult.Approved();
    }
}
```

---

## 3. Chunk Architecture

### 3.1 Chunk Dimensions

**Chunk Specification**:

| Dimension | Size | Notes |
|-----------|------|-------|
| **Horizontal (X)** | 16 blocks | Standard chunk width |
| **Horizontal (Z)** | 16 blocks | Standard chunk depth |
| **Vertical (Y)** | 256 blocks | Full world height |
| **Total Blocks** | 65,536 | 16 × 16 × 256 |
| **Memory (raw)** | 256 KB | 65,536 blocks × 4 bytes |

**Design Rationale**:
- **16×16 horizontal**: Optimal balance between cache locality and granularity
- **256 vertical**: Single chunk spans entire world height, simplifying vertical lookups
- **No sub-chunks**: Unlike Minecraft's 16-block sections, we use unified vertical storage for simplicity in a smaller world

### 3.2 Chunk Data Storage Format

**VoxelChunk Class Structure**:
```csharp
public class VoxelChunk
{
    // Chunk coordinates in world
    public int ChunkX { get; }
    public int ChunkZ { get; }
    
    // Primary block data: contiguous 4-byte array
    // Index = (y * 16 + z) * 16 + x = (y << 8) + (z << 4) + x
    private BlockData[] _blocks;
    
    // Chunk state
    public ChunkState State { get; private set; }
    public bool IsModified { get; private set; }
    public long LastAccessedTick { get; set; }
    
    // Cached mesh data (client-side)
    public Mesh ChunkMesh { get; set; }
    public bool MeshDirty { get; set; } = true;
    
    // Modified blocks since last save (for delta persistence)
    private HashSet<int> _modifiedBlockIndices = new();
    
    public VoxelChunk(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        _blocks = new BlockData[65536]; // 16×16×256
        State = ChunkState.Empty;
    }
    
    // Fast block access
    public BlockData GetBlock(int x, int y, int z)
    {
        int index = (y << 8) | (z << 4) | x;
        return _blocks[index];
    }
    
    public void SetBlock(int x, int y, int z, BlockData block)
    {
        int index = (y << 8) | (z << 4) | x;
        _blocks[index] = block;
        IsModified = true;
        MeshDirty = true;
        _modifiedBlockIndices.Add(index);
    }
    
    // Bulk operations for world generation
    public void FillLayer(int y, BlockData block)
    {
        int baseIndex = y << 8;
        for (int i = 0; i < 256; i++)
        {
            _blocks[baseIndex + i] = block;
        }
        IsModified = true;
        MeshDirty = true;
    }
}

public enum ChunkState
{
    Empty,          // Uninitialized
    Generating,     // World generation in progress
    Generated,      // Blocks assigned, not yet meshed
    Active,         // Fully loaded and simulated
    Saving,         // Persistence in progress
    Unloading       // Ready for removal
}
```

### 3.3 Chunk Lifecycle

**State Transitions**:
```
Empty → Generating → Generated → Active → Saving → Unloaded
                ↓           ↓         ↑
                └─ (if modified) ←────┘
```

**Chunk Loading Pipeline**:
```csharp
public class ChunkManager
{
    private Dictionary<long, VoxelChunk> _chunks = new();
    private Queue<VoxelChunk> _generationQueue = new();
    
    public async Task<VoxelChunk> LoadChunkAsync(int chunkX, int chunkZ)
    {
        long hash = GetChunkHash(chunkX, chunkZ);
        
        // Check if already loaded
        if (_chunks.TryGetValue(hash, out var existing))
        {
            existing.LastAccessedTick = CurrentTick;
            return existing;
        }
        
        // Try to load from persistence
        var chunk = await _persistence.LoadChunkAsync(chunkX, chunkZ);
        
        if (chunk == null)
        {
            // Generate new chunk
            chunk = new VoxelChunk(chunkX, chunkZ);
            _generationQueue.Enqueue(chunk);
        }
        
        _chunks[hash] = chunk;
        return chunk;
    }
    
    public void UnloadDistantChunks(Vector3 playerPosition, float unloadRadius)
    {
        var playerChunkX = Mathf.FloorToInt(playerPosition.X / 16f);
        var playerChunkZ = Mathf.FloorToInt(playerPosition.Z / 16f);
        
        foreach (var kvp in _chunks.ToList())
        {
            var chunk = kvp.Value;
            var dx = chunk.ChunkX - playerChunkX;
            var dz = chunk.ChunkZ - playerChunkZ;
            var dist = Mathf.Sqrt(dx * dx + dz * dz);
            
            if (dist > unloadRadius)
            {
                // Save if modified
                if (chunk.IsModified)
                {
                    _persistence.SaveChunkAsync(chunk).ConfigureAwait(false);
                }
                
                _chunks.Remove(kvp.Key);
            }
        }
    }
}
```

### 3.4 Chunk Loading Radius

**Simulation vs. Render Distances**:

| Type | MVP Radius | Post-MVP | Purpose |
|------|------------|----------|---------|
| **Simulation** | 5 chunks | 8 chunks | Block updates, physics, AI pathfinding |
| **Render** | 8 chunks | 16 chunks | Visual mesh rendering |
| **Unload** | 10 chunks | 20 chunks | Memory cleanup threshold |

**Server-Side Chunk Management**:
```csharp
public class ServerChunkConfig
{
    // Simulation radius: chunks where blocks update (water flow, crop growth)
    public const int SimulationRadius = 5;  // 11×11 = 121 chunks
    
    // AI pathfinding radius: chunks agents can navigate
    public const int PathfindingRadius = 7; // 15×15 = 225 chunks
    
    // Maximum loaded chunks per player (for memory budgeting)
    public const int MaxChunksPerPlayer = 400; // ~100 MB at 256 KB/chunk
}
```

---

## 4. World Dimensions

### 4.1 MVP World Size (0.5 km²)

**Specifications**:

| Metric | Value | Calculation |
|--------|-------|-------------|
| **Surface Area** | 0.5 km² | 704m × 704m |
| **Chunk Grid** | 44×44 | 704m / 16m per chunk |
| **Total Chunks** | 1,936 | 44 × 44 |
| **Memory (all loaded)** | ~500 MB | 1,936 × 256 KB |
| **Horizontal Range** | ±352m | -352 to +351 |
| **Vertical Range** | -200 to +56 | 256 blocks |

**Geographic Features**:
- **Maximum Elevation**: ~56m above sea level (Y=56 is ceiling)
- **Sea Level**: Y=0 (convention)
- **Minimum Elevation**: -200m (bedrock)
- **Usable Depth**: 200m below surface for mining/geology

### 4.2 Post-MVP World Size (2 km²)

**Specifications**:

| Metric | Value | Calculation |
|--------|-------|-------------|
| **Surface Area** | 2 km² | 1,408m × 1,408m |
| **Chunk Grid** | 88×88 | 1,408m / 16m per chunk |
| **Total Chunks** | 7,744 | 88 × 88 |
| **Memory (all loaded)** | ~2 GB | 7,744 × 256 KB |
| **Horizontal Range** | ±704m | -704 to +703 |

### 4.3 Geological Strata Distribution

The vertical world is organized into distinct geological layers:

| Layer | Y Range | Thickness | Primary Blocks | Purpose |
|-------|---------|-----------|----------------|---------|
| **Surface** | +40 to +56 | 16 blocks | Air (empty) | Building space, limited |
| **Topsoil** | +30 to +40 | 10 blocks | Grass, dirt, farmland | Agriculture, building |
| **Subsurface** | +10 to +30 | 20 blocks | Dirt, sand, gravel | Shallow resources |
| **Stone** | -50 to +10 | 60 blocks | Stone, coal ore | Basic mining |
| **Deep Stone** | -150 to -50 | 100 blocks | Stone, iron ore, copper | Intermediate mining |
| **Bedrock Layer** | -200 to -150 | 50 blocks | Deepslate, rare ores, bedrock | Deep mining, bedrock barrier |
| **Bedrock** | -200 only | 1 block | Bedrock (unmodifiable) | World boundary |

**Ore Distribution** (typical heights):

| Ore | Y Range | Peak Abundance | Notes |
|-----|---------|----------------|-------|
| **Coal** | +10 to -50 | -20 | Common, surface-accessible |
| **Iron** | -10 to -80 | -40 | Requires moderate digging |
| **Copper** | -20 to -90 | -50 | Industrial metals |
| **Gold** | -60 to -120 | -80 | Deep mining required |
| **Gems** | -100 to -180 | -150 | Rare, deep strata only |

---

## 5. Block Types Catalog

### 5.1 Block ID Allocation

**ID Ranges**:

| Range | Purpose | Count |
|-------|---------|-------|
| **0** | Air (empty space) | 1 |
| **1** | Bedrock | 1 |
| **2-49** | Core terrain blocks | 48 |
| **50-99** | Ore and mineral blocks | 50 |
| **100-149** | Organic/nature blocks | 50 |
| **150-199** | Building/decorative blocks | 50 |
| **200-255** | Special/technical blocks | 56 |
| **256-65535** | Reserved for modding/expansion | 65,280 |

### 5.2 Core Terrain Blocks (IDs 2-49)

| ID | Name | Category | Properties | Notes |
|----|------|----------|------------|-------|
| **0** | Air | Technical | Non-solid, transparent | Empty space |
| **1** | Bedrock | Technical | Unmodifiable, solid | World boundary |
| **2** | Stone | Terrain | Solid, opaque, hardness=2 | Most common underground |
| **3** | Dirt | Terrain | Solid, opaque, hardness=0.5 | Soil layer |
| **4** | Grass | Terrain | Solid, opaque, hardness=0.6 | Topsoil with vegetation |
| **5** | Sand | Terrain | Solid, opaque, gravity-affected | Beaches, deserts |
| **6** | Gravel | Terrain | Solid, opaque, gravity-affected | Loose sediment |
| **7** | Clay | Terrain | Solid, opaque, hardness=0.6 | Wet areas, ceramics |
| **8** | Deepslate | Terrain | Solid, opaque, hardness=3 | Deep layer stone |
| **9** | Granite | Terrain | Solid, opaque, hardness=2.5 | Igneous rock variant |
| **10** | Diorite | Terrain | Solid, opaque, hardness=2.5 | Igneous rock variant |
| **11** | Andesite | Terrain | Solid, opaque, hardness=2.5 | Igneous rock variant |
| **12** | Sandstone | Terrain | Solid, opaque, hardness=1.2 | Desert bedrock |
| **13** | Red Sand | Terrain | Solid, opaque, gravity-affected | Desert variant |
| **14** | Red Sandstone | Terrain | Solid, opaque, hardness=1.2 | Desert bedrock variant |
| **15** | Calcite | Terrain | Solid, opaque, hardness=1.5 | Limestone variant |
| **16** | Tuff | Terrain | Solid, opaque, hardness=2 | Volcanic rock |
| **17-19** | *Reserved* | | | Future terrain variants |

### 5.3 Ore and Mineral Blocks (IDs 50-99)

| ID | Name | Category | Properties | Y Range | Notes |
|----|------|----------|------------|---------|-------|
| **50** | Coal Ore | Ore | Drops coal, hardness=2 | +10 to -50 | Fuel source |
| **51** | Iron Ore | Ore | Drops iron, hardness=3 | -10 to -80 | Basic metal |
| **52** | Copper Ore | Ore | Drops copper, hardness=3 | -20 to -90 | Industrial metal |
| **53** | Gold Ore | Ore | Drops gold, hardness=3 | -60 to -120 | Currency/precious |
| **54** | Gem Ore | Ore | Drops gems, hardness=4 | -100 to -180 | Rare, valuable |
| **55** | Deepslate Coal | Ore | Drops coal, hardness=4 | -150 to -50 | Deep variant |
| **56** | Deepslate Iron | Ore | Drops iron, hardness=5 | -150 to -80 | Deep variant |
| **57** | Deepslate Copper | Ore | Drops copper, hardness=5 | -150 to -90 | Deep variant |
| **58** | Deepslate Gold | Ore | Drops gold, hardness=5 | -150 to -120 | Deep variant |
| **59** | Deepslate Gem | Ore | Drops gems, hardness=6 | -180 to -150 | Deep variant |
| **60** | Stone Variant A | Ore | Decorative stone | All | Building material |
| **61** | Stone Variant B | Ore | Decorative stone | All | Building material |
| **62** | Mineral Deposit | Ore | Generic mineral | All | Placeholder ores |
| **63-69** | *Reserved* | | | | Future ores |

### 5.4 Organic and Nature Blocks (IDs 100-149)

| ID | Name | Category | Properties | Notes |
|----|------|----------|------------|-------|
| **100** | Oak Log | Organic | Solid, rotatable, hardness=1.5 | Tree trunk |
| **101** | Oak Leaves | Organic | Non-solid (transparent), opacity=0.5 | Tree canopy |
| **102** | Oak Planks | Organic | Solid, hardness=1.5 | Processed wood |
| **103** | Pine Log | Organic | Solid, rotatable, hardness=1.5 | Conifer variant |
| **104** | Pine Leaves | Organic | Non-solid, opacity=0.5 | Conifer canopy |
| **105** | Pine Planks | Organic | Solid, hardness=1.5 | Processed conifer |
| **106** | Hardwood Log | Organic | Solid, rotatable, hardness=2 | Dense wood |
| **107** | Hardwood Leaves | Organic | Non-solid, opacity=0.5 | Dense canopy |
| **108** | Hardwood Planks | Organic | Solid, hardness=2 | Premium wood |
| **109** | Sapling Oak | Organic | Non-solid, replaceable | Grows into tree |
| **110** | Sapling Pine | Organic | Non-solid, replaceable | Grows into tree |
| **111** | Tall Grass | Organic | Non-solid, replaceable | Ground cover |
| **112** | Fern | Organic | Non-solid, replaceable | Forest floor |
| **113** | Cactus | Organic | Solid, hurts on touch | Desert plant |
| **114** | Reeds | Organic | Non-solid, grows in water | Wetland plant |
| **115** | Mushroom Brown | Organic | Non-solid, edible | Forest fungi |
| **116** | Mushroom Red | Organic | Non-solid, dangerous | Forest fungi |
| **117** | Crop Wheat | Organic | Non-solid, 8 growth stages | Agriculture |
| **118** | Crop Vegetables | Organic | Non-solid, 8 growth stages | Agriculture |
| **119-124** | *Reserved* | | | Future crops |
| **125** | Water | Fluid | Non-solid, flowing | Natural water |
| **126** | Water Source | Fluid | Non-solid, infinite | Spring source |
| **127** | Ice | Organic | Solid, slippery | Frozen water |
| **128** | Snow | Organic | Non-solid, layerable | Snow cover |
| **129** | Snow Block | Organic | Solid, soft | Compacted snow |
| **130-149** | *Reserved* | | | Future organic blocks |

### 5.5 Building and Decorative Blocks (IDs 150-199)

| ID | Name | Category | Properties | Notes |
|----|------|----------|------------|-------|
| **150** | Cobblestone | Building | Solid, rough texture | Basic building |
| **151** | Stone Bricks | Building | Solid, refined | Masonry |
| **152** | Stone Brick Variant A | Building | Solid, decorative | Patterned |
| **153** | Stone Brick Variant B | Building | Solid, decorative | Patterned |
| **154** | Stone Brick Variant C | Building | Solid, decorative | Weathered |
| **155** | Smooth Stone | Building | Solid, polished | Refined stone |
| **156** | Stone Slab | Building | Half-height | Stairs, platforms |
| **157** | Stone Stairs | Building | Directional | Staircases |
| **158** | Wood Slab | Building | Half-height, wooden | Wooden platforms |
| **159** | Wood Stairs | Building | Directional, wooden | Wooden staircases |
| **160** | Wood Door | Building | Directional, openable | Entry |
| **161** | Wood Trapdoor | Building | Directional, openable | Ceiling/floor hatch |
| **162** | Wood Fence | Building | Partial solid | Barriers |
| **163** | Wood Gate | Building | Directional, openable | Fence entry |
| **164** | Glass | Building | Transparent, solid | Windows |
| **165** | Glass Pane | Building | Thin, transparent | Window panes |
| **166** | Torch | Building | Non-solid, emits light | Lighting |
| **167** | Lantern | Building | Non-solid, emits light | Decorative light |
| **168** | Carpet | Building | Thin layer, decorative | Flooring |
| **169** | Flower Pot | Building | Small container | Decoration |
| **170** | Chest | Building | Solid, 27 slots storage | Storage |
| **171** | Crafting Table | Building | Solid, crafting UI | Crafting station |
| **172** | Furnace | Building | Solid, lit/unlit states | Smelting |
| **173** | Bed | Building | Directional, sleep | Sleeping |
| **174-199** | *Reserved* | | | Future building blocks |

### 5.6 Special/Technical Blocks (IDs 200-255)

| ID | Name | Category | Properties | Notes |
|----|------|----------|------------|-------|
| **200** | Barrier | Technical | Invisible, solid | Admin boundaries |
| **201** | Command Block | Technical | Executes scripts | Admin tools |
| **202** | Structure Void | Technical | Invisible, non-solid | Construction aid |
| **203-255** | *Reserved* | | | Future technical use |

---

## 6. Technical Implementation in Godot 4

### 6.1 C# Data Structures

**Core Voxel Types**:
```csharp
// Core namespace: Societies.Voxel
namespace Societies.Voxel
{
    /// <summary>
    /// Compact 4-byte block representation
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct BlockData : IEquatable<BlockData>
    {
        private readonly uint _packed;
        
        public ushort BlockId => (ushort)(_packed & 0xFFFF);
        public byte Metadata => (byte)((_packed >> 16) & 0xFF);
        public byte LightAndState => (byte)((_packed >> 24) & 0xFF);
        public uint Packed => _packed;
        
        public BlockData(ushort id, byte metadata = 0, byte lightState = 0)
        {
            _packed = (uint)id | ((uint)metadata << 16) | ((uint)lightState << 24);
        }
        
        public bool IsAir => BlockId == 0;
        public bool IsBedrock => BlockId == 1;
        
        public bool Equals(BlockData other) => _packed == other._packed;
        public override bool Equals(object obj) => obj is BlockData other && Equals(other);
        public override int GetHashCode() => (int)_packed;
        public static bool operator ==(BlockData left, BlockData right) => left.Equals(right);
        public static bool operator !=(BlockData left, BlockData right) => !left.Equals(right);
    }
    
    /// <summary>
    /// Block position in world coordinates
    /// </summary>
    public readonly struct BlockPosition : IEquatable<BlockPosition>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        
        public BlockPosition(int x, int y, int z)
        {
            X = x; Y = y; Z = z;
        }
        
        public int ChunkX => Mathf.FloorToInt(X / 16f);
        public int ChunkZ => Mathf.FloorToInt(Z / 16f);
        public int LocalX => ((X % 16) + 16) % 16;  // Handle negatives
        public int LocalY => Y + 200;  // Shift to 0-255 range
        public int LocalZ => ((Z % 16) + 16) % 16;
        
        public bool Equals(BlockPosition other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is BlockPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        
        public static BlockPosition operator +(BlockPosition a, BlockPosition b) =>
            new BlockPosition(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
}
```

### 6.2 VoxelChunk Class Structure

**Server-Side Implementation**:
```csharp
namespace Societies.Voxel
{
    /// <summary>
    /// Server-side chunk data container
    /// </summary>
    public class VoxelChunk
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 256;
        public const int TotalBlocks = Width * Depth * Height; // 65,536
        public const int MemorySize = TotalBlocks * 4; // 256 KB
        
        // Chunk coordinates
        public int ChunkX { get; }
        public int ChunkZ { get; }
        
        // Block data (contiguous array for cache efficiency)
        private readonly BlockData[] _blocks;
        
        // Modification tracking
        private readonly HashSet<int> _modifiedIndices = new();
        private long _lastModifiedTick;
        
        // State
        public ChunkState State { get; private set; }
        public bool IsModified => _modifiedIndices.Count > 0;
        public long LastAccessedTick { get; set; }
        
        // Neighbor references (for edge lookups)
        public VoxelChunk NeighborNorth { get; set; }
        public VoxelChunk NeighborSouth { get; set; }
        public VoxelChunk NeighborEast { get; set; }
        public VoxelChunk NeighborWest { get; set; }
        
        public VoxelChunk(int chunkX, int chunkZ)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            _blocks = new BlockData[TotalBlocks];
            State = ChunkState.Empty;
        }
        
        /// <summary>
        /// Fast block lookup without bounds checking (internal use)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockData GetBlockFast(int localX, int localY, int localZ)
        {
            int index = (localY << 8) | (localZ << 4) | localX;
            return _blocks[index];
        }
        
        /// <summary>
        /// Safe block lookup with bounds checking
        /// </summary>
        public BlockData GetBlock(int localX, int localY, int localZ)
        {
            if ((uint)localX >= Width || (uint)localY >= Height || (uint)localZ >= Depth)
                return new BlockData(0); // Return air for out of bounds
            
            return GetBlockFast(localX, localY, localZ);
        }
        
        /// <summary>
        /// Set block with modification tracking
        /// </summary>
        public void SetBlock(int localX, int localY, int localZ, BlockData block)
        {
            if ((uint)localX >= Width || (uint)localY >= Height || (uint)localZ >= Depth)
                throw new ArgumentOutOfRangeException("Block coordinates out of chunk bounds");
            
            int index = (localY << 8) | (localZ << 4) | localX;
            _blocks[index] = block;
            _modifiedIndices.Add(index);
            _lastModifiedTick = GameServer.CurrentTick;
        }
        
        /// <summary>
        /// Get all modified blocks since last save
        /// </summary>
        public IEnumerable<(int index, BlockData block)> GetModifications()
        {
            foreach (int index in _modifiedIndices)
            {
                yield return (index, _blocks[index]);
            }
        }
        
        /// <summary>
        /// Clear modification tracking after save
        /// </summary>
        public void ClearModifications()
        {
            _modifiedIndices.Clear();
        }
        
        /// <summary>
        /// Serialize chunk to byte array for persistence
        /// </summary>
        public byte[] Serialize()
        {
            // Simple raw serialization: 256 KB
            byte[] result = new byte[MemorySize];
            Buffer.BlockCopy(_blocks, 0, result, 0, MemorySize);
            return result;
        }
        
        /// <summary>
        /// Deserialize from byte array
        /// </summary>
        public static VoxelChunk Deserialize(int chunkX, int chunkZ, byte[] data)
        {
            if (data.Length != MemorySize)
                throw new ArgumentException($"Invalid chunk data size: {data.Length}");
            
            var chunk = new VoxelChunk(chunkX, chunkZ);
            Buffer.BlockCopy(data, 0, chunk._blocks, 0, MemorySize);
            chunk.State = ChunkState.Generated;
            return chunk;
        }
        
        /// <summary>
        /// Generate chunk using world generator
        /// </summary>
        public void Generate(WorldGenerator generator)
        {
            State = ChunkState.Generating;
            generator.GenerateChunk(this);
            State = ChunkState.Generated;
        }
    }
    
    public enum ChunkState
    {
        Empty,
        Generating,
        Generated,
        Active,
        Saving,
        Unloading
    }
}
```

### 6.3 Memory Layout and Optimization

**Memory Budget per Chunk**:

| Component | Size | Purpose |
|-----------|------|---------|
| **Block Data** | 256 KB | Primary storage (65,536 × 4 bytes) |
| **Modified Index Set** | ~2-8 KB | HashSet of modified blocks (variable) |
| **Object Overhead** | ~64 bytes | C# object headers, references |
| **Total (typical)** | **~260 KB** | Per loaded chunk |

**Optimizations**:

```csharp
public class MemoryOptimization
{
    // 1. Contiguous array layout for cache efficiency
    // _blocks[] is a flat array, not jagged or 3D
    
    // 2. Index calculation optimized with bit shifts
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBlockIndex(int x, int y, int z) => (y << 8) | (z << 4) | x;
    // Equivalent to: y * 256 + z * 16 + x
    // But: y << 8 is faster than y * 256
    
    // 3. Block pooling for frequent modifications
    private static ArrayPool<BlockData> _blockPool = ArrayPool<BlockData>.Shared;
    
    // 4. Lazy initialization for empty chunks
    public static VoxelChunk CreateEmpty(int x, int z)
    {
        // Don't allocate _blocks[] until actually needed
        return new VoxelChunk(x, z, allocateData: false);
    }
    
    // 5. Memory-mapped files for large worlds
    public class MemoryMappedChunkStorage
    {
        private MemoryMappedFile _mmap;
        
        public BlockData GetBlockDirect(int chunkX, int chunkZ, int localX, int localY, int localZ)
        {
            long offset = CalculateFileOffset(chunkX, chunkZ, localX, localY, localZ);
            // Read directly from memory-mapped file without loading full chunk
            return ReadBlockAtOffset(offset);
        }
    }
}
```

### 6.4 Thread Safety Considerations

**Chunk Access Patterns**:
```csharp
public class ThreadSafeChunkManager
{
    private readonly ConcurrentDictionary<long, VoxelChunk> _chunks = new();
    private readonly ReaderWriterLockSlim _worldLock = new();
    
    // Thread-safe chunk retrieval
    public bool TryGetChunk(int chunkX, int chunkZ, out VoxelChunk chunk)
    {
        long hash = GetChunkHash(chunkX, chunkZ);
        return _chunks.TryGetValue(hash, out chunk);
    }
    
    // Read-heavy operation: use read lock
    public BlockData GetBlockSafe(int worldX, int worldY, int worldZ)
    {
        var pos = new BlockPosition(worldX, worldY, worldZ);
        
        if (!TryGetChunk(pos.ChunkX, pos.ChunkZ, out var chunk))
            return new BlockData(0); // Air
        
        // Chunk-level lock for thread safety
        lock (chunk)
        {
            return chunk.GetBlock(pos.LocalX, pos.LocalY, pos.LocalZ);
        }
    }
    
    // Write operation: use write lock
    public void SetBlockSafe(int worldX, int worldY, int worldZ, BlockData block, Entity actor)
    {
        var pos = new BlockPosition(worldX, worldY, worldZ);
        
        _worldLock.EnterWriteLock();
        try
        {
            if (!TryGetChunk(pos.ChunkX, pos.ChunkZ, out var chunk))
                return; // Or load chunk
            
            lock (chunk)
            {
                // Validate modification
                if (worldY == -200)
                    throw new InvalidOperationException("Cannot modify bedrock");
                
                // Check permissions, ownership, etc.
                if (!CanModifyBlock(pos, actor))
                    return;
                
                chunk.SetBlock(pos.LocalX, pos.LocalY, pos.LocalZ, block);
                
                // Notify neighbors (for updates like water flow)
                NotifyBlockChanged(pos, block);
            }
        }
        finally
        {
            _worldLock.ExitWriteLock();
        }
    }
    
    // Async chunk loading (non-blocking)
    public async Task<VoxelChunk> LoadChunkAsync(int chunkX, int chunkZ)
    {
        long hash = GetChunkHash(chunkX, chunkZ);
        
        // Fast path: already loaded
        if (_chunks.TryGetValue(hash, out var existing))
        {
            Interlocked.Exchange(ref existing.LastAccessedTick, GameServer.CurrentTick);
            return existing;
        }
        
        // Load from persistence (async I/O)
        var chunk = await _persistence.LoadChunkAsync(chunkX, chunkZ);
        
        if (chunk == null)
        {
            // Generate new chunk
            chunk = new VoxelChunk(chunkX, chunkZ);
            await Task.Run(() => _generator.GenerateChunk(chunk));
        }
        
        // Add to dictionary (thread-safe)
        _chunks.TryAdd(hash, chunk);
        return chunk;
    }
}
```

---

## 7. Integration with Other Systems

### 7.1 AI Pathfinding on Voxels

**Pathfinding Interface**:
```csharp
public class VoxelPathfindingAdapter : IPathfindingGraph
{
    private readonly World _world;
    
    public bool IsWalkable(Vector3 worldPos)
    {
        var pos = new BlockPosition((int)worldPos.X, (int)worldPos.Y, (int)worldPos.Z);
        
        // Get block at position
        var block = _world.GetBlock(pos.X, pos.Y, pos.Z);
        var blockDef = BlockRegistry.Get(block.BlockId);
        
        // Must be solid to stand on
        if (!blockDef.IsSolid)
            return false;
        
        // Check if there's headroom above (1.8m = 2 blocks)
        var blockAbove = _world.GetBlock(pos.X, pos.Y + 1, pos.Z);
        var blockAbove2 = _world.GetBlock(pos.X, pos.Y + 2, pos.Z);
        
        // Must have at least 2 empty blocks above for agent height
        return blockAbove.IsAir && blockAbove2.IsAir;
    }
    
    public float GetMovementCost(Vector3 from, Vector3 to)
    {
        var blockTo = _world.GetBlock((int)to.X, (int)to.Y, (int)to.Z);
        var def = BlockRegistry.Get(blockTo.BlockId);
        
        // Different terrain types have different costs
        return def.Category switch
        {
            BlockCategory.Water => 10.0f,    // Slow wading
            BlockCategory.Sand => 1.5f,      // Slower on sand
            BlockCategory.Organic => 1.2f,   // Slightly slower in vegetation
            _ => 1.0f                          // Normal on stone/dirt
        };
    }
    
    public IEnumerable<Vector3> GetNeighbors(Vector3 pos)
    {
        // Standard 8-directional movement with height changes
        int x = (int)pos.X;
        int y = (int)pos.Y;
        int z = (int)pos.Z;
        
        // Check all 8 horizontal neighbors
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                
                // Check same level, 1 up, or 1 down
                for (int dy = -1; dy <= 1; dy++)
                {
                    var neighborPos = new Vector3(x + dx, y + dy, z + dz);
                    if (IsWalkable(neighborPos))
                    {
                        yield return neighborPos;
                    }
                }
            }
        }
    }
}
```

### 7.2 Economy (Resource Extraction)

**Resource Gathering Integration**:
```csharp
public class ResourceExtractionSystem
{
    private readonly World _world;
    
    public GatheringResult TryGatherBlock(
        BlockPosition pos, 
        Entity gatherer, 
        ToolItem tool)
    {
        var block = _world.GetBlock(pos.X, pos.Y, pos.Z);
        var blockDef = BlockRegistry.Get(block.BlockId);
        
        // Validate gatherable
        if (!blockDef.IsGatherable)
            return GatheringResult.Failure("Block is not gatherable");
        
        // Check tool requirements
        if (blockDef.RequiredToolType != null && 
            (tool == null || tool.ToolType != blockDef.RequiredToolType))
        {
            return GatheringResult.Failure($"Requires {blockDef.RequiredToolType} tool");
        }
        
        // Calculate yield
        int yield = CalculateYield(blockDef, tool, gatherer.Skills);
        
        // Remove block from world
        _world.SetBlock(pos.X, pos.Y, pos.Z, new BlockData(0)); // Replace with air
        
        // Create dropped item entity
        var itemDrop = new ItemDropEntity(
            itemId: GetDropItemId(blockDef),
            quantity: yield,
            position: new Vector3(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f)
        );
        
        _world.SpawnEntity(itemDrop);
        
        // Log economic event
        _economy.LogGathering(gatherer, blockDef, yield);
        
        return GatheringResult.Success(yield);
    }
    
    private int CalculateYield(BlockDefinition blockDef, ToolItem tool, SkillSet skills)
    {
        float baseYield = 1.0f;
        
        // Tool modifier
        if (tool != null)
            baseYield *= tool.Efficiency;
        
        // Skill modifier
        baseYield *= (1.0f + skills.Gathering * 0.1f);
        
        // Random variance (±20%)
        baseYield *= (0.8f + (float)_random.NextDouble() * 0.4f);
        
        return Mathf.Max(1, Mathf.RoundToInt(baseYield));
    }
}
```

### 7.3 Governance (Land Claims in 3D)

**3D Land Claim System**:
```csharp
public class LandClaimSystem
{
    private readonly World _world;
    
    /// <summary>
    /// Check if an action is permitted at a block position
    /// </summary>
    public bool CanModifyBlock(BlockPosition pos, Entity actor)
    {
        // Bedrock layer: always denied
        if (pos.Y == -200)
            return false;
        
        // Find jurisdiction at this location
        var jurisdiction = _governance.GetJurisdictionAt(pos.X, pos.Z);
        
        if (jurisdiction == null)
        {
            // Unclaimed land: free to modify (with basic rules)
            return true;
        }
        
        // Check claim ownership
        var claim = jurisdiction.GetClaimAt(pos.X, pos.Z);
        
        if (claim == null)
        {
            // Public land within jurisdiction: check laws
            return jurisdiction.HasPermission(actor, Permission.ModifyPublicLand);
        }
        
        // Private claim: must be owner or have permission
        if (claim.OwnerId == actor.Id)
            return true;
        
        // Check if actor is in permissions list
        return claim.HasPermission(actor.Id, Permission.ModifyBlocks);
    }
    
    /// <summary>
    /// Create a land claim (3D bounding box)
    /// </summary>
    public Claim CreateClaim(
        Entity owner, 
        int minX, int maxX, 
        int minZ, int maxZ,
        ClaimType type)
    {
        // Validate claim size
        int width = maxX - minX;
        int depth = maxZ - minZ;
        int area = width * depth;
        
        if (area > type.MaxArea)
            throw new InvalidOperationException($"Claim exceeds maximum area for {type}");
        
        // Check for overlapping claims
        var overlapping = _governance.FindOverlappingClaims(minX, maxX, minZ, maxZ);
        if (overlapping.Any())
            throw new InvalidOperationException("Claim overlaps existing claims");
        
        // Create claim (full vertical extent)
        var claim = new Claim
        {
            OwnerId = owner.Id,
            MinX = minX,
            MaxX = maxX,
            MinZ = minZ,
            MaxZ = maxZ,
            MinY = -200,  // Bedrock to surface
            MaxY = 56,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };
        
        _governance.RegisterClaim(claim);
        return claim;
    }
}
```

### 7.4 Physics (Collision Generation)

**Voxel Collision System**:
```csharp
public class VoxelCollisionSystem
{
    /// <summary>
    /// Generate collision shape for a chunk
    /// </summary>
    public void UpdateChunkCollisions(VoxelChunk chunk)
    {
        var collisionShapes = new List<CollisionShape3D>();
        
        // Iterate through chunk blocks
        for (int y = 0; y < VoxelChunk.Height; y++)
        {
            for (int z = 0; z < VoxelChunk.Depth; z++)
            {
                for (int x = 0; x < VoxelChunk.Width; x++)
                {
                    var block = chunk.GetBlockFast(x, y, z);
                    var blockDef = BlockRegistry.Get(block.BlockId);
                    
                    if (!blockDef.IsSolid)
                        continue; // No collision for non-solid blocks
                    
                    // Create box shape for solid block
                    var box = new BoxShape3D();
                    box.Size = new Vector3(1, 1, 1);
                    
                    var collisionShape = new CollisionShape3D();
                    collisionShape.Shape = box;
                    collisionShape.Position = new Vector3(
                        chunk.ChunkX * 16 + x + 0.5f,
                        y + 0.5f,
                        chunk.ChunkZ * 16 + z + 0.5f
                    );
                    
                    collisionShapes.Add(collisionShape);
                }
            }
        }
        
        // Combine into static body (server-side optimization)
        var staticBody = new StaticBody3D();
        foreach (var shape in collisionShapes)
        {
            staticBody.AddChild(shape);
        }
        
        // Replace old collision body
        chunk.CollisionBody = staticBody;
    }
    
    /// <summary>
    /// Raycast against voxel world
    /// </summary>
    public VoxelRaycastResult Raycast(Vector3 from, Vector3 direction, float maxDistance)
    {
        // DDA (Digital Differential Analysis) algorithm for voxel traversal
        var currentPos = from;
        var step = direction.Normalized();
        
        float tMaxX = (Mathf.Ceil(from.X) - from.X) / step.X;
        float tMaxY = (Mathf.Ceil(from.Y) - from.Y) / step.Y;
        float tMaxZ = (Mathf.Ceil(from.Z) - from.Z) / step.Z;
        
        float tDeltaX = 1.0f / Mathf.Abs(step.X);
        float tDeltaY = 1.0f / Mathf.Abs(step.Y);
        float tDeltaZ = 1.0f / Mathf.Abs(step.Z);
        
        int currentX = Mathf.FloorToInt(from.X);
        int currentY = Mathf.FloorToInt(from.Y);
        int currentZ = Mathf.FloorToInt(from.Z);
        
        float distance = 0;
        
        while (distance < maxDistance)
        {
            // Check current block
            var block = _world.GetBlock(currentX, currentY, currentZ);
            if (!block.IsAir)
            {
                var blockDef = BlockRegistry.Get(block.BlockId);
                if (blockDef.IsSolid)
                {
                    return new VoxelRaycastResult
                    {
                        Hit = true,
                        Position = new Vector3(currentX, currentY, currentZ),
                        Distance = distance,
                        Block = block
                    };
                }
            }
            
            // Step to next voxel
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                currentX += step.X > 0 ? 1 : -1;
                distance = tMaxX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxZ)
            {
                currentY += step.Y > 0 ? 1 : -1;
                distance = tMaxY;
                tMaxY += tDeltaY;
            }
            else
            {
                currentZ += step.Z > 0 ? 1 : -1;
                distance = tMaxZ;
                tMaxZ += tDeltaZ;
            }
        }
        
        return new VoxelRaycastResult { Hit = false };
    }
}
```

---

## 8. Performance Budget

### 8.1 Memory Per Chunk

**Detailed Breakdown**:

| Component | Bytes Per Block | Total (65,536 blocks) | Notes |
|-----------|----------------|------------------------|-------|
| **Block Data** | 4 | 262,144 (256 KB) | Primary storage |
| **Light Data** | 0 (packed) | 0 | Packed into BlockData |
| **Biome Data** | 0 (separate) | 0 | Separate system |
| **Modified Tracking** | 4 (average) | ~2,048 (2 KB) | HashSet overhead |
| **Object Overhead** | - | ~64 | C# object header |
| **Total** | | **~258 KB** | Per chunk |

**Scaling Calculations**:

| Scenario | Chunks Loaded | Memory Required | Notes |
|----------|---------------|-----------------|-------|
| **Single chunk** | 1 | 258 KB | Base unit |
| **Player view (8 chunks)** | 8 | ~2 MB | Small build area |
| **Simulation radius (5 chunks)** | 121 | ~31 MB | MVP server per player |
| **Full MVP world** | 1,936 | ~500 MB | All chunks loaded |
| **Full Post-MVP** | 7,744 | ~2 GB | All chunks loaded |

### 8.2 Maximum Loaded Chunks

**Server Limits**:
```csharp
public static class VoxelPerformanceBudget
{
    // MVP: 8 players, simulation radius 5
    public const int MVPPlayerCount = 8;
    public const int MVPSimulationRadius = 5;
    public const int MVPChunksPerPlayer = 121; // (5*2+1)^2
    public const int MVPTotalActiveChunks = 968; // 8 * 121
    public const int MVPMemoryMB = 250; // 968 * 258 KB
    
    // Post-MVP: 20 players, simulation radius 8
    public const int PostMVPPlayerCount = 20;
    public const int PostMVPSimulationRadius = 8;
    public const int PostMVPChunksPerPlayer = 289; // (8*2+1)^2
    public const int PostMVPTotalActiveChunks = 5780; // 20 * 289
    public const int PostMVPMemoryMB = 1500; // ~1.5 GB
    
    // Maximum supported (with 4GB RAM budget)
    public const int MaxActiveChunks = 16000; // ~4 GB
    public const int MaxPlayersAtMaxRadius = 32; // 16000 / 289 / 1.5 (overlap factor)
}
```

### 8.3 Block Update Performance

**Update Budget**:

| Operation | Time Budget | Max Per Tick | Notes |
|-----------|-------------|--------------|-------|
| **Block placement** | 0.1 ms | 500 blocks | Building, mining |
| **Block breaking** | 0.1 ms | 500 blocks | Resource gathering |
| **Fluid updates** | 0.5 ms | 100 blocks | Water/lava flow |
| **Growth ticks** | 0.2 ms | 200 blocks | Crops, trees |
| **Gravity (falling)** | 0.1 ms | 100 blocks | Sand, gravel |
| **Total per tick** | 1.0 ms | 1,400 blocks | At 20 TPS = 20ms budget |

**Update Queuing**:
```csharp
public class BlockUpdateScheduler
{
    private Queue<BlockUpdate> _pendingUpdates = new();
    private const int MaxUpdatesPerTick = 1400;
    
    public void QueueUpdate(BlockPosition pos, BlockData newBlock)
    {
        _pendingUpdates.Enqueue(new BlockUpdate(pos, newBlock));
    }
    
    public void ProcessUpdates(long currentTick)
    {
        int processed = 0;
        var stopwatch = Stopwatch.StartNew();
        
        while (_pendingUpdates.Count > 0 && processed < MaxUpdatesPerTick)
        {
            if (stopwatch.ElapsedMilliseconds > 1)
                break; // Budget exhausted
            
            var update = _pendingUpdates.Dequeue();
            ApplyBlockUpdate(update);
            processed++;
        }
        
        // Remaining updates deferred to next tick
        Metrics.RecordBlockUpdates(processed, _pendingUpdates.Count);
    }
}
```

**Chunk Generation Performance**:

| Generation Phase | Time Budget | Target | Notes |
|------------------|-------------|--------|-------|
| **Terrain noise** | 5 ms | 1 chunk | Base heightmap |
| **Cave carving** | 3 ms | 1 chunk | 3D noise tunnels |
| **Ore placement** | 2 ms | 1 chunk | Mineral veins |
| **Surface details** | 2 ms | 1 chunk | Trees, grass |
| **Total generation** | 12 ms | 1 chunk | Async background |

**Persistence Performance**:

| Operation | Target Latency | Throughput | Notes |
|-----------|---------------|------------|-------|
| **Chunk load (SSD)** | < 5 ms | 200 chunks/sec | From SQLite/PostgreSQL |
| **Chunk save (SSD)** | < 3 ms | 300 chunks/sec | Delta compression |
| **Full world save** | < 1 sec | 2,000 chunks | Async batching |
| **Network serialize** | < 1 ms | 1,000 chunks/sec | For client transfer |

---

## Document Information

### Authoring
- **Author**: AI Assistant
- **Date**: 2026-02-01
- **Status**: Draft

### Cross-References

**Documents Referenced**:
- `01-architecture-overview.md` - Core technical architecture
- `minecraft-voxel-research.md` - Voxel system research
- `03-data-persistence.md` - Persistence architecture
- `04-performance-scalability.md` - Performance budgets

**Documents Informed**:
- Session 2: AI System Design - Pathfinding on voxels
- Session 3: Core Gameplay Loops - Resource gathering, building
- Session 4: Progression & Balance - Resource distribution
- Session 5: Governance Mechanics - 3D land claims

---

**Previous**: [← Security Specification](12-security-spec.md) | **Index**: [Session 1 Index]([AGENTS-READ-FIRST]-index.md)
