# Session 1: Physics & Collision Detection

> **Navigation**: [Index]([AGENTS-READ-FIRST]-index.md) | [Previous: Error Handling](11-error-handling.md) | [Next: Appendix](07-appendices.md)
> 
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

## Executive Summary

This document specifies the physics and collision detection architecture for Societies' voxel-based world. The system leverages Godot 4.x's physics engine with custom voxel-aware collision optimization to support:

- **Block Size**: 1m³ voxels forming the terrain
- **Chunk Size**: 16×16×256 blocks (65,536 voxels per chunk)
- **Physics Bodies**: CharacterBody3D for agents/players, RigidBody3D for debris/vehicles
- **World Scale**: 0.5 km² MVP world with ~16,777,216 potential voxel collision surfaces

**Key Design Decisions**:

1. **Per-chunk collision bodies**: Each chunk generates a `ConcavePolygonShape3D` for its visible surface voxels, not every block
2. **Hierarchical collision system**: Spatial hash → AABB culling → per-voxel precision for optimal performance
3. **Server-authoritative physics**: Server validates all movement; clients predict locally
4. **Physics LOD**: Distant chunks use simplified collision; nearby chunks use full precision
5. **Sleeping optimization**: Static terrain collision bodies sleep until modified

**Performance Targets**:
- Collision query budget: <0.5ms per tick for 8 players + 20 agents
- Chunk collision generation: <10ms for 16×16×256 chunk
- Raycast performance: <0.1ms for block interaction at 20 TPS
- Memory overhead: ~2KB per chunk collision mesh (surface-only)

**Godot 4 Physics Integration**:
- Uses Godot Physics (not Bullet) for determinism and server compatibility
- Physics ticks at 20 TPS (50ms) synchronized with simulation tick
- Headless server runs physics without rendering (40-60% CPU savings)
- Multithreaded physics enabled for broad phase (Narrow phase single-threaded)

---

## 1. Collision Mesh Generation

### ConcavePolygonShape3D for Chunks

Voxel worlds contain millions of potential collision surfaces. Creating individual collision shapes for each block is prohibitively expensive. Instead, we generate optimized `ConcavePolygonShape3D` meshes representing only the visible surfaces of each chunk.

**Collision Mesh Strategy**:

```csharp
public class ChunkCollisionGenerator
{
    /// <summary>
    /// Generates collision mesh for a chunk's visible surfaces only.
    /// Reduces collision faces from 65,536 (all voxels) to ~2,000-5,000 (surfaces).
    /// </summary>
    public static ConcavePolygonShape3D GenerateCollisionMesh(Chunk chunk)
    {
        var faces = new List<Vector3>();
        
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int y = 0; y < Chunk.SizeY; y++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    if (block.IsAir) continue;
                    
                    // Only add faces exposed to air (visible surfaces)
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.Top))
                        AddFaceVertices(faces, x, y, z, BlockFace.Top);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.Bottom))
                        AddFaceVertices(faces, x, y, z, BlockFace.Bottom);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.North))
                        AddFaceVertices(faces, x, y, z, BlockFace.North);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.South))
                        AddFaceVertices(faces, x, y, z, BlockFace.South);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.East))
                        AddFaceVertices(faces, x, y, z, BlockFace.East);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.West))
                        AddFaceVertices(faces, x, y, z, BlockFace.West);
                }
            }
        }
        
        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces.ToArray());
        return shape;
    }
}
```

**Performance Impact**:

| Approach | Collision Faces (per chunk) | Memory | Query Performance |
|----------|---------------------------|--------|-------------------|
| Individual box per block | 393,216 (6 faces × 65,536) | ~12 MB | O(n) - unusable |
| Greedy meshing | 2,000-8,000 | ~150 KB | O(log n) |
| Surface-only (our approach) | 3,000-10,000 | ~200 KB | O(log n) |
| LOD simplified | 500-2,000 | ~50 KB | O(log n) - distant |

### Simplified Collision Shapes

For distant chunks (>100m from any player/agent), collision meshes are simplified:

1. **Heightmap approximation**: Replace full 3D collision with 2.5D heightmap
2. **Reduced resolution**: Merge adjacent surfaces, reducing face count by 50-70%
3. **AABB-only**: Extreme distances use only bounding box checks (no per-voxel precision)

**Collision LOD Levels**:

| Distance | Detail Level | Face Count | Use Case |
|----------|--------------|------------|----------|
| 0-50m | Full | 100% | Player movement, mining |
| 50-100m | Medium | 50% | Agent pathfinding |
| 100-200m | Low | 25% | Projectile collision |
| >200m | AABB only | 0% | Broad phase only |

### Collision Mesh Update Strategy

When blocks are modified (mining, building), collision meshes must update:

1. **Immediate updates**: Modified chunk regenerates collision mesh synchronously (<10ms)
2. **Neighbor updates**: Adjacent chunks regenerate asynchronously (queue for next tick)
3. **Dirty tracking**: Only modified chunks regenerate; unchanged chunks cached
4. **Batch updates**: Multiple block changes batched into single regeneration

```csharp
public class CollisionUpdateManager
{
    private HashSet<ChunkCoord> _dirtyChunks = new();
    private HashSet<ChunkCoord> _neighborDirtyChunks = new();
    
    public void OnBlocksModified(IEnumerable<BlockCoord> modifiedBlocks)
    {
        foreach (var block in modifiedBlocks)
        {
            var chunkCoord = block.ToChunkCoord();
            _dirtyChunks.Add(chunkCoord);
            
            // Mark neighbors if block is on chunk edge
            if (IsChunkEdge(block))
            {
                AddNeighborChunks(chunkCoord, _neighborDirtyChunks);
            }
        }
    }
    
    public void ProcessUpdates()
    {
        // Process critical chunks immediately
        foreach (var coord in _dirtyChunks)
        {
            RegenerateCollisionMesh(coord, immediate: true);
        }
        
        // Process neighbors next tick (can be deferred)
        foreach (var coord in _neighborDirtyChunks)
        {
            QueueForNextTick(coord);
        }
        
        _dirtyChunks.Clear();
        _neighborDirtyChunks.Clear();
    }
}
```

### Per-Chunk Collision Bodies

Each chunk has a dedicated `StaticBody3D` with collision shape:

```csharp
public class ChunkCollisionBody : StaticBody3D
{
    public ChunkCoord Coord { get; private set; }
    public CollisionShape3D ShapeNode { get; private set; }
    public ConcavePolygonShape3D CollisionShape { get; private set; }
    
    public void Initialize(Chunk chunk)
    {
        Coord = chunk.Coord;
        
        // Generate collision mesh
        CollisionShape = ChunkCollisionGenerator.GenerateCollisionMesh(chunk);
        
        // Create collision node
        ShapeNode = new CollisionShape3D();
        ShapeNode.Shape = CollisionShape;
        AddChild(ShapeNode);
        
        // Configure physics properties
        CollisionLayer = PhysicsLayers.Terrain;
        CollisionMask = PhysicsLayers.Entities | PhysicsLayers.Projectiles | PhysicsLayers.Vehicles;
        
        // Sleep until modified (optimization)
        CanSleep = true;
        
        // Set position in world
        GlobalPosition = chunk.WorldPosition;
    }
    
    public void UpdateCollisionMesh(Chunk chunk)
    {
        // Replace shape (Godot handles this efficiently)
        var newShape = ChunkCollisionGenerator.GenerateCollisionMesh(chunk);
        ShapeNode.Shape = newShape;
        CollisionShape = newShape;
        
        // Wake up temporarily for updates
        CanSleep = false;
    }
}
```

---

## 2. Hierarchical Collision System

### Broad Phase: Spatial Hash Grid

The world is divided into a spatial hash grid for efficient entity queries:

```csharp
public class SpatialHashGrid
{
    private const int CellSize = 32; // meters
    private Dictionary<Vector3I, HashSet<PhysicsBody3D>> _grid = new();
    
    public void Insert(PhysicsBody3D body, AABB bounds)
    {
        var minCell = WorldToCell(bounds.Position);
        var maxCell = WorldToCell(bounds.End);
        
        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    var cell = new Vector3I(x, y, z);
                    if (!_grid.ContainsKey(cell))
                        _grid[cell] = new HashSet<PhysicsBody3D>();
                    _grid[cell].Add(body);
                }
            }
        }
    }
    
    public IEnumerable<PhysicsBody3D> Query(AABB bounds)
    {
        var result = new HashSet<PhysicsBody3D>();
        var minCell = WorldToCell(bounds.Position);
        var maxCell = WorldToCell(bounds.End);
        
        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    var cell = new Vector3I(x, y, z);
                    if (_grid.TryGetValue(cell, out var bodies))
                        result.UnionWith(bodies);
                }
            }
        }
        
        return result;
    }
    
    private Vector3I WorldToCell(Vector3 worldPos)
    {
        return new Vector3I(
            Mathf.FloorToInt(worldPos.X / CellSize),
            Mathf.FloorToInt(worldPos.Y / CellSize),
            Mathf.FloorToInt(worldPos.Z / CellSize)
        );
    }
}
```

**Performance**: Spatial hash reduces O(n²) entity pair checks to O(n) in practice, critical for 100+ moving entities.

### Mid Phase: AABB Culling Per Chunk

After broad phase, AABB (Axis-Aligned Bounding Box) tests quickly reject non-colliding pairs:

```csharp
public bool AABBIntersects(AABB a, AABB b)
{
    return (a.Position.X <= b.End.X && a.End.X >= b.Position.X) &&
           (a.Position.Y <= b.End.Y && a.End.Y >= b.Position.Y) &&
           (a.Position.Z <= b.End.Z && a.End.Z >= b.Position.Z);
}
```

**Chunk-Level AABB Culling**:

Each chunk maintains an AABB. Before checking individual voxels:

1. Test entity AABB against chunk AABB
2. If no intersection, skip entire chunk
3. If intersection, proceed to narrow phase

### Narrow Phase: Per-Voxel Collision

For precise block interaction (mining, placing), raycast against individual voxels:

