# 17. Voxel Rendering & Meshing System

> **Navigation**: [Index]([AGENTS-READ-FIRST]-index.md) | [← Previous: Session 1 Documents](../session-1-technical-architecture/01-architecture-overview.md)
> 
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

# Technical Specification: Voxel Rendering & Meshing

**Document Version**: 1.0  
**Status**: Draft  
**Last Updated**: 2026-02-01  
**Target Engine**: Godot 4.x + C#  
**Related Documents**: [Minecraft Voxel Research](minecraft-voxel-research.md), [Performance & Scalability](04-performance-scalability.md)

---

## Executive Summary

This document specifies the complete voxel rendering system for Societies, a Godot 4.x-based multiplayer civilization simulation. The system renders **16×16×256 block chunks** at **60 FPS** with **64-128 visible chunks** using aggressive optimization techniques including greedy meshing, face culling, and LOD systems.

**Key Technical Decisions**:

| Decision | Approach | Rationale |
|----------|----------|-----------|
| **Meshing Algorithm** | Greedy meshing with face culling | 90% triangle reduction vs naive approach |
| **Rendering API** | Godot ArrayMesh + SurfaceTool | Native Godot 4 optimization, no external dependencies |
| **Threading** | Background mesh generation | Maintains 60 FPS during chunk updates |
| **LOD Strategy** | Distance-based mesh simplification | 4-level LOD reduces far chunk triangles by 94% |
| **Texture Strategy** | Single texture atlas (4096×4096) | Single draw call per chunk, 16×16px per block type |

**Performance Targets**:
- **60 FPS** with 64-128 visible chunks
- **<16ms** mesh generation time per chunk (background thread)
- **<100 draw calls** for 128 chunks (batching via shared materials)
- **~270MB** VRAM for texture atlas + mesh buffers
- **GPU instancing** for vegetation/details (1000+ instances per chunk)

**Godot 4 Specific Advantages**:
- SurfaceTool provides optimized vertex buffer construction
- ArrayMesh supports dynamic mesh updates without recreation
- Multi-threaded mesh generation using Godot's Thread class
- Built-in frustum and occlusion culling via RenderingServer

---

## Table of Contents