```csharp
public class VoxelRaycaster
{
    /// <summary>
    /// Casts ray against voxel grid with high precision.
    /// Returns hit block coordinate and face normal.
    /// </summary>
    public RaycastHit RaycastVoxels(Vector3 origin, Vector3 direction, float maxDistance)
    {
        // DDA (Digital Differential Analysis) algorithm for voxel traversal
        var currentBlock = WorldToBlock(origin);
        var step = new Vector3I(
            direction.X > 0 ? 1 : -1,
            direction.Y > 0 ? 1 : -1,
            direction.Z > 0 ? 1 : -1
        );
        
        var tMax = new Vector3(
            (BlockToWorld(currentBlock + step.X).X - origin.X) / direction.X,
            (BlockToWorld(currentBlock + step.Y).Y - origin.Y) / direction.Y,
            (BlockToWorld(currentBlock + step.Z).Z - origin.Z) / direction.Z
        );
        
        var tDelta = new Vector3(
            Mathf.Abs(1.0f / direction.X),
            Mathf.Abs(1.0f / direction.Y),
            Mathf.Abs(1.0f / direction.Z)
        );
        
        float distance = 0;
        while (distance < maxDistance)
        {
            // Check current block
            var block = World.GetBlock(currentBlock);
            if (!block.IsAir && block.IsSolid)
            {
                return new RaycastHit
                {
                    BlockCoord = currentBlock,
                    Distance = distance,
                    Normal = GetFaceNormal(currentBlock, origin, direction)
                };
            }
            
            // Step to next voxel
            if (tMax.X < tMax.Y && tMax.X < tMax.Z)
            {
                currentBlock.X += step.X;
                distance = tMax.X;
                tMax.X += tDelta.X;
            }
            else if (tMax.Y < tMax.Z)
            {
                currentBlock.Y += step.Y;
                distance = tMax.Y;
                tMax.Y += tDelta.Y;
            }
            else
            {
                currentBlock.Z += step.Z;
                distance = tMax.Z;
                tMax.Z += tDelta.Z;
            }
        }
        
        return null; // No hit
    }
}
```

### Optimization Layers Summary

| Layer | Algorithm | Entities | Time Budget |
|-------|-----------|----------|-------------|
| Broad | Spatial hash grid | 500-1000 | 0.1ms |
| Mid | AABB culling | 100-200 | 0.2ms |
| Narrow | Per-voxel raycast | 8-20 | 0.5ms |

---

## 3. CharacterBody3D on Terrain

### Player Controller Implementation

Players and AI agents use `CharacterBody3D` for movement on voxel terrain:

```csharp
public partial class VoxelCharacterController : CharacterBody3D
{
    [Export] public float WalkSpeed { get; set; } = 5.0f;
    [Export] public float SprintSpeed { get; set; } = 8.0f;
    [Export] public float JumpVelocity { get; set; } = 8.5f;
    [Export] public float StepHeight { get; set; } = 0.6f; // 0.6m step up
    [Export] public float MaxSlopeAngle { get; set; } = 45.0f;
    [Export] public bool SnapToVoxelGrid { get; set; } = false;
    
    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    private bool _isSprinting = false;
    
    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;
        
        // Apply gravity
        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;
        
        // Handle jump
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;
        
        // Get input direction
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = new Vector3(inputDir.X, 0, inputDir.Y).Normalized();
        direction = direction.Rotated(Vector3.Up, GlobalRotation.Y);
        
        // Apply movement
        float speed = _isSprinting ? SprintSpeed : WalkSpeed;
        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
        }
        
        Velocity = velocity;
        
        // Move with slide and step handling
        MoveAndSlideWithStep(delta);
        
        // Optional: Snap to voxel grid for precision
        if (SnapToVoxelGrid && IsOnFloor())
            SnapToGrid();
    }
    
    private void MoveAndSlideWithStep(double delta)
    {
        // Standard move and slide
        MoveAndSlide();
        
        // Handle stepping up
        if (IsOnFloor() && Velocity.Length() > 0.1f)
        {
            TryStepUp();
        }
    }
    
    private void TryStepUp()
    {
        // Test if we can step up
        var testPos = GlobalPosition + new Vector3(0, StepHeight, 0);
        var testMotion = Velocity.Normalized() * 0.1f;
        
        var query = PhysicsRayQueryParameters3D.Create(testPos, testPos + testMotion);
        query.CollisionMask = (uint)PhysicsLayers.Terrain;
        
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        
        if (result.Count == 0)
        {
            // No obstacle, can step up
            GlobalPosition += new Vector3(0, StepHeight, 0);
        }
    }
}
```

### Ground Detection

Accurate ground detection is critical for jump mechanics and slope handling:

```csharp
public bool IsOnGround(float checkDistance = 0.1f)
{
    var from = GlobalPosition;
    var to = from + new Vector3(0, -checkDistance, 0);
    
    var query = PhysicsRayQueryParameters3D.Create(from, to);
    query.CollisionMask = (uint)PhysicsLayers.Terrain;
    query.Exclude = new Array<Rid> { GetRid() };
    
    var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    
    if (result.Count > 0)
    {
        var normal = (Vector3)result["normal"];
        float slopeAngle = Mathf.RadToDeg(Mathf.Acos(normal.Dot(Vector3.Up)));
        return slopeAngle <= MaxSlopeAngle;
    }
    
    return false;
}
```

### Slope Handling

Characters slide down steep slopes automatically:

```csharp
private void ApplySlopeSliding(float delta)
{
    if (IsOnFloor())
    {
        var floorNormal = GetFloorNormal();
        float slopeAngle = Mathf.RadToDeg(Mathf.Acos(floorNormal.Dot(Vector3.Up)));
        
        if (slopeAngle > MaxSlopeAngle)
        {
            // Slide down slope
            var slideDirection = new Vector3(floorNormal.X, -floorNormal.Y, floorNormal.Z).Normalized();
            var slideVelocity = slideDirection * (_gravity * Mathf.Sin(Mathf.DegToRad(slopeAngle)));
            Velocity += slideVelocity * delta;
        }
    }
}
```

### Step Height

Characters can automatically step up small obstacles (0.6m default):

```csharp
private bool TryAutoStep(Vector3 moveDirection)
{
    if (moveDirection.Length() < 0.01f) return false;
    
    // Cast forward at step height
    var from = GlobalPosition + new Vector3(0, StepHeight, 0);
    var to = from + moveDirection.Normalized() * 0.5f;
    
    var query = PhysicsRayQueryParameters3D.Create(from, to);
    query.CollisionMask = (uint)PhysicsLayers.Terrain;
    
    var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    
    if (result.Count > 0)
    {
        // Obstacle found at step height, check if we can step over it
        var hitPoint = (Vector3)result["position"];
        var heightDiff = hitPoint.Y - GlobalPosition.Y;
        
        if (heightDiff <= StepHeight && heightDiff > 0.1f)
        {
            // Can step up
            GlobalPosition = new Vector3(
                GlobalPosition.X,
                hitPoint.Y,
                GlobalPosition.Z
            );
            return true;
        }
    }
    
    return false;
}
```

### Snap to Voxel Grid Option

For precision building and mining, characters can optionally snap to grid:

```csharp
private void SnapToGrid()
{
    // Snap position to 0.5m grid (center of voxel)
    var snappedX = Mathf.Snapped(GlobalPosition.X, 0.5f);
    var snappedZ = Mathf.Snapped(GlobalPosition.Z, 0.5f);
    
    // Only adjust Y if we're very close to a voxel boundary
    var yDiff = GlobalPosition.Y % 1.0f;
    var snappedY = yDiff < 0.1f || yDiff > 0.9f 
        ? Mathf.Snapped(GlobalPosition.Y, 1.0f) 
        : GlobalPosition.Y;
    
    GlobalPosition = new Vector3(snappedX, snappedY, snappedZ);
}
```

---

## 4. RigidBody Interaction

### Block Breaking Creates Physics Debris

When blocks are destroyed, they spawn physics-enabled debris (Eco-style rubble):

```csharp
public class BlockDestructionSystem
{
    [Export] public int MaxDebrisPieces { get; set; } = 50; // Per chunk
    [Export] public float DebrisLifetime { get; set; } = 30.0f; // Seconds
    
    public void OnBlockDestroyed(BlockCoord coord, BlockType blockType)
    {
        // Spawn debris based on block material
        int debrisCount = GetDebrisCount(blockType);
        
        for (int i = 0; i < debrisCount; i++)
        {
            var debris = CreateDebrisPiece(blockType);
            debris.GlobalPosition = BlockToWorld(coord) + RandomOffset();
            debris.LinearVelocity = RandomVelocity();
            
            // Add to scene
            GetTree().Root.AddChild(debris);
            
            // Auto-cleanup after lifetime
            GetTree().CreateTimer(DebrisLifetime).Timeout += () => debris.QueueFree();
        }
    }
    
    private RigidBody3D CreateDebrisPiece(BlockType blockType)
    {
        var debris = new RigidBody3D();
        
        // Create small cube mesh
        var mesh = new BoxMesh();
        mesh.Size = new Vector3(0.3f, 0.3f, 0.3f); // 30cm rubble pieces
        
        var meshInstance = new MeshInstance3D();
        meshInstance.Mesh = mesh;
        meshInstance.MaterialOverride = GetMaterialForBlock(blockType);
        debris.AddChild(meshInstance);
        
        // Collision
        var shape = new BoxShape3D();
        shape.Size = mesh.Size;
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        debris.AddChild(collision);
        
        // Physics properties
        debris.Mass = 0.5f; // 500g rubble
        debris.CanSleep = true;
        debris.Sleeping = false;
        
        // Disable continuous collision detection for small debris
        debris.CcdMode = RigidBody3D.CCDMode.Disabled;
        
        // Collision layers
        debris.CollisionLayer = (uint)PhysicsLayers.Debris;
        debris.CollisionMask = (uint)(PhysicsLayers.Terrain | PhysicsLayers.Debris | PhysicsLayers.Entities);
        
        return debris;
    }
}
```

### Rubble Physics (Eco-Style)

Debris interacts with terrain and settles naturally:

**Physics Settings for Rubble**:
- **Mass**: 0.5-2.0 kg depending on material
- **Restitution (bounciness)**: 0.1-0.3 (stone bounces less than wood)
- **Friction**: 0.6-0.9 (rough terrain stops debris quickly)
- **Linear dampening**: 0.1-0.5 (slows horizontal movement)
- **Angular dampening**: 0.5-1.0 (stops tumbling)

### Collision with Terrain

Debris collides with terrain using the chunk collision bodies:

```csharp
public void SetupDebrisCollision(RigidBody3D debris)
{
    // Debris collides with terrain layer
    debris.CollisionMask |= (uint)PhysicsLayers.Terrain;
    
    // Contact monitoring for sound effects
    debris.ContactMonitor = true;
    debris.MaxContactsReported = 1;
    debris.BodyEntered += (body) => OnDebrisImpact(debris, body);
}

private void OnDebrisImpact(RigidBody3D debris, Node body)
{
    var velocity = debris.LinearVelocity.Length();
    
    if (velocity > 2.0f)
    {
        // Play impact sound based on velocity
        var intensity = Mathf.Clamp(velocity / 10.0f, 0.0f, 1.0f);
        AudioManager.PlayImpactSound(debris.GlobalPosition, intensity);
    }
    
    // Put to sleep if nearly stopped
    if (velocity < 0.1f && debris.AngularVelocity.Length() < 0.1f)
    {
        debris.Sleeping = true;
    }
}
```

### Sleeping Optimization for Static Objects

Critical performance optimization - sleeping bodies use minimal CPU:

```csharp
public class DebrisManager
{
    private List<RigidBody3D> _debris = new();
    private double _sleepCheckTimer = 0;
    private const double SleepCheckInterval = 1.0; // Check every second
    
    public override void _Process(double delta)
    {
        _sleepCheckTimer += delta;
        
        if (_sleepCheckTimer >= SleepCheckInterval)
        {
            _sleepCheckTimer = 0;
            ForceSleepCheck();
        }
    }
    
    private void ForceSleepCheck()
    {
        foreach (var debris in _debris)
        {
            if (!debris.Sleeping)
            {
                // Force sleep if nearly stationary
                if (debris.LinearVelocity.Length() < 0.05f && 
                    debris.AngularVelocity.Length() < 0.05f)
                {
                    debris.Sleeping = true;
                }
            }
        }
    }
    
    public void CleanupSleptDebris()
    {
        // Remove debris that has been sleeping for a while
        foreach (var debris in _debris.ToList())
        {
            if (debris.Sleeping && !IsPlayerNearby(debris.GlobalPosition, 10.0f))
            {
                // Fade out and remove
                FadeAndRemove(debris);
            }
        }
    }
}
```

**Sleep Thresholds**:

| Body Type | Linear Sleep Threshold | Angular Sleep Threshold | Max Sleep Time |
|-----------|----------------------|------------------------|----------------|
| Small debris (rubble) | 0.1 m/s | 0.1 rad/s | 60s |
| Medium objects | 0.5 m/s | 0.5 rad/s | 300s |
| Vehicles | 1.0 m/s | 1.0 rad/s | Never (persistent) |

---

## 5. Entity vs Voxel Collision

### Raycasting for Block Interaction

Mining and building require precise raycast against voxels:

```csharp
public class BlockInteractionSystem
{
    [Export] public float ReachDistance { get; set; } = 5.0f; // 5 meters
    [Export] public float MiningRange { get; set; } = 4.0f;
    
    /// <summary>
    /// Casts ray from camera/player to find target block for mining.
    /// </summary>
    public BlockRaycastResult RaycastForMining(Vector3 origin, Vector3 direction)
    {
        var result = VoxelRaycaster.RaycastVoxels(origin, direction, MiningRange);
        
        if (result != null)
        {
            return new BlockRaycastResult
            {
                BlockCoord = result.BlockCoord,
                HitPoint = origin + direction * result.Distance,
                FaceNormal = result.Normal,
                Block = World.GetBlock(result.BlockCoord),
                Distance = result.Distance
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// Casts ray for block placement (finds adjacent empty space).
    /// </summary>
    public BlockRaycastResult RaycastForPlacement(Vector3 origin, Vector3 direction)
    {
        var result = RaycastForMining(origin, direction);
        
        if (result != null)
        {
            // Calculate placement position (adjacent to hit face)
            var placementCoord = result.BlockCoord + Vector3I.From(result.FaceNormal);
            
            return new BlockRaycastResult
            {
                BlockCoord = placementCoord,
                HitPoint = BlockToWorld(placementCoord),
                FaceNormal = result.FaceNormal,
                Block = World.GetBlock(placementCoord),
                Distance = result.Distance,
                IsPlacement = true
            };
        }
        
        return null;
    }
}
```

### Block Placement Validation

Before placing a block, validate no entities are blocking:

```csharp
public bool CanPlaceBlock(BlockCoord coord, BlockType blockType)
{
    // Check if space is empty
    if (!World.GetBlock(coord).IsAir)
        return false;
    
    // Check for entities in the space
    var blockAABB = new AABB(
        BlockToWorld(coord),
        new Vector3(1, 1, 1)
    );
    
    // Query spatial hash for entities
    var nearbyEntities = SpatialHash.Query(blockAABB.Grow(0.1f));
    
    foreach (var entity in nearbyEntities)
    {
        if (entity is PhysicsBody3D body)
        {
            // Check AABB overlap
            var entityAABB = body.GetAabb();
            if (blockAABB.Intersects(entityAABB))
            {
                return false; // Entity blocking
            }
        }
    }
    
    // Check for supporting block below (for gravity-affected blocks)
    if (blockType.RequiresSupport)
    {
        var below = coord + new Vector3I(0, -1, 0);
        if (World.GetBlock(below).IsAir)
            return false;
    }
    
    return true;
}
```

### Mining Collision Checks

Ensure mining doesn't expose players to danger (falling, etc.):

```csharp
public MiningValidationResult ValidateMining(BlockCoord coord, Node3D miner)
{
    var result = new MiningValidationResult();
    
    // Check if miner can reach
    var distance = miner.GlobalPosition.DistanceTo(BlockToWorld(coord));
    if (distance > MiningRange)
    {
        result.IsValid = false;
        result.Reason = "Block too far away";
        return result;
    }
    
    // Check for falling hazard
    var below = coord + new Vector3I(0, -1, 0);
    if (!World.GetBlock(below).IsSolid)
    {
        result.Warnings.Add("Mining will create hole");
    }
    
    // Check for structural support
    var above = coord + new Vector3I(0, 1, 0);
    if (World.GetBlock(above).IsSolid && !World.GetBlock(above).IsGravityAffected)
    {
        // Block above is supported, safe to mine
    }
    else if (World.GetBlock(above).IsSolid)
    {
        result.Warnings.Add("Mining may cause collapse");
    }
    
    // Check ownership/laws (server-authoritative)
    if (!HasMiningRights(miner, coord))
    {
        result.IsValid = false;
        result.Reason = "No mining rights";
        return result;
    }
    
    result.IsValid = true;
    return result;
}
```

### Custom Entity Shapes on Terrain

Different entity types have custom collision shapes:

```csharp
public enum EntityShapeType
{
    Humanoid,    // Capsule: 0.6m radius, 1.8m height
    AnimalSmall, // Capsule: 0.3m radius, 0.8m height
    AnimalLarge, // Capsule: 0.8m radius, 2.0m height
    Vehicle,     // Box: varies by vehicle type
    Projectile,  // Sphere: small radius
    Item,        // Box: 0.3m cube
    Building     // Custom: matches building footprint
}

public static class EntityShapeFactory
{
    public static CollisionShape3D CreateShape(EntityShapeType type)
    {
        return type switch
        {
            EntityShapeType.Humanoid => CreateCapsule(0.6f, 1.8f),
            EntityShapeType.AnimalSmall => CreateCapsule(0.3f, 0.8f),
            EntityShapeType.AnimalLarge => CreateCapsule(0.8f, 2.0f),
            EntityShapeType.Vehicle => CreateBox(new Vector3(2.5f, 1.5f, 4.0f)),
            EntityShapeType.Projectile => CreateSphere(0.1f),
            EntityShapeType.Item => CreateBox(new Vector3(0.3f, 0.3f, 0.3f)),
            _ => throw new ArgumentException($"Unknown shape type: {type}")
        };
    }
    
    private static CollisionShape3D CreateCapsule(float radius, float height)
    {
        var shape = new CapsuleShape3D();
        shape.Radius = radius;
        shape.Height = height - (radius * 2); // Total height includes caps
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, height / 2, 0); // Center on feet
        
        return collision;
    }
    
    private static CollisionShape3D CreateBox(Vector3 size)
    {
        var shape = new BoxShape3D();
        shape.Size = size;
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, size.Y / 2, 0);
        
        return collision;
    }
    
    private static CollisionShape3D CreateSphere(float radius)
    {
        var shape = new SphereShape3D();
        shape.Radius = radius;
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        
        return collision;
    }
}
```

---

## 6. Vehicles and Complex Objects

### VehicleBody3D on Voxel Terrain

Vehicles use `RigidBody3D` (or custom VehicleBody3D extending it) for physics:

```csharp
public partial class VoxelVehicle : RigidBody3D
{
    [Export] public float EngineForce { get; set; } = 100.0f;
    [Export] public float BrakeForce { get; set; } = 50.0f;
    [Export] public float MaxSteeringAngle { get; set; } = 30.0f; // Degrees
    [Export] public float WheelRadius { get; set; } = 0.5f;
    [Export] public float SuspensionRestLength { get; set; } = 0.3f;
    [Export] public float SuspensionStiffness { get; set; } = 50.0f;
    [Export] public float SuspensionDamping { get; set; } = 2.0f;
    
    private VehicleWheel3D[] _wheels;
    private float _steering = 0.0f;
    private float _engineInput = 0.0f;
    private float _brakeInput = 0.0f;
    
    public override void _Ready()
    {
        Mass = 1500.0f; // 1.5 metric tons
        CanSleep = false; // Vehicles never sleep (persistent)
        
        SetupWheels();
    }
    
    private void SetupWheels()
    {
        _wheels = new VehicleWheel3D[4];
        
        // Front left
        _wheels[0] = CreateWheel(new Vector3(-1.0f, 0.5f, 1.5f), true, true);
        // Front right
        _wheels[1] = CreateWheel(new Vector3(1.0f, 0.5f, 1.5f), true, true);
        // Rear left
        _wheels[2] = CreateWheel(new Vector3(-1.0f, 0.5f, -1.5f), false, false);
        // Rear right
        _wheels[3] = CreateWheel(new Vector3(1.0f, 0.5f, -1.5f), false, false);
    }
    
    private VehicleWheel3D CreateWheel(Vector3 position, bool isFront, bool canSteer)
    {
        var wheel = new VehicleWheel3D();
        wheel.Position = position;
        wheel.WheelRadius = WheelRadius;
        wheel.SuspensionRestLength = SuspensionRestLength;
        wheel.SuspensionStiffness = SuspensionStiffness;
        wheel.SuspensionDamping = SuspensionDamping;
        wheel.UseAsSteering = canSteer;
        wheel.UseAsTraction = !isFront; // Rear wheel drive
        
        // Wheel collider
        var shape = new CylinderShape3D();
        shape.Radius = WheelRadius;
        shape.Height = 0.3f;
        
        wheel.Shape = shape;
        
        AddChild(wheel);
        return wheel;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Apply steering
        _wheels[0].Steering = Mathf.DegToRad(_steering);
        _wheels[1].Steering = Mathf.DegToRad(_steering);
        
        // Apply engine force to rear wheels
        _wheels[2].EngineForce = _engineInput * EngineForce;
        _wheels[3].EngineForce = _engineInput * EngineForce;
        
        // Apply brakes to all wheels
        foreach (var wheel in _wheels)
        {
            wheel.Brake = _brakeInput * BrakeForce;
        }
    }
    
    public void SetInputs(float steering, float throttle, float brake)
    {
        _steering = Mathf.Clamp(steering, -MaxSteeringAngle, MaxSteeringAngle);
        _engineInput = Mathf.Clamp(throttle, -1.0f, 1.0f);
        _brakeInput = Mathf.Clamp(brake, 0.0f, 1.0f);
    }
}
```

### Wheel Collision

Vehicle wheels cast rays to find terrain surface:

```csharp
public partial class VehicleWheel3D : Node3D
{
    [Export] public float WheelRadius { get; set; } = 0.5f;
    [Export] public float SuspensionRestLength { get; set; } = 0.3f;
    [Export] public float SuspensionStiffness { get; set; } = 50.0f;
    [Export] public float SuspensionDamping { get; set; } = 2.0f;
    
    public float Steering { get; set; } = 0.0f;
    public float EngineForce { get; set; } = 0.0f;
    public float Brake { get; set; } = 0.0f;
    
    private float _suspensionCompression = 0.0f;
    private bool _isInContact = false;
    private Vector3 _contactNormal = Vector3.Up;
    private float _slip = 0.0f;
    
    public void UpdateWheel(VoxelVehicle vehicle, double delta)
    {
        // Cast ray down from wheel position
        var from = GlobalPosition;
        var to = from + new Vector3(0, -SuspensionRestLength - WheelRadius, 0);
        
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = (uint)PhysicsLayers.Terrain;
        
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            _isInContact = true;
            var hitPoint = (Vector3)result["position"];
            _contactNormal = (Vector3)result["normal"];
            
            // Calculate suspension compression
            var distance = from.DistanceTo(hitPoint);
            _suspensionCompression = SuspensionRestLength - (distance - WheelRadius);
            
            // Apply suspension force
            var suspensionForce = CalculateSuspensionForce(delta);
            vehicle.ApplyCentralImpulse(_contactNormal * suspensionForce * (float)delta);
            
            // Apply traction/friction
            if (EngineForce != 0 || Brake != 0)
            {
                ApplyTraction(vehicle, delta);
            }
        }
        else
        {
            _isInContact = false;
            _suspensionCompression = 0.0f;
        }
    }
    
    private float CalculateSuspensionForce(double delta)
    {
        // Spring force
        var springForce = _suspensionCompression * SuspensionStiffness;
        
        // Damping force (simplified)
        var dampingForce = _suspensionCompression * SuspensionDamping;
        
        return springForce + dampingForce;
    }
}
```

### Terrain Following

Vehicles adapt to terrain slope through suspension:

```csharp
public void AlignToTerrain(VehicleWheel3D[] wheels)
{
    // Calculate average terrain normal under vehicle
    Vector3 avgNormal = Vector3.Zero;
    int contactCount = 0;
    
    foreach (var wheel in wheels)
    {
        if (wheel.IsInContact)
        {
            avgNormal += wheel.ContactNormal;
            contactCount++;
        }
    }
    
    if (contactCount > 0)
    {
        avgNormal /= contactCount;
        avgNormal = avgNormal.Normalized();
        
        // Smoothly rotate vehicle to match terrain
        var targetRotation = Quaternion.FromToRotation(Vector3.Up, avgNormal);
        var currentRotation = Quaternion.FromEuler(GlobalRotation);
        var newRotation = currentRotation.Slerp(targetRotation, 0.1f);
        
        GlobalRotation = newRotation.GetEuler();
    }
}
```

### Rail Systems (Minecarts)

Minecarts use constrained physics on rail paths:

```csharp
public partial class Minecart : RigidBody3D
{
    [Export] public float MaxSpeed { get; set; } = 10.0f; // m/s
    [Export] public float Acceleration { get; set; } = 2.0f;
    [Export] public float Friction { get; set; } = 0.5f;
    
    private RailPath _currentRail;
    private float _progressAlongRail = 0.0f; // 0.0 to 1.0
    private float _speed = 0.0f;
    
    public override void _PhysicsProcess(double delta)
    {
        if (_currentRail == null) return;
        
        // Move along rail path
        _progressAlongRail += _speed * (float)delta / _currentRail.Length;
        _progressAlongRail = Mathf.Clamp(_progressAlongRail, 0.0f, 1.0f);
        
        // Get position on rail curve
        var point = _currentRail.Curve.SampleBaked(_progressAlongRail * _currentRail.Curve.GetBakedLength());
        GlobalPosition = _currentRail.ToGlobal(point);
        
        // Get tangent for rotation
        var tangent = _currentRail.Curve.SampleBaked(
            _progressAlongRail * _currentRail.Curve.GetBakedLength(),
            true
        );
        LookAt(GlobalPosition + tangent, Vector3.Up);
        
        // Apply friction
        _speed = Mathf.MoveToward(_speed, 0.0f, Friction * (float)delta);
        
        // Check for rail junctions/switches
        if (_progressAlongRail >= 1.0f)
        {
            TransitionToNextRail();
        }
    }
    
    public void ApplyImpulse(float force)
    {
        _speed += force * Acceleration;
        _speed = Mathf.Clamp(_speed, -MaxSpeed, MaxSpeed);
    }
    
    private void TransitionToNextRail()
    {
        var nextRail = _currentRail.GetConnectedRail();
        if (nextRail != null)
        {
            _currentRail = nextRail;
            _progressAlongRail = 0.0f;
        }
        else
        {
            // End of line - stop
            _speed = 0.0f;
        }
    }
}
```

---

## 7. Performance Optimization

### Physics Sleeping

Godot's physics sleeping is critical for voxel world performance:

**Sleeping Rules**:

```csharp
public class SleepOptimizationManager
{
    public void ConfigureSleepSettings()
    {
        // Global physics settings
        PhysicsServer3D.AreaSetParam(
            GetWorld3D().Space,
            PhysicsServer3D.AreaParameter.LinearDamp,
            0.1f
        );
        
        PhysicsServer3D.AreaSetParam(
            GetWorld3D().Space,
            PhysicsServer3D.AreaParameter.AngularDamp,
            0.5f
        );
    }
    
    public void SetBodySleepThresholds(RigidBody3D body, BodyType type)
    {
        switch (type)
        {
            case BodyType.Debris:
                body.CanSleep = true;
                // Auto-sleep when nearly stopped
                break;
                
            case BodyType.Vehicle:
                body.CanSleep = false; // Never sleep
                break;
                
            case BodyType.Prop:
                body.CanSleep = true;
                body.Sleeping = true; // Start sleeping
                break;
                
            case BodyType.Item:
                body.CanSleep = true;
                // Items sleep after 5 seconds of being still
                GetTree().CreateTimer(5.0).Timeout += () => {
                    if (body.LinearVelocity.Length() < 0.01f)
                        body.Sleeping = true;
                };
                break;
        }
    }
}
```

### Collision Layer Management

Organized collision layers prevent unnecessary tests:

```csharp
[Flags]
public enum PhysicsLayers : uint
{
    None = 0,
    Terrain = 1 << 0,      // Layer 1: Voxel chunks
    Entities = 1 << 1,     // Layer 2: Players, agents
    Vehicles = 1 << 2,     // Layer 3: Cars, minecarts
    Debris = 1 << 3,       // Layer 4: Rubble, items
    Projectiles = 1 << 4,  // Layer 5: Arrows, bullets
    Triggers = 1 << 5,     // Layer 6: Zones, sensors
    Buildings = 1 << 6,    // Layer 7: Static structures
    Water = 1 << 7,        // Layer 8: Water blocks
}

// Layer interaction matrix
public static class CollisionMatrix
{
    public static readonly Dictionary<PhysicsLayers, PhysicsLayers> CollisionMasks = new()
    {
        { PhysicsLayers.Terrain, PhysicsLayers.Entities | PhysicsLayers.Vehicles | 
                                PhysicsLayers.Debris | PhysicsLayers.Projectiles },
        { PhysicsLayers.Entities, PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                                 PhysicsLayers.Vehicles | PhysicsLayers.Debris | 
                                 PhysicsLayers.Triggers },
        { PhysicsLayers.Vehicles, PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                                 PhysicsLayers.Vehicles | PhysicsLayers.Debris },
        { PhysicsLayers.Debris, PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                               PhysicsLayers.Vehicles | PhysicsLayers.Debris },
        { PhysicsLayers.Projectiles, PhysicsLayers.Terrain | PhysicsLayers.Entities },
        { PhysicsLayers.Triggers, PhysicsLayers.Entities },
        { PhysicsLayers.Buildings, PhysicsLayers.Entities | PhysicsLayers.Vehicles | 
                                  PhysicsLayers.Debris | PhysicsLayers.Projectiles },
    };
}
```

### Update Frequency Culling

Different update rates based on distance and importance:

```csharp
public class PhysicsLODManager
{
    private Dictionary<PhysicsBody3D, float> _updateRates = new();
    private double[] _updateTimers;
    
    public void SetLODLevel(PhysicsBody3D body, LODLevel level)
    {
        float updateRate = level switch
        {
            LODLevel.Full => 1.0f,      // Every tick (20 TPS)
            LODLevel.High => 0.5f,      // 10 TPS
            LODLevel.Medium => 0.2f,    // 4 TPS
            LODLevel.Low => 0.1f,       // 2 TPS
            LODLevel.Frozen => 0.0f,    // No updates
            _ => 1.0f
        };
        
        _updateRates[body] = updateRate;
    }
    
    public void UpdateLODForDistance(PhysicsBody3D body, Vector3 playerPos)
    {
        float distance = body.GlobalPosition.DistanceTo(playerPos);
        
        var level = distance switch
        {
            < 10.0f => LODLevel.Full,    // 0-10m
            < 30.0f => LODLevel.High,    // 10-30m
            < 100.0f => LODLevel.Medium, // 30-100m
            < 200.0f => LODLevel.Low,    // 100-200m
            _ => LODLevel.Frozen          // >200m
        };
        
        SetLODLevel(body, level);
    }
    
    public bool ShouldUpdate(PhysicsBody3D body, double delta)
    {
        if (!_updateRates.TryGetValue(body, out var rate))
            return true; // Default: always update
            
        if (rate <= 0) return false; // Frozen
        
        int index = body.GetHashCode() % _updateTimers.Length;
        _updateTimers[index] += delta;
        
        double interval = 1.0 / (20.0 * rate); // 20 TPS base
        
        if (_updateTimers[index] >= interval)
        {
            _updateTimers[index] = 0;
            return true;
        }
        
        return false;
    }
}

public enum LODLevel
{
    Full,    // 20 TPS - Critical (players, nearby agents)
    High,    // 10 TPS - Important (nearby vehicles, active debris)
    Medium,  // 4 TPS - Background (distant agents, sleeping debris)
    Low,     // 2 TPS - Far away (very distant objects)
    Frozen   // 0 TPS - No simulation (beyond simulation distance)
}
```

### Multithreaded Physics

Godot 4 supports multithreaded physics (broad phase only):

```csharp
public void ConfigureMultithreadedPhysics()
{
    // Enable in Project Settings or via code
    ProjectSettings.SetSetting("physics/3d/run_on_separate_thread", true);
    
    // Number of physics solver iterations
    ProjectSettings.SetSetting("physics/3d/solver/solver_iterations", 8);
    
    // Enable continuous collision detection for fast objects
    ProjectSettings.SetSetting("physics/3d/default_continuous_cd", true);
}
```

**Important**: Narrow phase and constraint solving remain single-threaded. Gameplay code in `_PhysicsProcess` always runs on main thread.

---

## 8. Technical Implementation

### VoxelCollisionSystem Class

Central system managing all voxel collision:

```csharp
public partial class VoxelCollisionSystem : Node
{
    public static VoxelCollisionSystem Instance { get; private set; }
    
    [Export] public int MaxChunkCollisionBodies { get; set; } = 1000;
    [Export] public float CollisionLODRange { get; set; } = 100.0f;
    
    private Dictionary<ChunkCoord, ChunkCollisionBody> _chunkBodies = new();
    private SpatialHashGrid _spatialHash = new();
    private CollisionUpdateManager _updateManager = new();
    private PhysicsLODManager _lodManager = new();
    private Queue<ChunkCoord> _generationQueue = new();
    
    public override void _Ready()
    {
        Instance = this;
        
        // Subscribe to world events
        World.Instance.ChunkLoaded += OnChunkLoaded;
        World.Instance.ChunkUnloaded += OnChunkUnloaded;
        World.Instance.BlocksModified += OnBlocksModified;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Process pending collision updates
        _updateManager.ProcessUpdates();
        
        // Update LOD for all collision bodies
        UpdateCollisionLOD();
        
        // Generate collision for queued chunks
        ProcessGenerationQueue();
    }
    
    private void OnChunkLoaded(Chunk chunk)
    {
        // Queue collision generation (don't block loading)
        _generationQueue.Enqueue(chunk.Coord);
    }
    
    private void OnChunkUnloaded(Chunk chunk)
    {
        // Remove collision body
        if (_chunkBodies.TryGetValue(chunk.Coord, out var body))
        {
            body.QueueFree();
            _chunkBodies.Remove(chunk.Coord);
            _spatialHash.Remove(body);
        }
    }
    
    private void OnBlocksModified(List<BlockCoord> blocks)
    {
        _updateManager.OnBlocksModified(blocks);
    }
    
    private void ProcessGenerationQueue()
    {
        // Generate up to 5 collision bodies per frame
        int toGenerate = Mathf.Min(5, _generationQueue.Count);
        
        for (int i = 0; i < toGenerate; i++)
        {
            var coord = _generationQueue.Dequeue();
            GenerateChunkCollision(coord);
        }
    }
    
    private void GenerateChunkCollision(ChunkCoord coord)
    {
        var chunk = World.Instance.GetChunk(coord);
        if (chunk == null) return;
        
        var body = new ChunkCollisionBody();
        body.Initialize(chunk);
        
        AddChild(body);
        _chunkBodies[coord] = body;
        _spatialHash.Insert(body, body.GetAabb());
    }
    
    private void UpdateCollisionLOD()
    {
        // Get average player position
        var avgPlayerPos = GetAveragePlayerPosition();
        
        foreach (var kvp in _chunkBodies)
        {
            var distance = kvp.Value.GlobalPosition.DistanceTo(avgPlayerPos);
            
            if (distance > CollisionLODRange * 2)
            {
                // Far away - disable collision entirely
                kvp.Value.ProcessMode = ProcessModeEnum.Disabled;
            }
            else if (distance > CollisionLODRange)
            {
                // Medium distance - simplified collision
                kvp.Value.ProcessMode = ProcessModeEnum.Always;
                kvp.Value.SetLOD(CollisionLOD.Low);
            }
            else
            {
                // Nearby - full collision
                kvp.Value.ProcessMode = ProcessModeEnum.Always;
                kvp.Value.SetLOD(CollisionLOD.Full);
            }
        }
    }
    
    /// <summary>
    /// Raycast against voxel world with high precision.
    /// </summary>
    public RaycastHit Raycast(Vector3 origin, Vector3 direction, float maxDistance, 
                             uint collisionMask = uint.MaxValue)
    {
        return VoxelRaycaster.RaycastVoxels(origin, direction, maxDistance);
    }
    
    /// <summary>
    /// Check if an AABB intersects with terrain.
    /// </summary>
    public bool CheckAABBCollision(AABB bounds, uint collisionMask = uint.MaxValue)
    {
        // Query spatial hash for nearby chunk bodies
        var nearbyBodies = _spatialHash.Query(bounds);
        
        foreach (var body in nearbyBodies)
        {
            if (body is ChunkCollisionBody chunkBody)
            {
                if (chunkBody.GetAabb().Intersects(bounds))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}
```

### Chunk Collision Body Management

```csharp
public partial class ChunkCollisionBody : StaticBody3D
{
    public ChunkCoord Coord { get; private set; }
    public CollisionLOD CurrentLOD { get; private set; } = CollisionLOD.Full;
    
    private CollisionShape3D _shapeNode;
    private ConcavePolygonShape3D _fullShape;
    private ConcavePolygonShape3D _simplifiedShape;
    private AABB _bounds;
    
    public void Initialize(Chunk chunk)
    {
        Coord = chunk.Coord;
        
        // Generate full detail shape
        _fullShape = ChunkCollisionGenerator.GenerateCollisionMesh(chunk);
        
        // Generate simplified shape (50% reduction)
        _simplifiedShape = ChunkCollisionGenerator.GenerateSimplifiedMesh(chunk, reduction: 0.5f);
        
        // Create collision node with full shape initially
        _shapeNode = new CollisionShape3D();
        _shapeNode.Shape = _fullShape;
        AddChild(_shapeNode);
        
        // Calculate bounds
        _bounds = CalculateBounds(chunk);
        
        // Set position
        GlobalPosition = chunk.WorldPosition;
        
        // Configure collision
        CollisionLayer = (uint)PhysicsLayers.Terrain;
        CollisionMask = (uint)(PhysicsLayers.Entities | PhysicsLayers.Vehicles | 
                              PhysicsLayers.Debris | PhysicsLayers.Projectiles);
        
        // Start sleeping until needed
        CanSleep = true;
        Sleeping = true;
    }
    
    public void SetLOD(CollisionLOD lod)
    {
        if (CurrentLOD == lod) return;
        
        CurrentLOD = lod;
        
        // Swap collision shape based on LOD
        _shapeNode.Shape = lod switch
        {
            CollisionLOD.Full => _fullShape,
            CollisionLOD.Low => _simplifiedShape,
            _ => _fullShape
        };
        
        // Wake up briefly to update
        if (lod != CollisionLOD.Disabled)
        {
            Sleeping = false;
        }
    }
    
    public AABB GetAabb()
    {
        return new AABB(
            GlobalPosition + _bounds.Position,
            _bounds.Size
        );
    }
    
    public void UpdateMesh(Chunk chunk)
    {
        // Regenerate both LODs
        _fullShape = ChunkCollisionGenerator.GenerateCollisionMesh(chunk);
        _simplifiedShape = ChunkCollisionGenerator.GenerateSimplifiedMesh(chunk, reduction: 0.5f);
        
        // Update current shape
        _shapeNode.Shape = CurrentLOD == CollisionLOD.Full ? _fullShape : _simplifiedShape;
        
        // Update bounds
        _bounds = CalculateBounds(chunk);
        
        // Wake up
        Sleeping = false;
    }
}

public enum CollisionLOD
{
    Full,      // Full detail surface mesh
    Low,       // Simplified mesh (50% reduction)
    Disabled   // No collision (too far away)
}
```

### Collision Update Triggers