1. [Mesh Generation Strategy](#1-mesh-generation-strategy)
2. [Greedy Meshing Algorithm](#2-greedy-meshing-algorithm)
3. [Texture Atlasing](#3-texture-atlasing)
4. [Godot 4 Implementation](#4-godot-4-implementation)
5. [LOD System](#5-lod-system)
6. [Chunk Mesh Management](#6-chunk-mesh-management)
7. [Optimization Techniques](#7-optimization-techniques)
8. [Visual Effects](#8-visual-effects)
9. [Performance Budgets](#9-performance-budgets)
10. [C# Code Examples](#10-c-code-examples)
11. [Implementation Roadmap](#11-implementation-roadmap)

---

## 1. Mesh Generation Strategy

### 1.1 Rendering Pipeline Overview

```mermaid
graph LR
    A[Chunk Data<br/>16×16×256] --> B[Face Culling<br/>Hidden Face Removal]
    B --> C[Greedy Meshing<br/>Quad Consolidation]
    C --> D[UV Mapping<br/>Texture Atlas]
    D --> E[SurfaceTool<br/>Vertex Construction]
    E --> F[ArrayMesh<br/>Godot Mesh]
    F --> G[MeshInstance3D<br/>Scene Node]
    G --> H[RenderingServer<br/>GPU Rendering]
```

### 1.2 Face Culling (Hidden Face Removal)

**Algorithm**: For each block face, check adjacent block in that direction. If adjacent block is opaque and solid, don't render the face.

**Culling Rules**:
```csharp
// Face visibility check
bool ShouldRenderFace(BlockType current, BlockType neighbor, Direction dir)
{
    // Air blocks always render adjacent faces
    if (current == BlockType.Air) return false;
    
    // Solid neighbor blocks occlude faces
    if (neighbor.IsOpaque && neighbor.IsSolid) return false;
    
    // Transparent neighbors (water, glass) don't occlude
    if (!neighbor.IsOpaque) return true;
    
    // Special case: Same block type culls faces between them
    if (current == neighbor && !current.AlwaysRenderFace) return false;
    
    return true;
}
```

**Performance Impact**:
- **Naive rendering**: 6 faces × 2 triangles × 16×16×256 = 786,432 triangles per chunk
- **With face culling**: ~50,000-150,000 triangles (depends on terrain complexity)
- **Reduction**: 80-94% triangle elimination

### 1.3 Adjacent Block Checking

**Chunk Boundary Handling**:

```csharp
public BlockType GetBlockWithNeighbors(int x, int y, int z, Chunk chunk)
{
    // Check bounds
    if (x < 0) return chunk.NeighborWest?.GetBlock(15, y, z) ?? BlockType.Air;
    if (x >= 16) return chunk.NeighborEast?.GetBlock(0, y, z) ?? BlockType.Air;
    if (z < 0) return chunk.NeighborSouth?.GetBlock(x, y, 15) ?? BlockType.Air;
    if (z >= 16) return chunk.NeighborNorth?.GetBlock(x, y, 0) ?? BlockType.Air;
    if (y < 0 || y >= 256) return BlockType.Air; // World bounds
    
    return chunk.GetBlock(x, y, z);
}
```

**Optimization**: Pre-load neighbor chunks during mesh generation to avoid repeated lookups.

### 1.4 Mesh Optimization Pipeline

**Processing Order**:
1. **Face Culling** (eliminate hidden faces)
2. **Greedy Meshing** (consolidate faces into larger quads)
3. **Quad Sorting** (group by texture/material for batching)
4. **Vertex Generation** (construct optimized vertex buffers)
5. **Index Buffer** (indexed drawing for memory efficiency)

---

## 2. Greedy Meshing Algorithm

### 2.1 Algorithm Description

Greedy meshing combines adjacent faces of the same block type into larger quads, reducing triangle count while maintaining visual quality.

**Core Principle**: Instead of rendering individual block faces (6 faces × 2 triangles = 12 triangles per visible block), combine matching faces into large quads.

### 2.2 Implementation Details

**2D Greedy Meshing (Per-Slice)**:

```csharp
public class GreedyMeshing
{
    private const int CHUNK_SIZE = 16;
    private const int CHUNK_HEIGHT = 256;
    
    public List<Quad> GenerateMesh(Chunk chunk, Direction faceDir)
    {
        var quads = new List<Quad>();
        var visited = new bool[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
        
        // Iterate through chunk dimensions perpendicular to face direction
        for (int slice = 0; slice < GetSliceCount(faceDir); slice++)
        {
            // Reset visited for new slice
            Array.Clear(visited, 0, visited.Length);
            
            for (int u = 0; u < CHUNK_SIZE; u++)
            {
                for (int v = 0; v < CHUNK_HEIGHT; v++)
                {
                    if (visited[u, v]) continue;
                    
                    // Get block at this position facing outward
                    var (x, y, z) = GetCoordinates(faceDir, slice, u, v);
                    var block = chunk.GetBlock(x, y, z);
                    
                    // Check if face should be rendered
                    if (!ShouldRenderFace(block, GetNeighbor(x, y, z, faceDir), faceDir))
                        continue;
                    
                    // Expand quad greedily
                    int width = 1;
                    int height = 1;
                    
                    // Expand horizontally
                    while (u + width < CHUNK_SIZE && 
                           CanExtend(block, faceDir, slice, u + width, v, visited))
                    {
                        width++;
                    }
                    
                    // Expand vertically
                    bool canExpandV = true;
                    while (canExpandV && v + height < CHUNK_HEIGHT)
                    {
                        for (int i = 0; i < width; i++)
                        {
                            if (!CanExtend(block, faceDir, slice, u + i, v + height, visited))
                            {
                                canExpandV = false;
                                break;
                            }
                        }
                        if (canExpandV) height++;
                    }
                    
                    // Mark visited
                    for (int i = 0; i < width; i++)
                        for (int j = 0; j < height; j++)
                            visited[u + i, v + j] = true;
                    
                    // Create quad
                    quads.Add(CreateQuad(faceDir, slice, u, v, width, height, block));
                }
            }
        }
        
        return quads;
    }
    
    private bool CanExtend(BlockType type, Direction dir, int slice, int u, int v, bool[,,] visited)
    {
        if (visited[u, v]) return false;
        
        var (x, y, z) = GetCoordinates(dir, slice, u, v);
        var block = chunk.GetBlock(x, y, z);
        
        // Must be same block type and face should render
        return block == type && 
               ShouldRenderFace(block, GetNeighbor(x, y, z, dir), dir);
    }
}
```

### 2.3 Quad Generation

**Quad Structure**:

```csharp
public struct Quad
{
    public Vector3 Position;      // World position of corner
    public Vector2 Size;          // Width, Height in blocks
    public Direction Face;        // Which face direction
    public BlockType Block;       // Block type for texturing
    public int Layer;             // Y-layer for LOD
    
    // Convert to mesh vertices
    public void GenerateVertices(List<Vector3> verts, List<Vector2> uvs, List<int> indices)
    {
        // Generate 4 vertices based on face direction
        var corners = GetCorners();
        int baseIndex = verts.Count;
        
        verts.AddRange(corners);
        uvs.AddRange(GetUVs());
        
        // Two triangles: 0-1-2, 0-2-3
        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }
}
```

### 2.4 Texture Coordinate Mapping

**Texture Atlas UV Mapping**:

```csharp
public class TextureAtlas
{
    private const int ATLAS_SIZE = 4096;  // 4096×4096 texture
    private const int BLOCK_TEX_SIZE = 16; // 16×16 per block face
    private const int BLOCKS_PER_ROW = ATLAS_SIZE / BLOCK_TEX_SIZE; // 256
    
    public Vector2[] GetBlockUVs(BlockType block, Direction face)
    {
        // Get texture index for this block face
        int texIndex = block.GetTextureIndex(face);
        
        // Calculate UV coordinates in atlas
        float u = (texIndex % BLOCKS_PER_ROW) * BLOCK_TEX_SIZE;
        float v = (texIndex / BLOCKS_PER_ROW) * BLOCK_TEX_SIZE;
        float size = BLOCK_TEX_SIZE;
        float atlas = ATLAS_SIZE;
        
        // Return 4 UVs for quad corners
        // Add small inset to avoid texture bleeding
        float inset = 0.001f;
        
        return new Vector2[]
        {
            new Vector2((u + inset) / atlas, (v + size - inset) / atlas),        // Top-left
            new Vector2((u + size - inset) / atlas, (v + size - inset) / atlas), // Top-right
            new Vector2((u + size - inset) / atlas, (v + inset) / atlas),        // Bottom-right
            new Vector2((u + inset) / atlas, (v + inset) / atlas)                // Bottom-left
        };
    }
}
```

### 2.5 Performance Characteristics

**Greedy Meshing Benefits**:

| Metric | Naive | Face Culling | Greedy Meshing | Improvement |
|--------|-------|--------------|----------------|-------------|
| Triangles (flat terrain) | 393,216 | 6,144 | 384 | 99.9% reduction |
| Triangles (hilly terrain) | 393,216 | ~150,000 | ~8,000 | 98% reduction |
| Triangles (cave wall) | 393,216 | ~100,000 | ~4,000 | 99% reduction |
| Mesh Gen Time | 5ms | 12ms | 25ms | Acceptable (async) |
| Draw Calls | 256 | 256 | 6-12 | 95% reduction |

**Trade-offs**:
- **Slightly more CPU time** for mesh generation (offset by async threading)
- **Texture uniformity required** across combined faces (no per-block tinting without vertex colors)
- **LOD compatibility** requires separate greedy pass per LOD level

---

## 3. Texture Atlasing

### 3.1 Texture Atlas Design

**Atlas Layout**:

```
┌─────────────────────────────────────────────────────────────┐
│  0    1    2    3    ...  253  254  255                     │
│ ┌──┐ ┌──┐ ┌──┐ ┌──┐      ┌──┐ ┌──┐ ┌──┐                   │
│ │Grass│Stone│Dirt │Sand...│Gold│Iron│Diam│                   │
│ └──┘ └──┘ └──┘ └──┘      └──┘ └──┘ └──┘                   │
│ ...                                                         │
│                                                             │
│ Row 255: Special blocks                                     │
└─────────────────────────────────────────────────────────────┘
   4096×4096 pixels total
   16×16 pixels per block texture
   256×256 = 65,536 block types supported
```

**Multi-Texture Support**:
- **Base color atlas** (RGB): Main block textures
- **Normal atlas** (RGB): Surface normals for lighting
- **Material atlas** (R=roughness, G=metallic, B=AO): PBR properties
- **Emission atlas** (RGB): Self-illuminating blocks

### 3.2 UV Mapping Per Block Type

**Block Face Texturing**:

```csharp
public class BlockType
{
    public int TopTextureIndex { get; set; }      // Y+ face
    public int BottomTextureIndex { get; set; }   // Y- face
    public int SideTextureIndex { get; set; }     // X+, X-, Z+, Z- faces
    public int[] FaceTextureIndices { get; set; } // Individual face indices
    
    public int GetTextureIndex(Direction face)
    {
        return face switch
        {
            Direction.Up => TopTextureIndex,
            Direction.Down => BottomTextureIndex,
            Direction.North or Direction.South or 
            Direction.East or Direction.West => SideTextureIndex,
            _ => SideTextureIndex
        };
    }
}
```

**Animated Textures**:
- Store animation frames horizontally in atlas
- UV offset updated in shader or per-frame
- Water, lava, fire, portals support 16-32 frame animations

### 3.3 Material Management

**StandardMaterial3D Setup**:

```csharp
public Material CreateChunkMaterial(Texture2D atlas)
{
    var material = new StandardMaterial3D
    {
        AlbedoTexture = atlas,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
        SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
        
        // Performance settings
        CullMode = BaseMaterial3D.CullModeEnum.Back,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly
    };
    
    // Enable texture repeating for UV mapping
    material.Uv1Scale = new Vector3(1, 1, 1);
    material.Uv1Offset = new Vector3(0, 0, 0);
    
    return material;
}
```

**Shader Material (Advanced)**:

```glsl
// Voxel chunk shader with texture atlas support
shader_type spatial;

uniform sampler2D atlas_texture : filter_nearest_mipmap_anisotropic;
uniform float atlas_size = 4096.0;
uniform float block_tex_size = 16.0;

varying vec2 atlas_uv;

void vertex() {
    // UVs passed from vertex data
    atlas_uv = UV;
}

void fragment() {
    // Sample from atlas with proper filtering
    vec4 tex_color = texture(atlas_texture, atlas_uv);
    
    ALBEDO = tex_color.rgb;
    ALPHA = tex_color.a;
    
    // Discard fully transparent pixels
    if (tex_color.a < 0.5) {
        discard;
    }
}
```

### 3.4 Texture Resolution

**Recommended Specifications**:

| Texture Type | Resolution | Format | Memory |
|-------------|------------|--------|--------|
| Color Atlas | 4096×4096 | RGBA8 (DXT5 compressed) | ~11MB |
| Normal Atlas | 4096×4096 | RGB8 (DXT1 compressed) | ~8MB |
| Material Atlas | 4096×4096 | RGB8 | ~8MB |
| **Total** | - | - | **~27MB VRAM** |

**Mipmapping**:
- Generate 12 mipmap levels (4096→1)
- Use trilinear filtering with anisotropy (8x)
- Improves visual quality at oblique angles

---

## 4. Godot 4 Implementation

### 4.1 SurfaceTool Usage

**SurfaceTool** is Godot 4's utility for constructing meshes programmatically.

```csharp
public class ChunkMeshGenerator
{
    private SurfaceTool _surfaceTool;
    
    public ChunkMeshGenerator()
    {
        _surfaceTool = new SurfaceTool();
    }
    
    public ArrayMesh GenerateChunkMesh(Chunk chunk)
    {
        _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        
        // Generate all visible faces
        var quads = GenerateAllQuads(chunk);
        
        foreach (var quad in quads)
        {
            AddQuadToSurface(quad);
        }
        
        // Generate optimized mesh
        _surfaceTool.GenerateNormals();
        _surfaceTool.GenerateTangents();
        _surfaceTool.Index();
        
        var mesh = _surfaceTool.Commit();
        
        // Clear for reuse
        _surfaceTool.Clear();
        
        return mesh;
    }
    
    private void AddQuadToSurface(Quad quad)
    {
        var corners = quad.GetWorldCorners();
        var normals = quad.GetNormals();
        var uvs = quad.GetUVs();
        
        // Add vertices with attributes
        for (int i = 0; i < 4; i++)
        {
            _surfaceTool.SetNormal(normals[i]);
            _surfaceTool.SetUV(uvs[i]);
            _surfaceTool.AddVertex(corners[i]);
        }
        
        // Add indices for two triangles
        _surfaceTool.AddIndex(0);
        _surfaceTool.AddIndex(1);
        _surfaceTool.AddIndex(2);
        _surfaceTool.AddIndex(0);
        _surfaceTool.AddIndex(2);
        _surfaceTool.AddIndex(3);
    }
}
```

### 4.2 ArrayMesh Generation

**Direct ArrayMesh Construction** (faster for large meshes):

```csharp
public class DirectMeshBuilder
{
    public ArrayMesh BuildMeshFromQuads(List<Quad> quads)
    {
        // Pre-allocate arrays
        int vertexCount = quads.Count * 4;
        int indexCount = quads.Count * 6;
        
        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var indices = new int[indexCount];
        
        int vIdx = 0;
        int iIdx = 0;
        
        foreach (var quad in quads)
        {
            // Get quad data
            var (corners, quadNormals, quadUVs) = quad.GetMeshData();
            
            // Add vertices
            for (int i = 0; i < 4; i++)
            {
                vertices[vIdx] = corners[i];
                normals[vIdx] = quadNormals[i];
                uvs[vIdx] = quadUVs[i];
                vIdx++;
            }
            
            // Add indices (relative to current quad)
            int baseIdx = vIdx - 4;
            indices[iIdx++] = baseIdx + 0;
            indices[iIdx++] = baseIdx + 1;
            indices[iIdx++] = baseIdx + 2;
            indices[iIdx++] = baseIdx + 0;
            indices[iIdx++] = baseIdx + 2;
            indices[iIdx++] = baseIdx + 3;
        }
        
        // Create arrays for mesh
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        
        // Create mesh
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        
        return mesh;
    }
}
```

### 4.3 MeshInstance3D Setup

```csharp
public class ChunkRenderer : Node3D
{
    private MeshInstance3D _meshInstance;
    private Chunk _chunk;
    private Material _material;
    
    public override void _Ready()
    {
        _meshInstance = new MeshInstance3D();
        _meshInstance.Name = $"Chunk_{_chunk.X}_{_chunk.Z}";
        _meshInstance.Position = new Vector3(
            _chunk.X * 16, 
            0, 
            _chunk.Z * 16
        );
        
        AddChild(_meshInstance);
    }
    
    public void UpdateMesh(ArrayMesh mesh)
    {
        // Clean up old mesh
        if (_meshInstance.Mesh != null)
        {
            _meshInstance.Mesh.Dispose();
        }
        
        // Assign new mesh
        _meshInstance.Mesh = mesh;
        
        // Set material (shared across all chunks)
        if (_meshInstance.GetSurfaceOverrideMaterial(0) == null)
        {
            _meshInstance.SetSurfaceOverrideMaterial(0, _material);
        }
    }
    
    public void ClearMesh()
    {
        if (_meshInstance.Mesh != null)
        {
            _meshInstance.Mesh.Dispose();
            _meshInstance.Mesh = null;
        }
    }
}
```

### 4.4 Material Assignment

**Shared Material Strategy**:

```csharp
public class VoxelMaterialManager
{
    private static StandardMaterial3D _sharedMaterial;
    private static Texture2D _atlasTexture;
    
    public static StandardMaterial3D GetSharedMaterial()
    {
        if (_sharedMaterial == null)
        {
            _atlasTexture = ResourceLoader.Load<Texture2D>("res://textures/block_atlas.png");
            
            _sharedMaterial = new StandardMaterial3D
            {
                AlbedoTexture = _atlasTexture,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmapsAnisotropic,
                TextureRepeat = false,
                
                // Shadows
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                
                // Culling
                CullMode = BaseMaterial3D.CullModeEnum.Back,
                
                // LOD fade
                DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
                DistanceFadeMinDistance = 200.0f,
                DistanceFadeMaxDistance = 250.0f
            };
        }
        
        return _sharedMaterial;
    }
}
```

### 4.5 Code Examples

**Complete Chunk Mesh Generation**:

```csharp
public class ChunkMeshBuilder : RefCounted
{
    private SurfaceTool _surfaceTool;
    private TextureAtlas _atlas;
    
    public ChunkMeshBuilder()
    {
        _surfaceTool = new SurfaceTool();
        _atlas = new TextureAtlas();
    }
    
    public ArrayMesh BuildChunkMesh(ChunkData chunk, ChunkData[] neighbors)
    {
        _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        _surfaceTool.SetMaterial(VoxelMaterialManager.GetSharedMaterial());
        
        // Generate mesh for each face direction
        for (int dir = 0; dir < 6; dir++)
        {
            var faceDir = (Direction)dir;
            GenerateFaceLayer(chunk, neighbors, faceDir);
        }
        
        _surfaceTool.GenerateNormals();
        _surfaceTool.Index();
        
        var mesh = _surfaceTool.Commit();
        _surfaceTool.Clear();
        
        return mesh;
    }
    
    private void GenerateFaceLayer(ChunkData chunk, ChunkData[] neighbors, Direction faceDir)
    {
        int dx = faceDir == Direction.East ? 1 : faceDir == Direction.West ? -1 : 0;
        int dz = faceDir == Direction.North ? 1 : faceDir == Direction.South ? -1 : 0;
        int dy = faceDir == Direction.Up ? 1 : faceDir == Direction.Down ? -1 : 0;
        
        // Iterate through chunk
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 256; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    
                    if (block == BlockType.Air) continue;
                    
                    // Check neighbor
                    int nx = x + dx;
                    int ny = y + dy;
                    int nz = z + dz;
                    
                    BlockType neighbor;
                    if (nx < 0 || nx >= 16 || nz < 0 || nz >= 16)
                    {
                        // Use neighbor chunk
                        neighbor = GetNeighborBlock(neighbors, nx, ny, nz);
                    }
                    else
                    {
                        neighbor = chunk.GetBlock(nx, ny, nz);
                    }
                    
                    // Skip if neighbor is opaque solid
                    if (neighbor.IsOpaque && neighbor.IsSolid) continue;
                    
                    // Add face
                    AddBlockFace(x, y, z, faceDir, block);
                }
            }
        }
    }
    
    private void AddBlockFace(int x, int y, int z, Direction dir, BlockType block)
    {
        var (verts, uvs) = GetFaceGeometry(x, y, z, dir, block);
        
        for (int i = 0; i < 4; i++)
        {
            _surfaceTool.SetUV(uvs[i]);
            _surfaceTool.AddVertex(verts[i]);
        }
        
        // Indices
        _surfaceTool.AddIndex(0);
        _surfaceTool.AddIndex(1);
        _surfaceTool.AddIndex(2);
        _surfaceTool.AddIndex(0);
        _surfaceTool.AddIndex(2);
        _surfaceTool.AddIndex(3);
    }
}
```

---

## 5. LOD System

### 5.1 Level of Detail Strategy

**LOD Levels**:

| Level | Distance | Block Aggregation | Triangles | Visual Quality |
|-------|----------|-------------------|-----------|----------------|
| LOD0 | 0-32m | 1×1×1 (full detail) | 100% | Full |
| LOD1 | 32-96m | 2×2×2 (8→1 blocks) | ~25% | Good |
| LOD2 | 96-192m | 4×4×4 (64→1 blocks) | ~6% | Reduced |
| LOD3 | 192m+ | 8×8×8 (512→1 blocks) | ~1.5% | Low |

### 5.2 Distance-Based LOD Switching

```csharp
public class LODManager
{
    private const float LOD0_DIST = 32.0f;
    private const float LOD1_DIST = 96.0f;
    private const float LOD2_DIST = 192.0f;
    
    public int CalculateLODLevel(Vector3 playerPos, Chunk chunk)
    {
        float distance = playerPos.DistanceTo(chunk.CenterPosition);
        
        return distance switch
        {
            < LOD0_DIST => 0,
            < LOD1_DIST => 1,
            < LOD2_DIST => 2,
            _ => 3
        };
    }
}
```

### 5.3 Simplified Meshing for Far Chunks

**LOD Mesh Generation**:

```csharp
public class LODMeshGenerator
{
    public ArrayMesh GenerateLODMesh(Chunk chunk, int lodLevel)
    {
        int step = 1 << lodLevel; // 1, 2, 4, 8
        
        _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        
        // Sample at lower resolution
        for (int x = 0; x < 16; x += step)
        {
            for (int y = 0; y < 256; y += step)
            {
                for (int z = 0; z < 16; z += step)
                {
                    // Get dominant block in this cell
                    var block = GetDominantBlock(chunk, x, y, z, step);
                    
                    if (block.IsSolid && block.IsOpaque)
                    {
                        // Check if visible
                        if (IsCellVisible(chunk, x, y, z, step))
                        {
                            AddLODBlock(x, y, z, step, block);
                        }
                    }
                }
            }
        }
        
        return _surfaceTool.Commit();
    }
    
    private BlockType GetDominantBlock(Chunk chunk, int x, int y, int z, int size)
    {
        // Simple strategy: use corner block
        // Advanced: count block types and use most common
        return chunk.GetBlock(x, y, z);
    }
}
```

### 5.4 Transition Handling

**LOD Transition Strategies**:

1. **Pop-in with Dithering**: Use alpha dithering for smooth transition
2. **Cross-fade**: Render both LODs briefly with alpha blending
3. **Geomorphing**: Morph vertices between LOD levels (expensive)
4. **Snap with Fog**: Hide transition in distance fog

**Recommended Approach**: Distance fade + fog

```csharp
// In material
_standardMaterial.DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha;
_standardMaterial.DistanceFadeMinDistance = lodSwitchDistance - 10.0f;
_standardMaterial.DistanceFadeMaxDistance = lodSwitchDistance + 10.0f;
```

---

## 6. Chunk Mesh Management

### 6.1 Mesh Generation Threading

**Background Thread Pool**:

```csharp
public class MeshGenerationQueue
{
    private Queue<Chunk> _pendingChunks = new();
    private HashSet<Chunk> _processingChunks = new();
    private Thread[] _workerThreads;
    private bool _running = true;
    
    public MeshGenerationQueue(int threadCount = 2)
    {
        _workerThreads = new Thread[threadCount];
        
        for (int i = 0; i < threadCount; i++)
        {
            _workerThreads[i] = new Thread(WorkerLoop);
            _workerThreads[i].Start();
        }
    }
    
    private void WorkerLoop()
    {
        while (_running)
        {
            Chunk chunk = null;
            
            lock (_pendingChunks)
            {
                if (_pendingChunks.Count > 0)
                {
                    chunk = _pendingChunks.Dequeue();
                    _processingChunks.Add(chunk);
                }
            }
            
            if (chunk != null)
            {
                try
                {
                    var mesh = GenerateChunkMesh(chunk);
                    
                    // Queue for main thread application
                    CallDeferred(nameof(ApplyMesh), chunk, mesh);
                }
                finally
                {
                    lock (_pendingChunks)
                    {
                        _processingChunks.Remove(chunk);
                    }
                }
            }
            else
            {
                Thread.Sleep(1); // Avoid busy-waiting
            }
        }
    }
    
    public void QueueChunk(Chunk chunk)
    {
        lock (_pendingChunks)
        {
            if (!_processingChunks.Contains(chunk))
            {
                _pendingChunks.Enqueue(chunk);
            }
        }
    }
}
```

### 6.2 Mesh Update Triggers

**Update Conditions**:

```csharp
public class ChunkMeshUpdater
{
    public void OnBlockChanged(int x, int y, int z, BlockType oldBlock, BlockType newBlock)
    {
        // Mark chunk dirty
        var chunk = GetChunkAt(x, y, z);
        chunk.IsMeshDirty = true;
        
        // Check if neighbors need update (face visibility change)
        if (IsSurfaceBlock(oldBlock) || IsSurfaceBlock(newBlock))
        {
            UpdateNeighborChunks(x, y, z);
        }
    }
    
    private void UpdateNeighborChunks(int x, int y, int z)
    {
        // Check if change affects neighboring chunks
        if (x % 16 == 0) MarkNeighborDirty(Direction.West);
        if (x % 16 == 15) MarkNeighborDirty(Direction.East);
        if (z % 16 == 0) MarkNeighborDirty(Direction.South);
        if (z % 16 == 15) MarkNeighborDirty(Direction.North);
    }
}
```

### 6.3 Dirty Chunk Tracking

```csharp
public class DirtyChunkManager
{
    private HashSet<Chunk> _dirtyChunks = new();
    private PriorityQueue<Chunk, float> _priorityQueue = new();
    
    public void MarkDirty(Chunk chunk, float priority = 0)
    {
        lock (_dirtyChunks)
        {
            if (_dirtyChunks.Add(chunk))
            {
                // Calculate priority based on distance to player
                float distance = chunk.DistanceToPlayer;
                _priorityQueue.Enqueue(chunk, distance);
            }
        }
    }
    
    public Chunk GetNextDirtyChunk()
    {
        lock (_dirtyChunks)
        {
            if (_priorityQueue.Count > 0)
            {
                var chunk = _priorityQueue.Dequeue();
                _dirtyChunks.Remove(chunk);
                return chunk;
            }
        }
        return null;
    }
}
```

### 6.4 Mesh Pooling/Reuse

**ArrayMesh Pool**:

```csharp
public class MeshPool
{
    private Queue<ArrayMesh> _pool = new();
    private int _maxSize = 100;
    
    public ArrayMesh Acquire()
    {
        lock (_pool)
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
        }
        return new ArrayMesh();
    }
    
    public void Release(ArrayMesh mesh)
    {
        // Clear mesh data
        for (int i = mesh.GetSurfaceCount() - 1; i >= 0; i--)
        {
            mesh.SurfaceRemove(i);
        }
        
        lock (_pool)
        {
            if (_pool.Count < _maxSize)
            {
                _pool.Enqueue(mesh);
            }
            else
            {
                mesh.Dispose();
            }
        }
    }
}
```

---

## 7. Optimization Techniques

### 7.1 Frustum Culling

**Godot 4 Built-in**:

```csharp
// Enable frustum culling on chunk meshes
_meshInstance.VisibilityAabb = new Aabb(
    new Vector3(0, 0, 0),
    new Vector3(16, 256, 16)
);
```

**Manual Culling** (for large worlds):

```csharp
public class FrustumCullingManager
{
    private Camera3D _camera;
    private Plane[] _frustumPlanes = new Plane[6];
    
    public void UpdateFrustum()
    {
        // Get frustum planes from camera
        _frustumPlanes = _camera.GetFrustum();
    }
    
    public bool IsChunkVisible(Chunk chunk)
    {
        var aabb = new Aabb(
            chunk.WorldPosition,
            new Vector3(16, 256, 16)
        );
        
        // Check against all 6 frustum planes
        foreach (var plane in _frustumPlanes)
        {
            if (plane.DistanceTo(aabb.GetCenter()) < -aabb.GetLongestAxisSize() / 2)
            {
                return false;
            }
        }
        
        return true;
    }
}
```

### 7.2 Occlusion Culling

**Hardware Occlusion Queries**:

```csharp
// Godot 4 uses automatic occlusion culling via RenderingServer
// Enable in Project Settings:
// rendering/occlusion_culling/enabled = true
```

**Software Occlusion** (for caves/interiors):

```csharp
public class OcclusionManager
{
    public bool IsChunkOccluded(Chunk chunk, Camera3D camera)
    {
        // Simple check: if chunk is underground and surrounded by solid blocks
        if (chunk.IsFullyEnclosed)
        {
            // Check if any adjacent chunk is visible
            return !chunk.Neighbors.Any(n => n.IsVisible);
        }
        
        return false;
    }
}
```

### 7.3 Draw Call Batching

**Material Sharing**:
- Use single shared material for all opaque blocks
- Separate materials only for transparent/special blocks
- Results in 1 draw call per chunk (opaque)

```csharp
// All chunks use same material instance
_meshInstance.SetSurfaceOverrideMaterial(0, _sharedMaterial);
```

**Chunk Batching** (advanced):

```csharp
public class ChunkBatchRenderer
{
    // Render multiple chunks with single draw call using MultiMesh
    private MultiMesh _multiMesh;
    
    public void BatchChunks(List<Chunk> chunks)
    {
        _multiMesh = new MultiMesh();
        _multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        _multiMesh.InstanceCount = chunks.Count;
        
        for (int i = 0; i < chunks.Count; i++)
        {
            _multiMesh.SetInstanceTransform(i, 
                new Transform3D(Basis.Identity, chunks[i].WorldPosition));
        }
        
        // Single draw call renders all chunks
        _multiMeshInstance.Multimesh = _multiMesh;
    }
}
```

### 7.4 GPU Instancing for Details

**Instanced Vegetation**:

```csharp
public class VegetationInstancer
{
    private MultiMesh _treeInstances;
    private MultiMesh _grassInstances;
    
    public void UpdateInstances(Chunk chunk)
    {
        // Collect vegetation positions
        var treePositions = new List<Vector3>();
        var grassPositions = new List<Vector3>();
        
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                for (int y = 0; y < 256; y++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    if (block == BlockType.Tree)
                        treePositions.Add(new Vector3(x, y, z));
                    else if (block == BlockType.Grass)
                        grassPositions.Add(new Vector3(x, y, z));
                }
            }
        }
        
        // Update MultiMesh instance counts
        _treeInstances.InstanceCount = treePositions.Count;
        _grassInstances.InstanceCount = grassPositions.Count;
        
        // Set transforms
        for (int i = 0; i < treePositions.Count; i++)
        {
            _treeInstances.SetInstanceTransform(i, 
                new Transform3D(Basis.Identity, treePositions[i]));
        }
    }
}
```

### 7.5 Render Distance Management

**Dynamic Render Distance**:

```csharp
public class RenderDistanceManager
{
    [Export] public int MaxRenderDistance { get; set; } = 16; // chunks
    [Export] public int MinRenderDistance { get; set; } = 4;
    [Export] public float TargetFPS { get; set; } = 60;
    
    private float _currentFPS;
    private int _currentRenderDistance;
    
    public override void _Process(double delta)
    {
        _currentFPS = 1.0f / (float)delta;
        
        // Adjust render distance based on FPS
        if (_currentFPS < TargetFPS * 0.8f && _currentRenderDistance > MinRenderDistance)
        {
            _currentRenderDistance--;
            UpdateRenderDistance();
        }
        else if (_currentFPS > TargetFPS * 1.1f && _currentRenderDistance < MaxRenderDistance)
        {
            _currentRenderDistance++;
            UpdateRenderDistance();
        }
    }
}
```

---

## 8. Visual Effects

### 8.1 Block Breaking Animations

**Crack Overlay**:

```csharp
public class BlockBreakAnimator
{
    private MeshInstance3D _crackOverlay;
    private Material _crackMaterial;
    
    public void ShowBreakProgress(Vector3 blockPos, float progress)
    {
        // Progress 0.0 to 1.0
        int stage = (int)(progress * 9); // 10 stages (0-9)
        
        // Update UVs to show appropriate crack texture
        var uvs = CalculateCrackUVs(stage);
        UpdateOverlayMesh(blockPos, uvs);
    }
    
    private void UpdateOverlayMesh(Vector3 position, Vector2[] uvs)
    {
        _crackOverlay.Position = position;
        
        // Create quad covering block face
        _surfaceTool.Clear();
        _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        
        // Add vertices with crack UVs
        // ...
        
        _crackOverlay.Mesh = _surfaceTool.Commit();
    }
}
```

### 8.2 Particle Effects

**Block Breaking Particles**:

```csharp
public class BlockParticleSystem
{
    private GpuParticles3D _particles;
    
    public void SpawnBreakParticles(Vector3 position, BlockType block)
    {
        var particleMaterial = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.5f, 0.5f, 0.5f),
            Direction = new Vector3(0, 1, 0),
            Spread = 180.0f,
            InitialVelocityMin = 2.0f,
            InitialVelocityMax = 5.0f,
            Gravity = new Vector3(0, -9.8f, 0),
            ScaleMin = 0.1f,
            ScaleMax = 0.3f,
            Lifetime = 1.0f
        };
        
        // Set block texture as particle color
        particleMaterial.Color = GetBlockColor(block);
        
        _particles.ProcessMaterial = particleMaterial;
        _particles.Amount = 16;
        _particles.OneShot = true;
        _particles.Emitting = true;
        _particles.Position = position;
    }
}
```

### 8.3 Lighting Integration

**Ambient Occlusion**:

```csharp
// Calculate AO per-vertex based on adjacent blocks
private float CalculateAO(Chunk chunk, int x, int y, int z, Direction face, int corner)
{
    // Sample 3 blocks around corner
    var (dx1, dy1, dz1) = GetCornerOffsets(face, corner, 0);
    var (dx2, dy2, dz2) = GetCornerOffsets(face, corner, 1);
    var (dx3, dy3, dz3) = GetCornerOffsets(face, corner, 2);
    
    bool side1 = IsSolidBlock(chunk.GetBlock(x + dx1, y + dy1, z + dz1));
    bool side2 = IsSolidBlock(chunk.GetBlock(x + dx2, y + dy2, z + dz2));
    bool corner = IsSolidBlock(chunk.GetBlock(x + dx3, y + dy3, z + dz3));
    
    // AO formula: 1.0 if no occlusion, 0.0 if fully occluded
    if (side1 && side2) return 0.0f;
    
    float ao = 1.0f;
    if (side1) ao -= 0.3f;
    if (side2) ao -= 0.3f;
    if (corner) ao -= 0.2f;
    
    return Mathf.Max(0.0f, ao);
}

// Apply as vertex color
_surfaceTool.SetColor(new Color(ao, ao, ao, 1.0f));
```

### 8.4 Shadow Handling

**Shadow Settings**:

```csharp
// Configure directional light shadows
var sunLight = new DirectionalLight3D
{
    ShadowEnabled = true,
    ShadowBias = 0.05f,
    ShadowNormalBias = 1.0f,
    ShadowBlurScale = 2.0f,
    ShadowMaxDistance = 200.0f,
    ShadowSplit1 = 0.1f,
    ShadowSplit2 = 0.3f,
    ShadowSplit3 = 0.6f,
    ShadowFadeStart = 0.8f
};

// Enable shadow casting on chunk meshes
_meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
```

---

## 9. Performance Budgets

### 9.1 Triangles Per Chunk

**Budget by LOD**:

| LOD Level | Target Triangles | Max Triangles | Notes |
|-----------|------------------|---------------|-------|
| LOD0 (Full) | 50,000 | 150,000 | Worst case: complex terrain |
| LOD1 | 12,500 | 37,500 | 2×2×2 aggregation |
| LOD2 | 3,125 | 9,375 | 4×4×4 aggregation |
| LOD3 | 780 | 2,340 | 8×8×8 aggregation |

**Per-Frame Budget** (128 visible chunks):
- **LOD0**: 32 chunks × 50,000 = 1,600,000 triangles
- **LOD1**: 32 chunks × 12,500 = 400,000 triangles
- **LOD2**: 32 chunks × 3,125 = 100,000 triangles
- **LOD3**: 32 chunks × 780 = 25,000 triangles
- **Total**: ~2.1M triangles per frame

### 9.2 Draw Calls Target

**Budget Breakdown**:

| Component | Draw Calls | Optimization |
|-----------|-----------|--------------|
| Opaque chunks | 128 | Single material shared |
| Transparent chunks | 16 | Separate material |
| Vegetation instances | 2 | MultiMesh instances |
| Entities | 20 | Standard rendering |
| Particles | 5 | GPU particles |
| UI | 10 | Godot UI system |
| **Total** | **~180** | **Well under 500 target** |

### 9.3 Memory Budget for Meshes

**VRAM Allocation**:

| Component | Memory | Calculation |
|-----------|--------|-------------|
| Texture atlas | 27 MB | 4096×4096 × 3 channels (compressed) |
| Active meshes | 200 MB | 128 chunks × 1.6MB avg |
| LOD meshes | 50 MB | Cached lower LOD versions |
| Shadow maps | 64 MB | 4 splits × 2048×2048 |
| **Total VRAM** | **~340 MB** | **Well under 1GB target** |

**System RAM**:
- Mesh generation buffers: 50 MB
- Chunk data storage: 100 MB
- Mesh queue/pooling: 20 MB
- **Total System RAM**: ~170 MB

### 9.4 Update Frequency

**Mesh Update Budget**:

| Update Type | Frequency | Time Budget |
|-------------|-----------|-------------|
| New chunk mesh | On demand | <20ms (async) |
| Block change update | Immediate | <5ms (priority queue) |
| LOD transition | Every 500ms | <10ms |
| Full remesh | Rarely | <100ms |

**Threading Budget**:
- **Mesh generation threads**: 2 threads
- **Max chunks per second**: 60 (1 per frame at 60 FPS)
- **Queue size**: Max 100 pending chunks

---

## 10. C# Code Examples

### 10.1 GreedyMeshing Class

```csharp
using Godot;
using System;
using System.Collections.Generic;

namespace Societies.Rendering
{
    /// <summary>
    /// Greedy meshing algorithm for voxel chunk optimization.
    /// Combines adjacent faces into larger quads to reduce triangle count.
    /// </summary>
    public class GreedyMeshing
    {
        private const int CHUNK_SIZE = 16;
        private const int CHUNK_HEIGHT = 256;
        
        private readonly bool[,,] _visited;
        
        public GreedyMeshing()
        {
            _visited = new bool[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
        }
        
        /// <summary>
        /// Generates optimized quads for a chunk face direction.
        /// </summary>
        public List<Quad> GenerateFaceQuads(ChunkData chunk, Direction faceDir)
        {
            var quads = new List<Quad>();
            Array.Clear(_visited, 0, _visited.Length);
            
            // Determine iteration order based on face direction
            var (outerLoop, middleLoop, innerLoop) = GetLoopOrder(faceDir);
            
            for (int o = 0; o < outerLoop.count; o++)
            {
                Array.Clear(_visited, 0, _visited.Length);
                
                for (int m = 0; m < middleLoop.count; m++)
                {
                    for (int i = 0; i < innerLoop.count; i++)
                    {
                        var (x, y, z) = GetCoordinates(outerLoop, middleLoop, innerLoop, o, m, i);
                        
                        if (_visited[x, y, z]) continue;
                        
                        var block = chunk.GetBlock(x, y, z);
                        if (block == BlockType.Air) continue;
                        
                        // Check if this face should be rendered
                        if (!ShouldRenderFace(chunk, x, y, z, faceDir)) continue;
                        
                        // Greedy expansion
                        var quad = ExpandQuad(chunk, x, y, z, faceDir, block, middleLoop, innerLoop, o, m, i);
                        quads.Add(quad);
                    }
                }
            }
            
            return quads;
        }
        
        private Quad ExpandQuad(ChunkData chunk, int x, int y, int z, 
            Direction faceDir, BlockType blockType, 
            LoopConfig middle, LoopConfig inner, int o, int m, int i)
        {
            int width = 1;
            int height = 1;
            
            // Expand in middle loop direction (width)
            while (CanExtend(chunk, faceDir, blockType, o, m, i + width, middle, inner))
            {
                width++;
                if (i + width >= inner.count) break;
            }
            
            // Expand in inner loop direction (height)
            bool canExpand = true;
            while (canExpand && m + height < middle.count)
            {
                // Check entire row
                for (int w = 0; w < width; w++)
                {
                    if (!CanExtend(chunk, faceDir, blockType, o, m + height, i + w, middle, inner))
                    {
                        canExpand = false;
                        break;
                    }
                }
                
                if (canExpand) height++;
            }
            
            // Mark visited
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    var (vx, vy, vz) = GetCoordinatesFromLoops(middle, inner, o, m + h, i + w);
                    _visited[vx, vy, vz] = true;
                }
            }
            
            return new Quad(
                new Vector3(x, y, z),
                new Vector2(width, height),
                faceDir,
                blockType
            );
        }
        
        private bool ShouldRenderFace(ChunkData chunk, int x, int y, int z, Direction dir)
        {
            int nx = x + dir.GetXOffset();
            int ny = y + dir.GetYOffset();
            int nz = z + dir.GetZOffset();
            
            // Check bounds
            if (nx < 0 || nx >= CHUNK_SIZE || 
                ny < 0 || ny >= CHUNK_HEIGHT || 
                nz < 0 || nz >= CHUNK_SIZE)
            {
                // Face at chunk boundary - check neighbor chunk
                return true; // Assume visible for now
            }
            
            var neighbor = chunk.GetBlock(nx, ny, nz);
            return !neighbor.IsOpaque || !neighbor.IsSolid;
        }
        
        private (LoopConfig, LoopConfig, LoopConfig) GetLoopOrder(Direction dir)
        {
            return dir switch
            {
                Direction.Up or Direction.Down => 
                    (new LoopConfig(1, CHUNK_HEIGHT), new LoopConfig(CHUNK_SIZE, 1), new LoopConfig(CHUNK_SIZE, 1)),
                Direction.North or Direction.South => 
                    (new LoopConfig(CHUNK_SIZE, 1), new LoopConfig(CHUNK_HEIGHT, 1), new LoopConfig(CHUNK_SIZE, 1)),
                Direction.East or Direction.West => 
                    (new LoopConfig(CHUNK_SIZE, 1), new LoopConfig(CHUNK_HEIGHT, 1), new LoopConfig(CHUNK_SIZE, 1)),
                _ => throw new ArgumentException("Invalid direction")
            };
        }
        
        private (int x, int y, int z) GetCoordinates(LoopConfig outer, LoopConfig middle, LoopConfig inner, int o, int m, int i)
        {
            return (outer.stride * o + middle.stride * m + inner.stride * i,
                    outer.axis == 1 ? o : middle.axis == 1 ? m : i,
                    outer.stride * o + middle.stride * m + inner.stride * i);
        }
    }
    
    public struct LoopConfig
    {
        public int count;
        public int stride;
        public int axis;
        
        public LoopConfig(int count, int stride)
        {
            this.count = count;
            this.stride = stride;
            this.axis = 0;
        }
    }
    
    public struct Quad
    {
        public Vector3 Position;
        public Vector2 Size;
        public Direction Face;
        public BlockType Block;
        
        public Quad(Vector3 pos, Vector2 size, Direction face, BlockType block)
        {
            Position = pos;
            Size = size;
            Face = face;
            Block = block;
        }
    }
}
```

### 10.2 Chunk Mesh Generation

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Rendering
{
    /// <summary>
    /// Generates ArrayMesh instances for voxel chunks using greedy meshing.
    /// </summary>
    public class ChunkMeshGenerator
    {
        private readonly GreedyMeshing _greedyMeshing;
        private readonly SurfaceTool _surfaceTool;
        private readonly TextureAtlas _atlas;
        
        public ChunkMeshGenerator()
        {
            _greedyMeshing = new GreedyMeshing();
            _surfaceTool = new SurfaceTool();
            _atlas = new TextureAtlas();
        }
        
        /// <summary>
        /// Generates a complete mesh for a chunk including all 6 face directions.
        /// </summary>
        public ArrayMesh GenerateMesh(ChunkData chunk, ChunkData[] neighbors)
        {
            _surfaceTool.Clear();
            _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            _surfaceTool.SetMaterial(VoxelMaterialManager.GetSharedMaterial());
            
            // Generate quads for each face direction
            foreach (Direction dir in System.Enum.GetValues<Direction>())
            {
                var quads = _greedyMeshing.GenerateFaceQuads(chunk, dir);
                
                foreach (var quad in quads)
                {
                    AddQuadToMesh(quad, chunk);
                }
            }
            
            // Optimize mesh
            _surfaceTool.GenerateNormals();
            _surfaceTool.GenerateTangents();
            _surfaceTool.Index();
            
            return _surfaceTool.Commit();
        }
        
        private void AddQuadToMesh(Quad quad, ChunkData chunk)
        {
            // Get world-space vertices
            var vertices = GetQuadVertices(quad);
            var normals = GetQuadNormals(quad.Face);
            var uvs = _atlas.GetUVs(quad.Block, quad.Face, quad.Size);
            
            // Add vertices
            for (int i = 0; i < 4; i++)
            {
                _surfaceTool.SetNormal(normals[i]);
                _surfaceTool.SetUV(uvs[i]);
                _surfaceTool.AddVertex(vertices[i]);
            }
            
            // Add indices for two triangles
            _surfaceTool.AddIndex(0);
            _surfaceTool.AddIndex(1);
            _surfaceTool.AddIndex(2);
            _surfaceTool.AddIndex(0);
            _surfaceTool.AddIndex(2);
            _surfaceTool.AddIndex(3);
        }
        
        private Vector3[] GetQuadVertices(Quad quad)
        {
            var basePos = quad.Position;
            var size = quad.Size;
            
            return quad.Face switch
            {
                Direction.Up => new[] {
                    basePos + new Vector3(0, 1, 0),
                    basePos + new Vector3(size.X, 1, 0),
                    basePos + new Vector3(size.X, 1, size.Y),
                    basePos + new Vector3(0, 1, size.Y)
                },
                Direction.Down => new[] {
                    basePos + new Vector3(0, 0, size.Y),
                    basePos + new Vector3(size.X, 0, size.Y),
                    basePos + new Vector3(size.X, 0, 0),
                    basePos + new Vector3(0, 0, 0)
                },
                Direction.North => new[] {
                    basePos + new Vector3(0, 0, size.Y),
                    basePos + new Vector3(size.X, 0, size.Y),
                    basePos + new Vector3(size.X, size.Y, size.Y),
                    basePos + new Vector3(0, size.Y, size.Y)
                },
                Direction.South => new[] {
                    basePos + new Vector3(size.X, 0, 0),
                    basePos + new Vector3(0, 0, 0),
                    basePos + new Vector3(0, size.Y, 0),
                    basePos + new Vector3(size.X, size.Y, 0)
                },
                Direction.East => new[] {
                    basePos + new Vector3(size.X, 0, size.Y),
                    basePos + new Vector3(size.X, 0, 0),
                    basePos + new Vector3(size.X, size.Y, 0),
                    basePos + new Vector3(size.X, size.Y, size.Y)
                },
                Direction.West => new[] {
                    basePos + new Vector3(0, 0, 0),
                    basePos + new Vector3(0, 0, size.Y),
                    basePos + new Vector3(0, size.Y, size.Y),
                    basePos + new Vector3(0, size.Y, 0)
                },
                _ => throw new System.ArgumentException("Invalid direction")
            };
        }
        
        private Vector3[] GetQuadNormals(Direction face)
        {
            var normal = face switch
            {
                Direction.Up => Vector3.Up,
                Direction.Down => Vector3.Down,
                Direction.North => Vector3.Forward,
                Direction.South => Vector3.Back,
                Direction.East => Vector3.Right,
                Direction.West => Vector3.Left,
                _ => Vector3.Up
            };
            
            return new[] { normal, normal, normal, normal };
        }
    }
}
```

### 10.3 Mesh Update Queue

```csharp
using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Societies.Rendering
{
    /// <summary>
    /// Thread-safe queue for chunk mesh generation with priority support.
    /// </summary>
    public class MeshUpdateQueue : Node
    {
        private readonly Queue<ChunkMeshJob> _highPriority = new();
        private readonly Queue<ChunkMeshJob> _normalPriority = new();
        private readonly Queue<ChunkMeshJob> _lowPriority = new();
        private readonly HashSet<Vector2I> _processing = new();
        private readonly object _lock = new();
        
        private Thread[] _workers;
        private bool _running = true;
        private int _maxWorkers = 2;
        
        [Signal]
        public delegate void MeshCompletedEventHandler(Vector2I chunkCoord, ArrayMesh mesh);
        
        public override void _Ready()
        {
            StartWorkers();
        }
        
        private void StartWorkers()
        {
            _workers = new Thread[_maxWorkers];
            
            for (int i = 0; i < _maxWorkers; i++)
            {
                _workers[i] = new Thread(WorkerThread);
                _workers[i].IsBackground = true;
                _workers[i].Start();
            }
        }
        
        private void WorkerThread()
        {
            var generator = new ChunkMeshGenerator();
            
            while (_running)
            {
                ChunkMeshJob job = null;
                
                lock (_lock)
                {
                    if (_highPriority.Count > 0)
                        job = _highPriority.Dequeue();
                    else if (_normalPriority.Count > 0)
                        job = _normalPriority.Dequeue();
                    else if (_lowPriority.Count > 0)
                        job = _lowPriority.Dequeue();
                }
                
                if (job != null)
                {
                    try
                    {
                        var mesh = generator.GenerateMesh(job.Chunk, job.Neighbors);
                        
                        // Emit signal on main thread
                        CallDeferred(nameof(EmitMeshCompleted), job.ChunkCoord, mesh);
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"Mesh generation failed for chunk {job.ChunkCoord}: {ex}");
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _processing.Remove(job.ChunkCoord);
                        }
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
        }
        
        private void EmitMeshCompleted(Vector2I coord, ArrayMesh mesh)
        {
            EmitSignal(SignalName.MeshCompleted, coord, mesh);
        }
        
        public void QueueChunk(ChunkData chunk, ChunkData[] neighbors, 
            Vector2I coord, Priority priority = Priority.Normal)
        {
            lock (_lock)
            {
                if (_processing.Contains(coord)) return;
                
                var job = new ChunkMeshJob
                {
                    Chunk = chunk,
                    Neighbors = neighbors,
                    ChunkCoord = coord,
                    Priority = priority
                };
                
                _processing.Add(coord);
                
                switch (priority)
                {
                    case Priority.High:
                        _highPriority.Enqueue(job);
                        break;
                    case Priority.Normal:
                        _normalPriority.Enqueue(job);
                        break;
                    case Priority.Low:
                        _lowPriority.Enqueue(job);
                        break;
                }
            }
        }
        
        public override void _ExitTree()
        {
            _running = false;
            
            foreach (var worker in _workers)
            {
                worker?.Join(1000);
            }
        }
        
        private class ChunkMeshJob
        {
            public ChunkData Chunk;
            public ChunkData[] Neighbors;
            public Vector2I ChunkCoord;
            public Priority Priority;
        }
    }
    
    public enum Priority
    {
        Low,
        Normal,
        High
    }
}
```

### 10.4 Complete Implementation Sample

```csharp
using Godot;
using System.Collections.Generic;

namespace Societies.Rendering
{
    /// <summary>
    /// Complete voxel world renderer managing chunks, LOD, and optimization.
    /// </summary>
    public partial class VoxelWorldRenderer : Node3D
    {
        [Export] public int RenderDistance { get; set; } = 16;
        [Export] public int MaxChunksPerFrame { get; set; } = 2;
        
        private Dictionary<Vector2I, ChunkRenderer> _renderedChunks = new();
        private MeshUpdateQueue _meshQueue;
        private ChunkMeshGenerator _meshGenerator;
        private Material _sharedMaterial;
        private Camera3D _camera;
        
        private Vector2I _currentChunkCoord;
        private int _chunksGeneratedThisFrame = 0;
        
        public override void _Ready()
        {
            _meshQueue = new MeshUpdateQueue();
            AddChild(_meshQueue);
            _meshQueue.MeshCompleted += OnMeshCompleted;
            
            _meshGenerator = new ChunkMeshGenerator();
            _sharedMaterial = VoxelMaterialManager.GetSharedMaterial();
            
            _camera = GetViewport().GetCamera3D();
        }
        
        public override void _Process(double delta)
        {
            _chunksGeneratedThisFrame = 0;
            
            // Update current chunk position
            var playerPos = _camera.GlobalPosition;
            _currentChunkCoord = new Vector2I(
                Mathf.FloorToInt(playerPos.X / 16),
                Mathf.FloorToInt(playerPos.Z / 16)
            );
            
            // Update visible chunks
            UpdateVisibleChunks();
            
            // Clean up distant chunks
            CleanupDistantChunks();
        }
        
        private void UpdateVisibleChunks()
        {
            var visibleChunks = GetVisibleChunkCoords();
            
            foreach (var coord in visibleChunks)
            {
                if (!_renderedChunks.ContainsKey(coord))
                {
                    LoadChunk(coord);
                }
            }
        }
        
        private void LoadChunk(Vector2I coord)
        {
            // Get chunk data from world
            var chunk = WorldManager.Instance.GetChunk(coord);
            if (chunk == null) return;
            
            // Get neighbor chunks
            var neighbors = new[]
            {
                WorldManager.Instance.GetChunk(coord + new Vector2I(-1, 0)),
                WorldManager.Instance.GetChunk(coord + new Vector2I(1, 0)),
                WorldManager.Instance.GetChunk(coord + new Vector2I(0, -1)),
                WorldManager.Instance.GetChunk(coord + new Vector2I(0, 1))
            };
            
            // Create renderer
            var renderer = new ChunkRenderer(coord, _sharedMaterial);
            AddChild(renderer);
            _renderedChunks[coord] = renderer;
            
            // Queue mesh generation
            var priority = GetChunkPriority(coord);
            _meshQueue.QueueChunk(chunk, neighbors, coord, priority);
        }
        
        private void OnMeshCompleted(Vector2I coord, ArrayMesh mesh)
        {
            if (_renderedChunks.TryGetValue(coord, out var renderer))
            {
                renderer.UpdateMesh(mesh);
            }
            else
            {
                mesh.Dispose(); // Chunk was unloaded while generating
            }
        }
        
        private void CleanupDistantChunks()
        {
            var toRemove = new List<Vector2I>();
            
            foreach (var kvp in _renderedChunks)
            {
                if (kvp.Key.DistanceTo(_currentChunkCoord) > RenderDistance + 2)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (var coord in toRemove)
            {
                var renderer = _renderedChunks[coord];
                renderer.ClearMesh();
                renderer.QueueFree();
                _renderedChunks.Remove(coord);
            }
        }
        
        private List<Vector2I> GetVisibleChunkCoords()
        {
            var coords = new List<Vector2I>();
            
            for (int x = -RenderDistance; x <= RenderDistance; x++)
            {
                for (int z = -RenderDistance; z <= RenderDistance; z++)
                {
                    // Circular render distance
                    if (x * x + z * z > RenderDistance * RenderDistance) continue;
                    
                    coords.Add(_currentChunkCoord + new Vector2I(x, z));
                }
            }
            
            return coords;
        }
        
        private Priority GetChunkPriority(Vector2I coord)
        {
            float dist = coord.DistanceTo(_currentChunkCoord);
            
            if (dist < 4) return Priority.High;
            if (dist < RenderDistance * 0.7f) return Priority.Normal;
            return Priority.Low;
        }
    }
    
    public partial class ChunkRenderer : Node3D
    {
        private MeshInstance3D _meshInstance;
        private Vector2I _coord;
        
        public ChunkRenderer(Vector2I coord, Material material)
        {
            _coord = coord;
            Name = $"Chunk_{coord.X}_{coord.Y}";
            Position = new Vector3(coord.X * 16, 0, coord.Y * 16);
            
            _meshInstance = new MeshInstance3D();
            _meshInstance.SetSurfaceOverrideMaterial(0, material);
            _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            _meshInstance.VisibilityAabb = new Aabb(
                new Vector3(0, 0, 0),
                new Vector3(16, 256, 16)
            );
            
            AddChild(_meshInstance);
        }
        
        public void UpdateMesh(ArrayMesh mesh)
        {
            _meshInstance.Mesh = mesh;
        }
        
        public void ClearMesh()
        {
            if (_meshInstance.Mesh != null)
            {
                _meshInstance.Mesh.Dispose();
                _meshInstance.Mesh = null;
            }
        }
    }
}
```

---

## 11. Implementation Roadmap

### Phase 1: Basic Meshing (Week 1-2)

**Goals**:
- [ ] Naive face generation (no culling)
- [ ] Basic SurfaceTool integration
- [ ] Single chunk rendering
- [ ] Static mesh display

**Success Criteria**:
- Render single 16×16×256 chunk at 60 FPS
- Basic camera movement
- No optimization required

### Phase 2: Face Culling (Week 2-3)

**Goals**:
- [ ] Implement face culling algorithm
- [ ] Adjacent chunk checking
- [ ] 80% triangle reduction
- [ ] Multi-chunk support

**Success Criteria**:
- 64 chunks rendered at 60 FPS
- Visible triangle count <100K
- Chunk boundaries seamless

### Phase 3: Greedy Meshing (Week 3-4)

**Goals**:
- [ ] Greedy meshing implementation
- [ ] Quad consolidation
- [ ] Texture atlas integration
- [ ] Draw call reduction

**Success Criteria**:
- 94% triangle reduction vs naive
- <12 draw calls for 64 chunks
- Texture atlas working

### Phase 4: Threading (Week 4-5)

**Goals**:
- [ ] Background mesh generation
- [ ] Mesh update queue
- [ ] Priority system
- [ ] Smooth chunk loading

**Success Criteria**:
- No frame drops during chunk updates
- <16ms mesh generation time
- Seamless chunk streaming

### Phase 5: LOD System (Week 5-6)

**Goals**:
- [ ] 4-level LOD implementation
- [ ] Distance-based switching
- [ ] LOD mesh generation
- [ ] Transition handling

**Success Criteria**:
- 128 chunks at 60 FPS with LOD
- <2.1M triangles total
- No visible popping

### Phase 6: Optimization (Week 6-7)

**Goals**:
- [ ] Frustum culling
- [ ] Occlusion culling
- [ ] GPU instancing for vegetation
- [ ] Dynamic render distance

**Success Criteria**:
- <180 draw calls for 128 chunks
- <340 MB VRAM usage
- Adaptive quality maintains 60 FPS

### Phase 7: Visual Polish (Week 7-8)

**Goals**:
- [ ] Block breaking animations
- [ ] Particle effects
- [ ] Ambient occlusion
- [ ] Shadow optimization

**Success Criteria**:
- Break animations at 60 FPS
- AO integration
- Shadow quality vs performance balanced

---

## Document Information

### Version History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2026-02-01 | Initial document creation | AI Assistant |

### Cross-References

- [Minecraft Voxel Research](minecraft-voxel-research.md) - Reference implementation study
- [Performance & Scalability](04-performance-scalability.md) - System-wide performance targets
- [Client-Server Architecture](02-client-server-architecture.md) - Network chunk transmission

### Open Questions

1. **Texture Atlas Size**: 4096×4096 sufficient for MVP block types? Need 8192×8192 for post-MVP?
2. **Multi-texture Support**: Separate atlases for normal/metallic/roughness or packed channels?
3. **Shader Complexity**: Custom shader for advanced effects vs StandardMaterial3D performance?
4. **Mobile Support**: LOD adjustments needed for mobile GPUs?

### Research Sources

- Godot 4.3 Documentation - Rendering and Mesh classes
- "Efficient Voxel Rendering" - Various game dev articles
- Minecraft technical documentation - Chunk meshing strategies
- Godot 4 C# API reference - SurfaceTool, ArrayMesh, MultiMesh

---

**End of Document**