```csharp
public class CollisionUpdateManager
{
    private HashSet<ChunkCoord> _immediateUpdates = new();
    private HashSet<ChunkCoord> _deferredUpdates = new();
    private Dictionary<ChunkCoord, int> _updatePriority = new();
    
    public void OnBlocksModified(IEnumerable<BlockCoord> modifiedBlocks)
    {
        foreach (var block in modifiedBlocks)
        {
            var chunkCoord = block.ToChunkCoord();
            
            // Check if modification is near any player
            if (IsNearPlayer(block))
            {
                _immediateUpdates.Add(chunkCoord);
                _updatePriority[chunkCoord] = 10; // High priority
            }
            else
            {
                _deferredUpdates.Add(chunkCoord);
                _updatePriority[chunkCoord] = _updatePriority.GetValueOrDefault(chunkCoord, 0) + 1;
            }
            
            // Check neighbor chunks if on edge
            if (IsChunkEdge(block))
            {
                var neighbors = GetNeighborChunks(chunkCoord, block);
                foreach (var neighbor in neighbors)
                {
                    _deferredUpdates.Add(neighbor);
                }
            }
        }
    }
    
    public void ProcessUpdates()
    {
        // Process immediate updates first (synchronous)
        foreach (var coord in _immediateUpdates)
        {
            var body = VoxelCollisionSystem.Instance.GetChunkBody(coord);
            var chunk = World.Instance.GetChunk(coord);
            
            if (body != null && chunk != null)
            {
                body.UpdateMesh(chunk);
            }
        }
        _immediateUpdates.Clear();
        
        // Process deferred updates (limit per frame)
        int processed = 0;
        int maxPerFrame = 3;
        
        var sortedDeferred = _deferredUpdates
            .OrderByDescending(c => _updatePriority.GetValueOrDefault(c, 0))
            .ToList();
        
        foreach (var coord in sortedDeferred)
        {
            if (processed >= maxPerFrame) break;
            
            var body = VoxelCollisionSystem.Instance.GetChunkBody(coord);
            var chunk = World.Instance.GetChunk(coord);
            
            if (body != null && chunk != null)
            {
                body.UpdateMesh(chunk);
                processed++;
            }
            
            _deferredUpdates.Remove(coord);
        }
    }
    
    private bool IsNearPlayer(BlockCoord block)
    {
        var blockPos = BlockToWorld(block);
        return PlayerManager.Instance.GetPlayers().Any(p => 
            p.GlobalPosition.DistanceTo(blockPos) < 50.0f);
    }
}
```

---

## 9. Integration Points

### Terrain Modification Updates Collision

When terrain changes, collision must update automatically:

```csharp
public partial class TerrainModificationSystem : Node
{
    [Signal] public delegate void TerrainModifiedEventHandler(List<BlockCoord> blocks);
    
    public void ModifyBlocks(List<BlockChange> changes)
    {
        var modifiedCoords = new List<BlockCoord>();
        
        foreach (var change in changes)
        {
            // Apply change to world
            World.Instance.SetBlock(change.Coord, change.NewBlock);
            modifiedCoords.Add(change.Coord);
            
            // Spawn debris if block destroyed
            if (change.OldBlock.IsSolid && !change.NewBlock.IsSolid)
            {
                BlockDestructionSystem.Instance.OnBlockDestroyed(
                    change.Coord, change.OldBlock.Type);
            }
        }
        
        // Notify collision system
        VoxelCollisionSystem.Instance.OnBlocksModified(modifiedCoords);
        
        // Emit signal for other systems
        EmitSignal(SignalName.TerrainModified, modifiedCoords);
        
        // Sync to clients (server-authoritative)
        if (Multiplayer.IsServer)
        {
            Rpc(nameof(SyncTerrainChanges), changes);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void SyncTerrainChanges(List<BlockChange> changes)
    {
        // Clients receive and apply changes
        if (!Multiplayer.IsServer)
        {
            foreach (var change in changes)
            {
                World.Instance.SetBlock(change.Coord, change.NewBlock);
            }
        }
    }
}
```

### Entity Spawn Collision Checks

Ensure entities don't spawn inside terrain:

```csharp
public class EntitySpawnSystem
{
    public bool TrySpawnEntity(Vector3 position, EntityType type, out Entity entity)
    {
        entity = null;
        
        // Check collision at spawn position
        var shape = EntityShapeFactory.CreateShape(type.Shape);
        var testAABB = CalculateEntityAABB(position, shape);
        
        // Check terrain collision
        if (VoxelCollisionSystem.Instance.CheckAABBCollision(testAABB))
        {
            return false; // Blocked by terrain
        }
        
        // Check entity collision
        var nearbyEntities = SpatialHash.Query(testAABB);
        if (nearbyEntities.Any())
        {
            return false; // Blocked by other entities
        }
        
        // Safe to spawn
        entity = EntityFactory.Create(type);
        entity.GlobalPosition = position;
        GetTree().Root.AddChild(entity);
        
        // Add to spatial hash
        SpatialHash.Insert(entity, testAABB);
        
        return true;
    }
    
    public Vector3 FindSafeSpawnPosition(Vector3 desiredPosition, EntityType type, 
                                         float searchRadius = 10.0f)
    {
        // Try desired position first
        if (TrySpawnEntity(desiredPosition, type, out _))
            return desiredPosition;
        
        // Search in expanding circles
        for (float radius = 1.0f; radius <= searchRadius; radius += 1.0f)
        {
            for (float angle = 0; angle < Mathf.Tau; angle += Mathf.Pi / 4)
            {
                var offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );
                
                var testPos = desiredPosition + offset;
                testPos.Y = GetGroundHeight(testPos); // Find ground
                
                if (TrySpawnEntity(testPos, type, out _))
                    return testPos;
            }
        }
        
        // Fallback: spawn at ground level
        return new Vector3(desiredPosition.X, GetGroundHeight(desiredPosition), desiredPosition.Z);
    }
}
```

### Building Placement Validation

Buildings must validate collision before placement:

```csharp
public class BuildingPlacementSystem
{
    public PlacementValidationResult ValidateBuildingPlacement(BuildingType building, 
                                                               Vector3 position, 
                                                               Vector3 rotation)
    {
        var result = new PlacementValidationResult();
        
        // Calculate building AABB
        var buildingAABB = building.GetAABB(position, rotation);
        
        // Check terrain flatness
        var groundPoints = SampleGroundHeight(buildingAABB);
        float heightVariance = groundPoints.Max() - groundPoints.Min();
        
        if (heightVariance > building.MaxHeightVariance)
        {
            result.IsValid = false;
            result.Errors.Add($"Ground too uneven (variance: {heightVariance:F2}m)");
        }
        
        // Check terrain collision
        if (VoxelCollisionSystem.Instance.CheckAABBCollision(buildingAABB))
        {
            result.IsValid = false;
            result.Errors.Add("Building intersects with terrain");
        }
        
        // Check for existing buildings
        var nearbyBuildings = BuildingManager.Query(buildingAABB.Grow(0.5f));
        if (nearbyBuildings.Any())
        {
            result.IsValid = false;
            result.Errors.Add("Too close to existing structures");
        }
        
        // Check resource requirements
        if (!HasResources(building.Cost))
        {
            result.IsValid = false;
            result.Errors.Add("Insufficient resources");
        }
        
        // Check ownership/laws
        if (!HasBuildingRights(position))
        {
            result.IsValid = false;
            result.Errors.Add("No building rights in this area");
        }
        
        return result;
    }
}
```

### Physics-Based Gameplay

Some gameplay systems use physics directly:

```csharp
public class PhysicsGameplaySystem
{
    // Explosions apply physics forces
    public void CreateExplosion(Vector3 center, float radius, float force)
    {
        // Find all physics bodies in radius
        var affectedBodies = GetBodiesInRadius(center, radius);
        
        foreach (var body in affectedBodies)
        {
            if (body is RigidBody3D rigidBody)
            {
                var direction = (rigidBody.GlobalPosition - center).Normalized();
                var distance = rigidBody.GlobalPosition.DistanceTo(center);
                var falloff = 1.0f - (distance / radius);
                
                var impulse = direction * force * falloff;
                rigidBody.ApplyCentralImpulse(impulse);
                
                // Wake up sleeping bodies
                rigidBody.Sleeping = false;
            }
        }
        
        // Damage terrain blocks
        var affectedBlocks = GetBlocksInRadius(center, radius);
        foreach (var block in affectedBlocks)
        {
            var distance = BlockToWorld(block).DistanceTo(center);
            var damage = force * (1.0f - distance / radius);
            
            ApplyBlockDamage(block, damage);
        }
    }
    
    // Falling blocks damage entities below
    public void OnFallingBlockImpact(RigidBody3D block, Vector3 impactPoint)
    {
        var velocity = block.LinearVelocity.Length();
        var mass = block.Mass;
        var kineticEnergy = 0.5f * mass * velocity * velocity;
        
        // Find entities below
        var query = PhysicsRayQueryParameters3D.Create(
            impactPoint, 
            impactPoint + new Vector3(0, -5, 0)
        );
        query.CollisionMask = (uint)PhysicsLayers.Entities;
        
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            var hitEntity = (Node)result["collider"];
            var damage = CalculateFallDamage(kineticEnergy);
            
            if (hitEntity is IDamageable damageable)
            {
                damageable.TakeDamage(damage, DamageType.FallingBlock);
            }
        }
    }
}
```

---

## 10. C# Code Examples

### Character Controller

```csharp
using Godot;

public partial class PlayerCharacter : CharacterBody3D
{
    [Export] public float WalkSpeed { get; set; } = 5.0f;
    [Export] public float SprintSpeed { get; set; } = 8.0f;
    [Export] public float JumpVelocity { get; set; } = 8.5f;
    [Export] public float StepHeight { get; set; } = 0.6f;
    [Export] public float MaxSlopeAngle { get; set; } = 45.0f;
    
    private float _gravity;
    private bool _isSprinting = false;
    private Camera3D _camera;
    
    public override void _Ready()
    {
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        _camera = GetNode<Camera3D>("Camera3D");
        
        // Setup collision
        var shape = new CapsuleShape3D();
        shape.Radius = 0.6f;
        shape.Height = 1.8f;
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.9f, 0);
        AddChild(collision);
        
        // Set collision layers
        CollisionLayer = (uint)PhysicsLayers.Entities;
        CollisionMask = (uint)(PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                              PhysicsLayers.Vehicles | PhysicsLayers.Debris);
    }
    
    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;
        
        // Apply gravity
        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;
        
        // Handle jump
        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;
        
        // Get input direction (relative to camera)
        Vector2 inputDir = Input.GetVector("move_left", "move_right", 
                                           "move_forward", "move_back");
        Vector3 direction = new Vector3(inputDir.X, 0, inputDir.Y).Normalized();
        
        // Transform direction by camera rotation
        direction = direction.Rotated(Vector3.Up, _camera.GlobalRotation.Y);
        
        // Handle sprint
        _isSprinting = Input.IsActionPressed("sprint") && direction != Vector3.Zero;
        
        // Apply movement
        float speed = _isSprinting ? SprintSpeed : WalkSpeed;
        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
        }
        
        Velocity = velocity;
        
        // Move with slide
        MoveAndSlide();
        
        // Handle stepping
        if (IsOnFloor() && GetLastMotion().Length() < 0.01f && direction != Vector3.Zero)
        {
            TryStepUp(direction);
        }
        
        // Sync to server if multiplayer
        if (Multiplayer.MultiplayerPeer != null)
        {
            Rpc(nameof(SyncPosition), GlobalPosition, Velocity);
        }
    }
    
    private void TryStepUp(Vector3 direction)
    {
        var from = GlobalPosition + new Vector3(0, StepHeight, 0);
        var to = from + direction.Normalized() * 0.4f;
        
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = (uint)PhysicsLayers.Terrain;
        
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        
        if (result.Count == 0)
        {
            // Can step up
            GlobalPosition += new Vector3(0, StepHeight * 0.5f, 0);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void SyncPosition(Vector3 position, Vector3 velocity)
    {
        if (!Multiplayer.IsServer)
        {
            // Client reconciliation
            GlobalPosition = GlobalPosition.Lerp(position, 0.3f);
            Velocity = velocity;
        }
    }
}
```

### Collision Mesh Generation

```csharp
using Godot;
using System.Collections.Generic;

public static class ChunkCollisionGenerator
{
    public static ConcavePolygonShape3D GenerateCollisionMesh(Chunk chunk)
    {
        var faces = new List<Vector3>();
        
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int y = 0; y < Chunk.SizeY; y++)
                {
                    var block = chunk.GetBlock(x, y, z);
                    if (block.IsAir || !block.IsSolid) continue;
                    
                    // Check each face
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.Top))
                        AddTopFace(faces, x, y, z);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.Bottom))
                        AddBottomFace(faces, x, y, z);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.North))
                        AddNorthFace(faces, x, y, z);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.South))
                        AddSouthFace(faces, x, y, z);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.East))
                        AddEastFace(faces, x, y, z);
                    if (IsFaceExposed(chunk, x, y, z, BlockFace.West))
                        AddWestFace(faces, x, y, z);
                }
            }
        }
        
        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces.ToArray());
        return shape;
    }
    
    private static bool IsFaceExposed(Chunk chunk, int x, int y, int z, BlockFace face)
    {
        var (nx, ny, nz) = face switch
        {
            BlockFace.Top => (x, y + 1, z),
            BlockFace.Bottom => (x, y - 1, z),
            BlockFace.North => (x, y, z - 1),
            BlockFace.South => (x, y, z + 1),
            BlockFace.East => (x + 1, y, z),
            BlockFace.West => (x - 1, y, z),
            _ => (x, y, z)
        };
        
        var neighbor = chunk.GetBlock(nx, ny, nz);
        return neighbor.IsAir || !neighbor.IsSolid;
    }
    
    private static void AddTopFace(List<Vector3> faces, int x, int y, int z)
    {
        float x0 = x, x1 = x + 1;
        float y0 = y + 1;
        float z0 = z, z1 = z + 1;
        
        // Triangle 1
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x1, y0, z0));
        faces.Add(new Vector3(x1, y0, z1));
        
        // Triangle 2
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x1, y0, z1));
        faces.Add(new Vector3(x0, y0, z1));
    }
    
    // Similar methods for other faces...
    private static void AddBottomFace(List<Vector3> faces, int x, int y, int z)
    {
        float x0 = x, x1 = x + 1;
        float y0 = y;
        float z0 = z, z1 = z + 1;
        
        // Triangle 1 (flipped normal)
        faces.Add(new Vector3(x0, y0, z1));
        faces.Add(new Vector3(x1, y0, z1));
        faces.Add(new Vector3(x1, y0, z0));
        
        // Triangle 2
        faces.Add(new Vector3(x0, y0, z1));
        faces.Add(new Vector3(x1, y0, z0));
        faces.Add(new Vector3(x0, y0, z0));
    }
    
    private static void AddNorthFace(List<Vector3> faces, int x, int y, int z)
    {
        float x0 = x, x1 = x + 1;
        float y0 = y, y1 = y + 1;
        float z0 = z;
        
        faces.Add(new Vector3(x1, y0, z0));
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x0, y1, z0));
        
        faces.Add(new Vector3(x1, y0, z0));
        faces.Add(new Vector3(x0, y1, z0));
        faces.Add(new Vector3(x1, y1, z0));
    }
    
    private static void AddSouthFace(List<Vector3> faces, int x, int y, int z)
    {
        float x0 = x, x1 = x + 1;
        float y0 = y, y1 = y + 1;
        float z0 = z + 1;
        
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x1, y0, z0));
        faces.Add(new Vector3(x1, y1, z0));
        
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x1, y1, z0));
        faces.Add(new Vector3(x0, y1, z0));
    }
    
    private static void AddEastFace(List<Vector3> faces, int x, int y, int z)
    {
        float x0 = x + 1;
        float y0 = y, y1 = y + 1;
        float z0 = z, z1 = z + 1;
        
        faces.Add(new Vector3(x0, y0, z1));
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x0, y1, z0));
        
        faces.Add(new Vector3(x0, y0, z1));
        faces.Add(new Vector3(x0, y1, z0));
        faces.Add(new Vector3(x0, y1, z1));
    }
    
    private static void AddWestFace(List<Vector3> faces, int x, int y, int z)
    {
        float x0 = x;
        float y0 = y, y1 = y + 1;
        float z0 = z, z1 = z + 1;
        
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x0, y0, z1));
        faces.Add(new Vector3(x0, y1, z1));
        
        faces.Add(new Vector3(x0, y0, z0));
        faces.Add(new Vector3(x0, y1, z1));
        faces.Add(new Vector3(x0, y1, z0));
    }
}

public enum BlockFace
{
    Top, Bottom, North, South, East, West
}
```

### Raycasting for Block Interaction

```csharp
using Godot;

public class VoxelRaycaster
{
    /// <summary>
    /// Performs DDA (Digital Differential Analysis) raycast through voxel grid.
    /// Efficiently traverses only voxels along ray path.
    /// </summary>
    public static RaycastHit RaycastVoxels(Vector3 origin, Vector3 direction, float maxDistance)
    {
        // Normalize direction
        direction = direction.Normalized();
        
        // Starting voxel
        var currentBlock = new Vector3I(
            Mathf.FloorToInt(origin.X),
            Mathf.FloorToInt(origin.Y),
            Mathf.FloorToInt(origin.Z)
        );
        
        // Step direction for each axis
        var step = new Vector3I(
            direction.X > 0 ? 1 : -1,
            direction.Y > 0 ? 1 : -1,
            direction.Z > 0 ? 1 : -1
        );
        
        // Distance to next voxel boundary for each axis
        var tMax = new Vector3(
            direction.X > 0 ? (currentBlock.X + 1 - origin.X) / direction.X 
                            : (currentBlock.X - origin.X) / direction.X,
            direction.Y > 0 ? (currentBlock.Y + 1 - origin.Y) / direction.Y 
                            : (currentBlock.Y - origin.Y) / direction.Y,
            direction.Z > 0 ? (currentBlock.Z + 1 - origin.Z) / direction.Z 
                            : (currentBlock.Z - origin.Z) / direction.Z
        );
        
        // Distance between voxel boundaries for each axis
        var tDelta = new Vector3(
            Mathf.Abs(1.0f / direction.X),
            Mathf.Abs(1.0f / direction.Y),
            Mathf.Abs(1.0f / direction.Z)
        );
        
        // Track which face we entered through
        Vector3 entryNormal = Vector3.Zero;
        
        // Traverse voxels
        float distance = 0;
        int maxSteps = Mathf.CeilToInt(maxDistance * 3); // Safety limit
        
        for (int stepCount = 0; stepCount < maxSteps; stepCount++)
        {
            // Check current voxel
            var block = World.Instance.GetBlock(currentBlock.X, currentBlock.Y, currentBlock.Z);
            
            if (block != null && block.IsSolid)
            {
                return new RaycastHit
                {
                    BlockPosition = currentBlock,
                    HitPoint = origin + direction * distance,
                    Distance = distance,
                    Normal = -entryNormal, // Normal points back toward ray origin
                    Block = block
                };
            }
            
            // Step to next voxel
            if (tMax.X < tMax.Y && tMax.X < tMax.Z)
            {
                currentBlock.X += step.X;
                entryNormal = new Vector3(-step.X, 0, 0);
                distance = tMax.X;
                tMax.X += tDelta.X;
            }
            else if (tMax.Y < tMax.Z)
            {
                currentBlock.Y += step.Y;
                entryNormal = new Vector3(0, -step.Y, 0);
                distance = tMax.Y;
                tMax.Y += tDelta.Y;
            }
            else
            {
                currentBlock.Z += step.Z;
                entryNormal = new Vector3(0, 0, -step.Z);
                distance = tMax.Z;
                tMax.Z += tDelta.Z;
            }
            
            // Check max distance
            if (distance > maxDistance)
                break;
        }
        
        return null; // No hit
    }
}

public class RaycastHit
{
    public Vector3I BlockPosition { get; set; }
    public Vector3 HitPoint { get; set; }
    public float Distance { get; set; }
    public Vector3 Normal { get; set; }
    public Block Block { get; set; }
}
```

### Vehicle Physics

```csharp
using Godot;

public partial class VoxelVehicle : RigidBody3D
{
    [Export] public float EngineForce { get; set; } = 200.0f;
    [Export] public float BrakeForce { get; set; } = 100.0f;
    [Export] public float MaxSteeringAngle { get; set; } = 35.0f;
    [Export] public float WheelRadius { get; set; } = 0.5f;
    [Export] public float SuspensionRestLength { get; set; } = 0.4f;
    [Export] public float SuspensionStiffness { get; set; } = 80.0f;
    [Export] public float SuspensionDamping { get; set; } = 3.0f;
    
    private VehicleWheel3D[] _wheels = new VehicleWheel3D[4];
    private float _steering = 0.0f;
    private float _throttle = 0.0f;
    private float _brake = 0.0f;
    
    public override void _Ready()
    {
        Mass = 1500.0f;
        CanSleep = false; // Vehicles never sleep
        
        // Setup vehicle collision
        var shape = new BoxShape3D();
        shape.Size = new Vector3(2.0f, 1.0f, 4.5f);
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.5f, 0);
        AddChild(collision);
        
        // Setup wheels
        SetupWheel(0, new Vector3(-1.0f, 0.4f, 1.5f), true, true);  // Front left
        SetupWheel(1, new Vector3(1.0f, 0.4f, 1.5f), true, true);   // Front right
        SetupWheel(2, new Vector3(-1.0f, 0.4f, -1.5f), false, false); // Rear left
        SetupWheel(3, new Vector3(1.0f, 0.4f, -1.5f), false, false);  // Rear right
        
        // Set collision layers
        CollisionLayer = (uint)PhysicsLayers.Vehicles;
        CollisionMask = (uint)(PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                              PhysicsLayers.Vehicles | PhysicsLayers.Debris);
    }
    
    private void SetupWheel(int index, Vector3 position, bool isFront, bool canSteer)
    {
        var wheel = new VehicleWheel3D();
        wheel.Position = position;
        wheel.WheelRadius = WheelRadius;
        wheel.SuspensionRestLength = SuspensionRestLength;
        wheel.SuspensionStiffness = SuspensionStiffness;
        wheel.SuspensionDamping = SuspensionDamping;
        wheel.UseAsSteering = canSteer;
        wheel.UseAsTraction = !isFront; // Rear wheel drive
        
        // Wheel collision
        var shape = new CylinderShape3D();
        shape.Radius = WheelRadius;
        shape.Height = 0.3f;
        wheel.Shape = shape;
        
        AddChild(wheel);
        _wheels[index] = wheel;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        // Get inputs
        _steering = Input.GetAxis("steer_right", "steer_left") * MaxSteeringAngle;
        _throttle = Input.GetAxis("brake", "accelerate");
        _brake = Input.IsActionPressed("brake") ? 1.0f : 0.0f;
        
        // Apply steering to front wheels
        _wheels[0].Steering = Mathf.DegToRad(_steering);
        _wheels[1].Steering = Mathf.DegToRad(_steering);
        
        // Apply engine force to rear wheels
        _wheels[2].EngineForce = _throttle * EngineForce;
        _wheels[3].EngineForce = _throttle * EngineForce;
        
        // Apply brakes to all wheels
        foreach (var wheel in _wheels)
        {
            wheel.Brake = _brake * BrakeForce;
        }
        
        // Update wheel physics
        UpdateWheels(delta);
        
        // Sync to server
        if (Multiplayer.MultiplayerPeer != null && IsMultiplayerAuthority())
        {
            Rpc(nameof(SyncVehicleState), GlobalPosition, GlobalRotation, 
                LinearVelocity, AngularVelocity);
        }
    }
    
    private void UpdateWheels(double delta)
    {
        foreach (var wheel in _wheels)
        {
            wheel.UpdateWheel(this, delta);
        }
        
        // Align vehicle to terrain based on wheel contacts
        AlignToTerrain();
    }
    
    private void AlignToTerrain()
    {
        Vector3 avgNormal = Vector3.Zero;
        int contactCount = 0;
        
        foreach (var wheel in _wheels)
        {
            if (wheel.IsInContact)
            {
                avgNormal += wheel.ContactNormal;
                contactCount++;
            }
        }
        
        if (contactCount > 0)
        {
            avgNormal = (avgNormal / contactCount).Normalized();
            
            // Smoothly interpolate to terrain normal
            var targetUp = avgNormal;
            var currentUp = GlobalTransform.Basis.Y;
            var newUp = currentUp.Lerp(targetUp, 0.1f);
            
            // Apply rotation
            LookAt(GlobalPosition + GlobalTransform.Basis.Z, newUp);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void SyncVehicleState(Vector3 pos, Vector3 rot, Vector3 vel, Vector3 angVel)
    {
        if (!Multiplayer.IsServer)
        {
            GlobalPosition = GlobalPosition.Lerp(pos, 0.3f);
            GlobalRotation = GlobalRotation.Lerp(rot, 0.3f);
            LinearVelocity = vel;
            AngularVelocity = angVel;
        }
    }
}

public partial class VehicleWheel3D : Node3D
{
    [Export] public float WheelRadius { get; set; } = 0.5f;
    [Export] public float SuspensionRestLength { get; set; } = 0.4f;
    [Export] public float SuspensionStiffness { get; set; } = 80.0f;
    [Export] public float SuspensionDamping { get; set; } = 3.0f;
    
    public float Steering { get; set; } = 0.0f;
    public float EngineForce { get; set; } = 0.0f;
    public float Brake { get; set; } = 0.0f;
    
    public bool IsInContact { get; private set; } = false;
    public Vector3 ContactNormal { get; private set; } = Vector3.Up;
    
    private CollisionShape3D _shape;
    private float _suspensionCompression = 0.0f;
    private float _rotationAngle = 0.0f;
    
    public override void _Ready()
    {
        _shape = new CollisionShape3D();
        AddChild(_shape);
    }
    
    public void UpdateWheel(VehicleVehicle vehicle, double delta)
    {
        // Cast ray for suspension
        var from = GlobalPosition;
        var to = from + new Vector3(0, -SuspensionRestLength - WheelRadius, 0);
        
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = (uint)PhysicsLayers.Terrain;
        
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            IsInContact = true;
            var hitPoint = (Vector3)result["position"];
            ContactNormal = (Vector3)result["normal"];
            
            // Calculate compression
            var distance = from.DistanceTo(hitPoint);
            _suspensionCompression = SuspensionRestLength - (distance - WheelRadius);
            
            // Calculate suspension force (spring + damper)
            var springForce = _suspensionCompression * SuspensionStiffness;
            var dampingForce = _suspensionCompression * SuspensionDamping;
            var totalForce = Mathf.Max(0, springForce + dampingForce);
            
            // Apply suspension force
            vehicle.ApplyForce(ContactNormal * totalForce, GlobalPosition - vehicle.GlobalPosition);
            
            // Calculate wheel velocity at contact point
            var wheelOffset = GlobalPosition - vehicle.GlobalPosition;
            var wheelVelocity = vehicle.LinearVelocity + 
                               vehicle.AngularVelocity.Cross(wheelOffset);
            
            // Apply traction/friction
            if (Mathf.Abs(EngineForce) > 0.01f || Brake > 0.01f)
            {
                // Longitudinal force (acceleration/braking)
                var forwardDir = -GlobalTransform.Basis.Z.Rotated(Vector3.Up, Steering);
                var longitudinalForce = forwardDir * EngineForce;
                var brakeForce = -wheelVelocity.Project(forwardDir).Normalized() * Brake;
                
                vehicle.ApplyForce(longitudinalForce + brakeForce, wheelOffset);
            }
            
            // Lateral friction (prevents sliding sideways)
            var rightDir = GlobalTransform.Basis.X.Rotated(Vector3.Up, Steering);
            var lateralVelocity = wheelVelocity.Project(rightDir);
            var lateralFriction = -lateralVelocity * 10.0f; // High friction
            
            vehicle.ApplyForce(lateralFriction, wheelOffset);
            
            // Update visual rotation
            _rotationAngle += (wheelVelocity.Length() / WheelRadius) * (float)delta;
        }
        else
        {
            IsInContact = false;
            _suspensionCompression = 0.0f;
        }
    }
}
```

---

## 11. Summary & Key Decisions

### Architecture Decisions

| Decision | Rationale | Impact |
|----------|-----------|--------|
| Per-chunk collision bodies | Reduces collision faces from millions to thousands | 100x performance improvement |
| Surface-only meshes | Only visible surfaces need collision | 95% reduction in collision data |
| Hierarchical collision (Broad→Mid→Narrow) | O(n²) to O(n) complexity | Scales to 10,000+ entities |
| Physics sleeping | Static objects use minimal CPU | 80% CPU reduction when idle |
| Collision LOD | Distant chunks use simplified collision | 50% CPU savings at distance |
| Server-authoritative physics | Prevents cheating, enables replay | Consistent simulation |

### Performance Budgets

| System | Budget (per tick) | Notes |
|--------|-------------------|-------|
| Collision queries | <0.5ms | 8 players + 20 agents |
| Chunk collision generation | <10ms | Async, not blocking |
| Raycast (mining) | <0.1ms | Critical path |
| Vehicle physics | <1.0ms | All vehicles combined |
| Debris simulation | <2.0ms | Sleeping debris excluded |

### Dependencies

- **Requires**: [World Generation System](01-architecture-overview.md), [Client-Server Architecture](02-client-server-architecture.md)
- **Informs**: [AI Pathfinding](../session-2-ai-system-design), [Building System](../session-3-core-gameplay-loops), [Mining Mechanics](../session-3-core-gameplay-loops)

### Open Questions

1. **Prototype validation needed**: Actual collision performance with 100 chunks loaded simultaneously
2. **Vehicle complexity**: Simple raycast wheels vs full physics simulation trade-offs
3. **Debris persistence**: How long should rubble remain? (Performance vs realism)

### Research References

- [Godot 4 Physics Documentation](https://docs.godotengine.org/en/stable/tutorials/physics/physics_introduction.html)
- [CharacterBody3D Best Practices](https://docs.godotengine.org/en/stable/classes/class_characterbody3d.html)
- [RigidBody3D Optimization](https://docs.godotengine.org/en/stable/classes/class_rigidbody3d.html)
- Eco-style debris physics research in `planning/research/completed/r3-eco-technical-postmortem.md`

---

*Last Updated: 2026-02-01*
*Part of Session 1: Technical Architecture*
