# Hybrid Entity-Block System Specification

> **Navigation**: [Session 3 Index]([AGENTS-READ-FIRST]-index.md) | [Core Gameplay Architecture](01-gameplay-systems-architecture.md)  
> **Part of**: Session 3 - Core Gameplay Loops  
> **Related Documents**:
> - [Voxel World System](../session-1-technical-architecture/13-voxel-world-system.md)
> - [Physics & Collision](../session-1-technical-architecture/18-physics-collision.md)
> - [Movement & Interaction](01c-movement-interaction-spec.md)
> - [Inventory System](01e-inventory-system-spec.md)
> - [Terrain Modification](../session-1-technical-architecture/15-terrain-modification.md)

---

**Status**: 📝 Specification Complete  
**Last Updated**: 2026-02-01  
**Godot Version**: 4.x  
**Language**: C#

---

## Executive Summary

### Hybrid System Philosophy

Societies implements a **dual representation system** combining static voxel blocks for world geometry with dynamic entities for interactive objects. This hybrid approach leverages the strengths of both paradigms:

**Block System (Static)**:
- Efficient storage: 4 bytes per 1m³ voxel (65,536 blocks per chunk = 256 KB)
- Optimal for: Terrain, buildings, static structures
- Massive scale: Supports millions of blocks with minimal memory overhead
- Simple collision: Chunk-based concave mesh collision

**Entity System (Dynamic)**:
- Physics simulation: Full RigidBody3D dynamics
- Optimal for: Vehicles, containers, workshops, furniture
- Custom shapes: Non-cube geometries (minecarts, curved surfaces)
- State persistence: Complex data (inventory, durability, ownership)

### When to Use Blocks vs Entities

| Use Block (Static) | Use Entity (Dynamic) |
|-------------------|----------------------|
| Terrain (dirt, stone, sand) | Vehicles (minecarts, carts, cars) |
| Building walls/floors | Storage containers (chests, crates) |
| Natural formations | Crafting stations (workshops, furnaces) |
| Roads/paths | Furniture (beds, chairs, tables) |
| Ores embedded in terrain | Doors and mechanisms |
| Decorative static blocks | Moving platforms |

**Decision Matrix**:
```
IF object needs physics simulation (movement, collision response):
    → Use Entity (RigidBody3D)
    
IF object has complex state (inventory, durability, custom data):
    → Use Entity (VoxelEntity base class)
    
IF object is static and cube-shaped:
    → Use Block (VoxelChunk)
    
IF object needs non-cube shape:
    → Use Entity (custom mesh collision)
```

### Examples from Reference Games

**Eco-Style Approach**:
- Vehicles have weight affecting handling
- Breaking objects creates physics debris
- Carrying capacity limits transport logistics
- Workshops are physical entities requiring placement

**Minecraft Comparison**:
- Minecraft: Chests are "tile entities" (blocks with data)
- Societies: Chests are full entities (better physics, interaction)
- Minecraft: Minecarts are entities on rails (similar approach)
- Societies: Expanded vehicle physics with weight/cargo

---

## 2. Block vs Entity Distinction

### 2.1 Static Blocks (Terrain, Buildings)

**Characteristics**:
```csharp
// 4-byte compact representation
public struct BlockData {
    public ushort BlockId;      // 65,536 possible types
    public byte Metadata;       // Rotation, state, orientation
    public byte LightAndState;  // Lighting, special flags
}

// Stored in contiguous 256 KB chunk arrays
public class VoxelChunk {
    private BlockData[] _blocks = new BlockData[65536]; // 16×16×256
}
```

**Use Cases**:
- Natural terrain (dirt, stone, sand layers)
- Building foundations and walls
- Static decorative elements
- Resource nodes (ore deposits in terrain)

**Collision**: ConcavePolygonShape3D per chunk (surface-only optimization)

### 2.2 Tile Entities (Block-Based with Data)

**Minecraft-Style Approach** (Limited Use):
```csharp
// For simple storage blocks that could be blocks
public class TileEntityBlock {
    public BlockPosition Position { get; set; }
    public Dictionary<string, object> Data { get; set; }
    // Simple inventory (27 slots for chest block)
    // Basic state (lit/unlit for furnace)
}
```

**Societies Approach**: Minimize tile entities, prefer full entities

**When Tile Entity is Acceptable**:
- Simple ON/OFF state (lamp, switch)
- Basic orientation data (stairs, logs)
- No physics needed, no complex interaction

### 2.3 Dynamic Entities (Vehicles, Workshops)

**Characteristics**:
```csharp
// Full Godot node with physics
public partial class VoxelEntity : RigidBody3D {
    public EntityId Id { get; set; }
    public EntityType Type { get; set; }
    public VoxelEntityData Data { get; set; } // Persistent state
    
    // Physics properties
    public float Mass { get; set; }
    public bool CanBePushed { get; set; }
    public bool CanBeRotated { get; set; }
}
```

**Use Cases**:
- Vehicles (minecarts, carts, cars)
- Storage (chests, crates, warehouses)
- Crafting (workbenches, furnaces, anvils)
- Furniture (beds, chairs, tables)
- Mechanisms (doors, gates, elevators)

### 2.4 Decision Criteria

**Choose BLOCK When**:
- [ ] Object is static (never moves after placement)
- [ ] Shape is cubic (1m³ or multi-block aligned)
- [ ] No complex state needed (simple ON/OFF max)
- [ ] No physics interactions required
- [ ] Massive quantities expected (thousands+)

**Choose ENTITY When**:
- [ ] Object moves or can be moved
- [ ] Custom shape needed (non-cube)
- [ ] Complex state (inventory, durability, timers)
- [ ] Physics simulation required
- [ ] Player needs to interact with it as an object
- [ ] Needs to be picked up/placed as a unit

**Hybrid Cases**:
- **Doors**: Entity (animated, interactive), but fits in block grid
- **Furniture**: Entity (custom shapes), snaps to block positions
- **Vehicles**: Entity (physics), but wheels follow terrain blocks

---

## 3. Entity System Architecture

### 3.1 Entity Base Class Structure

```csharp
namespace Societies.Entities {
    
    /// <summary>
    /// Base class for all voxel-world entities.
    /// Extends Godot's RigidBody3D for physics integration.
    /// </summary>
    public partial class VoxelEntity : RigidBody3D {
        
        // === Identity ===
        [Export] public EntityId Id { get; set; }
        [Export] public EntityType EntityType { get; set; }
        [Export] public string DisplayName { get; set; }
        
        // === Physics Configuration ===
        [Export] public float BaseMass { get; set; } = 10.0f;
        [Export] public bool IsPushable { get; set; } = true;
        [Export] public bool IsLiftable { get; set; } = false;
        [Export] public float DragCoefficient { get; set; } = 0.1f;
        
        // === Placement ===
        [Export] public bool SnapToGrid { get; set; } = true;
        [Export] public float GridSize { get; set; } = 1.0f; // 1m voxel grid
        [Export] public Vector3 GridOffset { get; set; } = new Vector3(0.5f, 0, 0.5f);
        
        // === State ===
        public EntityState CurrentState { get; protected set; } = EntityState.Placed;
        public EntityDurability Durability { get; protected set; }
        public EntityOwner Owner { get; set; }
        
        // === Components ===
        public InventoryComponent Inventory { get; protected set; }
        public InteractionComponent Interaction { get; protected set; }
        public VisualComponent Visuals { get; protected set; }
        
        // === Network ===
        public bool IsServerAuthoritative { get; set; } = true;
        public int LastUpdateTick { get; set; }
        
        public override void _Ready() {
            base._Ready();
            SetupCollisionLayers();
            InitializeComponents();
        }
        
        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            
            // Server-side physics validation
            if (IsServerAuthoritative && Multiplayer.IsServer()) {
                ValidatePhysicsState();
            }
        }
        
        protected virtual void SetupCollisionLayers() {
            CollisionLayer = (uint)PhysicsLayers.Entities;
            CollisionMask = (uint)(PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                                  PhysicsLayers.Vehicles | PhysicsLayers.Debris);
        }
        
        protected virtual void InitializeComponents() {
            // Override in subclasses
        }
        
        public virtual void OnPlaced(EntityPlacementContext context) {
            CurrentState = EntityState.Placed;
            GlobalPosition = SnapPositionToGrid(GlobalPosition);
            Freeze = true; // Static until interacted with
        }
        
        public virtual void OnPickedUp(Entity agent) {
            CurrentState = EntityState.BeingCarried;
            Freeze = true;
            // Parent to agent or move to inventory
        }
        
        public virtual void OnDropped(Vector3 position) {
            CurrentState = EntityState.Placed;
            GlobalPosition = position;
            Freeze = false; // Enable physics
        }
        
        public virtual void OnDestroyed(DestructionContext context) {
            CurrentState = EntityState.Destroyed;
            SpawnSalvage(context);
            QueueFree();
        }
        
        protected virtual void SpawnSalvage(DestructionContext context) {
            // Override to spawn specific debris
        }
        
        private Vector3 SnapPositionToGrid(Vector3 position) {
            if (!SnapToGrid) return position;
            
            return new Vector3(
                Mathf.Snapped(position.X - GridOffset.X, GridSize) + GridOffset.X,
                Mathf.Snapped(position.Y - GridOffset.Y, GridSize) + GridOffset.Y,
                Mathf.Snapped(position.Z - GridOffset.Z, GridSize) + GridOffset.Z
            );
        }
        
        private void ValidatePhysicsState() {
            // Anti-cheat: Validate position/velocity is reasonable
            if (LinearVelocity.Length() > 50.0f) {
                LinearVelocity = Vector3.Zero;
            }
            
            // Prevent falling through world
            if (GlobalPosition.Y < -250) {
                GlobalPosition = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
                LinearVelocity = Vector3.Zero;
            }
        }
    }
    
    public enum EntityState {
        InInventory,
        BeingCarried,
        Placed,       // Static/placed in world
        InUse,        // Being interacted with
        Destroyed
    }
}
```

### 3.2 Transform vs Block Grid Alignment

**Grid Alignment System**:
```csharp
public class EntityGridAlignment {
    
    /// <summary>
    /// Aligns entity to voxel grid while preserving rotation.
    /// </summary>
    public static void AlignToVoxelGrid(VoxelEntity entity) {
        // Get current position
        var pos = entity.GlobalPosition;
        var rot = entity.GlobalRotation;
        
        // Snap position to 1m grid
        var snappedPos = new Vector3(
            Mathf.Snapped(pos.X, 1.0f) + 0.5f,  // Center in voxel
            Mathf.Snapped(pos.Y, 1.0f),         // Bottom-align Y
            Mathf.Snapped(pos.Z, 1.0f) + 0.5f   // Center in voxel
        );
        
        // Snap rotation to 90° increments (optional)
        if (entity.SnapRotation) {
            var snappedRot = new Vector3(
                Mathf.Snapped(rot.X, Mathf.Pi / 2),
                Mathf.Snapped(rot.Y, Mathf.Pi / 2),
                Mathf.Snapped(rot.Z, Mathf.Pi / 2)
            );
            entity.GlobalRotation = snappedRot;
        }
        
        entity.GlobalPosition = snappedPos;
    }
    
    /// <summary>
    /// Gets the voxel coordinates this entity occupies.
    /// </summary>
    public static List<Vector3I> GetOccupiedVoxels(VoxelEntity entity) {
        var bounds = GetEntityBounds(entity);
        var voxels = new List<Vector3I>();
        
        for (int x = Mathf.FloorToInt(bounds.Min.X); x < Mathf.CeilToInt(bounds.Max.X); x++) {
            for (int y = Mathf.FloorToInt(bounds.Min.Y); y < Mathf.CeilToInt(bounds.Max.Y); y++) {
                for (int z = Mathf.FloorToInt(bounds.Min.Z); z < Mathf.CeilToInt(bounds.Max.Z); z++) {
                    voxels.Add(new Vector3I(x, y, z));
                }
            }
        }
        
        return voxels;
    }
    
    public static Bounds GetEntityBounds(VoxelEntity entity) {
        var aabb = entity.GetAabb();
        return new Bounds {
            Min = aabb.Position,
            Max = aabb.End
        };
    }
}
```

### 3.3 Physics Integration (RigidBody3D)

**Physics Configuration**:
```csharp
public class EntityPhysicsConfig {
    
    public static void ConfigureVehiclePhysics(RigidBody3D body) {
        body.Mass = 50.0f;  // 50kg for carts
        body.CanSleep = false; // Vehicles never sleep (persistent)
        body.Sleeping = false;
        
        // Linear damping (air resistance + friction)
        body.LinearDamp = 0.1f;
        body.LinearDampMode = DampMode.Combine;
        
        // Angular damping (rotation resistance)
        body.AngularDamp = 0.5f;
        body.AngularDampMode = DampMode.Combine;
        
        // Gravity
        body.GravityScale = 1.0f;
        
        // Center of mass (typically lower for stability)
        body.CenterOfMassMode = CenterOfMassModeEnum.Custom;
        body.CenterOfMass = new Vector3(0, 0.3f, 0);
        
        // Continuous collision detection for fast movement
        body.CcdMode = CCDMode.CastShape;
    }
    
    public static void ConfigureStaticEntity(RigidBody3D body) {
        // Static entities (furniture, placed objects)
        body.Freeze = true;
        body.FreezeMode = FreezeModeEnum.Static;
        
        // Still need collision for interaction
        body.CanSleep = true;
        body.Sleeping = true;
    }
    
    public static void ConfigurePushableEntity(RigidBody3D body) {
        // Light objects that can be pushed around
        body.Mass = 5.0f;
        body.CanSleep = true;
        
        // Low friction for easy pushing
        body.PhysicsMaterialOverride = new PhysicsMaterial {
            Friction = 0.3f,
            Rough = false
        };
    }
}
```

### 3.4 Entity Lifecycle Management

```csharp
public class EntityLifecycleManager {
    private Dictionary<EntityId, VoxelEntity> _activeEntities = new();
    private Queue<EntityId> _destructionQueue = new();
    
    public VoxelEntity SpawnEntity(EntityType type, Vector3 position, EntitySpawnContext context) {
        var entity = EntityFactory.Create(type);
        entity.Id = EntityId.Generate();
        entity.GlobalPosition = position;
        entity.Owner = context.Owner;
        
        // Add to scene
        GetTree().Root.AddChild(entity);
        
        // Track
        _activeEntities[entity.Id] = entity;
        
        // Network spawn
        if (Multiplayer.IsServer()) {
            Rpc(nameof(SyncEntitySpawn), entity.Id, (int)type, position, 
                context.ToDictionary());
        }
        
        return entity;
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    public void SyncEntitySpawn(EntityId id, int typeId, Vector3 position, 
                                Godot.Collections.Dictionary context) {
        // Client receives spawn
        if (_activeEntities.ContainsKey(id)) return;
        
        var type = (EntityType)typeId;
        var entity = EntityFactory.Create(type);
        entity.Id = id;
        entity.GlobalPosition = position;
        entity.Owner = EntityOwner.FromDictionary(context);
        
        GetTree().Root.AddChild(entity);
        _activeEntities[id] = entity;
    }
    
    public void QueueDestruction(EntityId id, DestructionContext context) {
        if (_activeEntities.TryGetValue(id, out var entity)) {
            entity.OnDestroyed(context);
            _destructionQueue.Enqueue(id);
        }
    }
    
    public void ProcessDestructions() {
        while (_destructionQueue.Count > 0) {
            var id = _destructionQueue.Dequeue();
            _activeEntities.Remove(id);
            
            // Network destroy
            if (Multiplayer.IsServer()) {
                Rpc(nameof(SyncEntityDestroy), id);
            }
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    public void SyncEntityDestroy(EntityId id) {
        if (_activeEntities.TryGetValue(id, out var entity)) {
            entity.QueueFree();
            _activeEntities.Remove(id);
        }
    }
}
```

---

## 4. Custom Shaped Entities Catalog (MVP)

### 4.1 Minecarts and Rail System

```csharp
public partial class MinecartEntity : VoxelEntity {
    
    [Export] public float MaxSpeed { get; set; } = 8.0f; // m/s
    [Export] public float Acceleration { get; set; } = 2.0f;
    [Export] public float Friction { get; set; } = 0.5f;
    [Export] public float Capacity { get; set; } = 100.0f; // kg cargo
    
    private RailPath _currentRail;
    private float _progressOnRail = 0.0f;
    private float _currentSpeed = 0.0f;
    private float _currentCargoWeight = 0.0f;
    
    public override void _Ready() {
        base._Ready();
        EntityType = EntityType.Minecart;
        BaseMass = 20.0f; // Empty cart weight
        
        SetupMinecartMesh();
        SetupMinecartCollision();
        SetupInventory();
    }
    
    private void SetupMinecartMesh() {
        // Custom minecart mesh (not a cube)
        var mesh = new MeshInstance3D();
        mesh.Mesh = LoadMesh("res://meshes/entities/minecart.obj");
        mesh.Scale = new Vector3(0.8f, 0.6f, 1.2f); // 0.8m wide, 0.6m tall, 1.2m long
        AddChild(mesh);
    }
    
    private void SetupMinecartCollision() {
        // Box collision matching cart shape
        var shape = new BoxShape3D();
        shape.Size = new Vector3(0.8f, 0.6f, 1.2f);
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.3f, 0); // Center vertically
        AddChild(collision);
        
        // Wheels (visual + collision)
        SetupWheels();
    }
    
    private void SetupInventory() {
        Inventory = new InventoryComponent {
            MaxWeight = Capacity,
            MaxSlots = 6 // 6 slots for cargo
        };
        AddChild(Inventory);
    }
    
    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);
        
        if (_currentRail != null) {
            MoveAlongRail((float)delta);
        } else {
            // Not on rails - standard physics
            ApplyRailFriction((float)delta);
        }
        
        // Update mass based on cargo
        UpdateTotalMass();
    }
    
    private void MoveAlongRail(float delta) {
        if (_currentRail == null) return;
        
        // Get position on rail curve
        float curveLength = _currentRail.Curve.GetBakedLength();
        float distance = _progressOnRail * curveLength;
        
        // Sample position and tangent
        Vector3 railPos = _currentRail.Curve.SampleBaked(distance);
        Vector3 tangent = _currentRail.Curve.SampleBaked(distance, true).Normalized();
        
        // Calculate slope factor
        float slope = CalculateSlope(tangent);
        
        // Apply physics
        ApplyRailPhysics(delta, slope);
        
        // Move cart
        _progressOnRail += (_currentSpeed * delta) / curveLength;
        
        // Update position
        GlobalPosition = _currentRail.ToGlobal(railPos);
        
        // Orient along rail
        if (tangent.Length() > 0.001f) {
            LookAt(GlobalPosition + tangent, Vector3.Up);
        }
        
        // Check for end of rail
        if (_progressOnRail >= 1.0f) {
            TransitionToNextRail();
        }
    }
    
    private void ApplyRailPhysics(float delta, float slope) {
        // Gravity assist/resistance based on slope
        float gravityForce = slope * 9.8f * Mass;
        
        // Apply friction
        float frictionForce = Friction * Mass * 9.8f;
        
        // Calculate acceleration
        float netForce = gravityForce - frictionForce;
        float acceleration = netForce / Mass;
        
        // Update speed
        _currentSpeed += acceleration * delta;
        _currentSpeed = Mathf.Clamp(_currentSpeed, -MaxSpeed, MaxSpeed);
        
        // Stop if slow enough
        if (Mathf.Abs(_currentSpeed) < 0.1f) {
            _currentSpeed = 0;
        }
    }
    
    private void ApplyRailFriction(float delta) {
        // Ground friction when not on rails
        LinearVelocity = LinearVelocity.MoveToward(Vector3.Zero, Friction * delta);
    }
    
    private void UpdateTotalMass() {
        _currentCargoWeight = Inventory?.CurrentWeight ?? 0;
        Mass = BaseMass + _currentCargoWeight;
    }
    
    public void ApplyPush(float force, Vector3 direction) {
        if (_currentRail != null) {
            // Push along rail direction
            float railDir = Vector3.Dot(direction, GlobalTransform.Basis.Z);
            _currentSpeed += force * Mathf.Sign(railDir);
        } else {
            // Standard physics push
            ApplyCentralImpulse(direction * force);
        }
    }
    
    public void SetRail(RailPath rail, float progress = 0) {
        _currentRail = rail;
        _progressOnRail = progress;
    }
    
    private void TransitionToNextRail() {
        var nextRail = _currentRail?.GetConnectedRail();
        if (nextRail != null) {
            _currentRail = nextRail;
            _progressOnRail = 0;
        } else {
            // End of line - derail or stop
            _currentSpeed = 0;
            _currentRail = null;
        }
    }
    
    private float CalculateSlope(Vector3 tangent) {
        // Returns -1 to 1 based on vertical component
        return -tangent.Y; // Negative because up is negative Y
    }
    
    protected override void SpawnSalvage(DestructionContext context) {
        // Spawn minecart materials + cargo
        base.SpawnSalvage(context);
        
        // Drop cargo
        if (Inventory != null) {
            foreach (var item in Inventory.GetAllItems()) {
                SpawnItemDrop(item, GlobalPosition + RandomOffset());
            }
        }
    }
}
```

### 4.2 Wooden Carts (Hand-Pushed)

```csharp
public partial class WoodenCartEntity : VoxelEntity {
    
    [Export] public float Capacity { get; set; } = 150.0f; // kg
    [Export] public float PushForce { get; set; } = 30.0f;
    [Export] public float MaxPushSpeed { get; set; } = 3.0f; // m/s (walking speed)
    
    private float _currentCargoWeight = 0;
    private Entity _currentPusher;
    private Vector3 _pushDirection;
    
    public override void _Ready() {
        base._Ready();
        EntityType = EntityType.WoodenCart;
        BaseMass = 15.0f;
        IsPushable = true;
        
        SetupCartMesh();
        SetupCartCollision();
        SetupInventory();
        
        // Carts can be lifted when empty
        IsLiftable = true;
    }
    
    private void SetupCartMesh() {
        // Wooden cart with 4 wheels
        var cartBody = new MeshInstance3D();
        cartBody.Mesh = LoadMesh("res://meshes/entities/wooden_cart_body.obj");
        cartBody.Scale = new Vector3(1.0f, 0.8f, 1.5f);
        AddChild(cartBody);
        
        // Wheel positions
        SetupWheel(new Vector3(-0.4f, 0.2f, 0.5f));
        SetupWheel(new Vector3(0.4f, 0.2f, 0.5f));
        SetupWheel(new Vector3(-0.4f, 0.2f, -0.5f));
        SetupWheel(new Vector3(0.4f, 0.2f, -0.5f));
    }
    
    private void SetupWheel(Vector3 position) {
        var wheel = new MeshInstance3D();
        wheel.Mesh = LoadMesh("res://meshes/entities/cart_wheel.obj");
        wheel.Position = position;
        wheel.Scale = new Vector3(0.4f, 0.4f, 0.1f);
        AddChild(wheel);
        
        // Wheel collision
        var wheelShape = new CylinderShape3D();
        wheelShape.Radius = 0.2f;
        wheelShape.Height = 0.1f;
        
        var wheelCol = new CollisionShape3D();
        wheelCol.Shape = wheelShape;
        wheelCol.Position = position;
        wheelCol.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(wheelCol);
    }
    
    private void SetupCartCollision() {
        // Main body collision
        var shape = new BoxShape3D();
        shape.Size = new Vector3(1.0f, 0.8f, 1.5f);
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.4f, 0);
        AddChild(collision);
        
        // Configure physics
        EntityPhysicsConfig.ConfigureVehiclePhysics(this);
    }
    
    private void SetupInventory() {
        Inventory = new InventoryComponent {
            MaxWeight = Capacity,
            MaxSlots = 12,
            AccessibleWhenPlaced = true
        };
        AddChild(Inventory);
    }
    
    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);
        
        UpdateTotalMass();
        
        if (_currentPusher != null) {
            ApplyPushing((float)delta);
        } else {
            ApplyGroundFriction((float)delta);
        }
    }
    
    private void ApplyPushing(float delta) {
        if (_currentPusher == null) return;
        
        // Calculate push direction from pusher position
        Vector3 toCart = (GlobalPosition - _currentPusher.GlobalPosition).Normalized();
        _pushDirection = new Vector3(toCart.X, 0, toCart.Z).Normalized();
        
        // Apply force
        float pushSpeed = CalculatePushSpeed();
        Vector3 targetVelocity = _pushDirection * pushSpeed;
        
        // Smoothly accelerate
        LinearVelocity = LinearVelocity.MoveToward(targetVelocity, PushForce * delta);
        
        // Rotate cart to face direction
        if (_pushDirection.Length() > 0.001f) {
            LookAt(GlobalPosition + _pushDirection, Vector3.Up);
        }
    }
    
    private void ApplyGroundFriction(float delta) {
        // Slow down when not being pushed
        LinearVelocity = LinearVelocity.MoveToward(Vector3.Zero, 2.0f * delta);
    }
    
    private float CalculatePushSpeed() {
        // Heavier carts move slower
        float loadFactor = 1.0f - (_currentCargoWeight / Capacity * 0.5f);
        return MaxPushSpeed * loadFactor;
    }
    
    private void UpdateTotalMass() {
        _currentCargoWeight = Inventory?.CurrentWeight ?? 0;
        Mass = BaseMass + _currentCargoWeight;
    }
    
    public void StartPushing(Entity pusher) {
        _currentPusher = pusher;
    }
    
    public void StopPushing() {
        _currentPusher = null;
    }
    
    protected override void SpawnSalvage(DestructionContext context) {
        // Wooden cart drops wood + nails
        SpawnDebris("wood", 5);
        SpawnDebris("iron_nails", 2);
        
        // Drop cargo
        if (Inventory != null) {
            foreach (var item in Inventory.GetAllItems()) {
                SpawnItemDrop(item, GlobalPosition + RandomOffset());
            }
        }
    }
}
```

### 4.3 Basic Workshops (Crafting Stations)

```csharp
public partial class WorkshopEntity : VoxelEntity {
    
    [Export] public WorkshopType WorkshopType { get; set; }
    [Export] public float CraftingSpeedMultiplier { get; set; } = 1.0f;
    [Export] public List<CraftingRecipe> AvailableRecipes { get; set; }
    
    private CraftingComponent _craftingComponent;
    private List<Entity> _activeUsers = new();
    
    public override void _Ready() {
        base._Ready();
        EntityType = EntityType.Workshop;
        BaseMass = 50.0f;
        IsPushable = false; // Heavy, not pushable
        IsLiftable = false;
        
        SetupWorkshopMesh();
        SetupWorkshopCollision();
        SetupCraftingComponent();
        SetupInteraction();
    }
    
    private void SetupWorkshopMesh() {
        // Workshop appearance based on type
        var mesh = new MeshInstance3D();
        
        switch (WorkshopType) {
            case WorkshopType.Workbench:
                mesh.Mesh = LoadMesh("res://meshes/entities/workbench.obj");
                mesh.Scale = new Vector3(1.5f, 1.0f, 0.8f);
                break;
            case WorkshopType.Furnace:
                mesh.Mesh = LoadMesh("res://meshes/entities/furnace.obj");
                mesh.Scale = new Vector3(1.0f, 1.2f, 1.0f);
                break;
            case WorkshopType.Anvil:
                mesh.Mesh = LoadMesh("res://meshes/entities/anvil.obj");
                mesh.Scale = new Vector3(0.6f, 0.8f, 0.6f);
                break;
        }
        
        AddChild(mesh);
        
        // Add particle effect for active crafting
        if (WorkshopType == WorkshopType.Furnace) {
            SetupFurnaceEffects();
        }
    }
    
    private void SetupWorkshopCollision() {
        var shape = new BoxShape3D();
        
        switch (WorkshopType) {
            case WorkshopType.Workbench:
                shape.Size = new Vector3(1.5f, 1.0f, 0.8f);
                break;
            case WorkshopType.Furnace:
                shape.Size = new Vector3(1.0f, 1.2f, 1.0f);
                break;
            case WorkshopType.Anvil:
                shape.Size = new Vector3(0.6f, 0.8f, 0.6f);
                break;
        }
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, shape.Size.Y / 2, 0);
        AddChild(collision);
        
        // Static physics
        EntityPhysicsConfig.ConfigureStaticEntity(this);
    }
    
    private void SetupCraftingComponent() {
        _craftingComponent = new CraftingComponent {
            SpeedMultiplier = CraftingSpeedMultiplier,
            Recipes = AvailableRecipes,
            RequiresTool = GetRequiredTool()
        };
        AddChild(_craftingComponent);
    }
    
    private void SetupInteraction() {
        Interaction = new InteractionComponent {
            InteractionType = InteractionType.Workshop,
            Range = 2.0f,
            OnInteract = OnWorkshopInteract
        };
        AddChild(Interaction);
    }
    
    private void OnWorkshopInteract(Entity interactor) {
        // Open crafting UI
        OpenCraftingInterface(interactor);
        _activeUsers.Add(interactor);
    }
    
    private void OpenCraftingInterface(Entity user) {
        // Send UI open command to client
        if (user is Player player) {
            player.OpenUI(new CraftingInterface {
                Workshop = this,
                Recipes = AvailableRecipes
            });
        }
    }
    
    public void StartCrafting(CraftingRecipe recipe, Entity crafter) {
        _craftingComponent.StartCrafting(recipe, crafter);
        PlayCraftingAnimation(recipe);
    }
    
    private void PlayCraftingAnimation(CraftingRecipe recipe) {
        // Play appropriate animation based on workshop type
        var animator = GetNode<AnimationPlayer>("AnimationPlayer");
        
        switch (WorkshopType) {
            case WorkshopType.Furnace:
                animator.Play("furnace_active");
                break;
            case WorkshopType.Anvil:
                animator.Play("hammer_strikes");
                break;
            case WorkshopType.Workbench:
                animator.Play("sawing");
                break;
        }
    }
    
    protected override void SpawnSalvage(DestructionContext context) {
        // Drop workshop materials
        switch (WorkshopType) {
            case WorkshopType.Workbench:
                SpawnDebris("wood", 10);
                SpawnDebris("iron_nails", 3);
                break;
            case WorkshopType.Furnace:
                SpawnDebris("stone", 15);
                SpawnDebris("iron", 5);
                break;
            case WorkshopType.Anvil:
                SpawnDebris("iron", 8);
                break;
        }
        
        // Return any materials in queue
        if (_craftingComponent?.CurrentRecipe != null) {
            foreach (var ingredient in _craftingComponent.CurrentRecipe.Ingredients) {
                SpawnItemDrop(ingredient.ItemId, ingredient.Quantity, GlobalPosition);
            }
        }
    }
}

public enum WorkshopType {
    Workbench,      // Basic crafting
    Furnace,        // Smelting
    Anvil,          // Metalworking
    Carpentry,      // Advanced woodworking
    Alchemy,        // Potions/medicine
    Masonry         // Stone crafting
}
```

### 4.4 Storage Containers

```csharp
public partial class StorageContainerEntity : VoxelEntity {
    
    [Export] public ContainerType ContainerType { get; set; }
    [Export] public float Capacity { get; set; } = 100.0f; // kg
    [Export] public int SlotCount { get; set; } = 30;
    [Export] public bool IsLocked { get; set; } = false;
    [Export] public bool IsPortable { get; set; } = false;
    
    private AnimationPlayer _lidAnimator;
    private bool _isOpen = false;
    
    public override void _Ready() {
        base._Ready();
        EntityType = EntityType.StorageContainer;
        
        SetupContainer();
        SetupInventory();
        SetupInteraction();
        SetupSecurity();
    }
    
    private void SetupContainer() {
        switch (ContainerType) {
            case ContainerType.Chest:
                BaseMass = 10.0f;
                Capacity = 100.0f;
                SlotCount = 30;
                IsPortable = true;
                SetupChestMesh();
                break;
                
            case ContainerType.Crate:
                BaseMass = 5.0f;
                Capacity = 50.0f;
                SlotCount = 20;
                IsPortable = true;
                IsLiftable = true;
                SetupCrateMesh();
                break;
                
            case ContainerType.Warehouse:
                BaseMass = 100.0f;
                Capacity = 1000.0f;
                SlotCount = 300;
                IsPortable = false;
                SetupWarehouseMesh();
                break;
                
            case ContainerType.Safe:
                BaseMass = 50.0f;
                Capacity = 50.0f;
                SlotCount = 10;
                IsPortable = false;
                IsLocked = true;
                SetupSafeMesh();
                break;
        }
        
        if (!IsPortable) {
            IsPushable = false;
            IsLiftable = false;
        }
    }
    
    private void SetupChestMesh() {
        var body = new MeshInstance3D();
        body.Mesh = LoadMesh("res://meshes/entities/chest_body.obj");
        body.Scale = new Vector3(1.0f, 0.6f, 0.5f);
        body.Position = new Vector3(0, 0.3f, 0);
        AddChild(body);
        
        // Lid (animated)
        var lid = new MeshInstance3D();
        lid.Mesh = LoadMesh("res://meshes/entities/chest_lid.obj");
        lid.Scale = new Vector3(1.0f, 0.2f, 0.5f);
        lid.Position = new Vector3(0, 0.6f, -0.25f);
        lid.Name = "Lid";
        AddChild(lid);
        
        // Collision
        var shape = new BoxShape3D();
        shape.Size = new Vector3(1.0f, 0.8f, 0.5f);
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.4f, 0);
        AddChild(collision);
        
        // Setup animator
        SetupLidAnimation(lid);
    }
    
    private void SetupLidAnimation(Node3D lid) {
        _lidAnimator = new AnimationPlayer();
        AddChild(_lidAnimator);
        
        var anim = new Animation();
        anim.AddTrack(Animation.TrackType.Rotation3D);
        anim.TrackSetPath(0, "Lid:rotation");
        anim.TrackInsertKey(0, 0.0f, new Quaternion(Vector3.Right, 0));
        anim.TrackInsertKey(0, 0.3f, new Quaternion(Vector3.Right, -Mathf.Pi / 2));
        
        _lidAnimator.AddAnimation("open", anim);
        
        var closeAnim = new Animation();
        closeAnim.AddTrack(Animation.TrackType.Rotation3D);
        closeAnim.TrackSetPath(0, "Lid:rotation");
        closeAnim.TrackInsertKey(0, 0.0f, new Quaternion(Vector3.Right, -Mathf.Pi / 2));
        closeAnim.TrackInsertKey(0, 0.3f, new Quaternion(Vector3.Right, 0));
        
        _lidAnimator.AddAnimation("close", closeAnim);
    }
    
    private void SetupInventory() {
        Inventory = new InventoryComponent {
            MaxWeight = Capacity,
            MaxSlots = SlotCount,
            AccessibleWhenPlaced = true,
            AccessibleWhenCarried = IsPortable && ContainerType == ContainerType.Crate
        };
        AddChild(Inventory);
    }
    
    private void SetupInteraction() {
        Interaction = new InteractionComponent {
            InteractionType = InteractionType.Storage,
            Range = 2.0f,
            OnInteract = OnStorageInteract
        };
        AddChild(Interaction);
    }
    
    private void SetupSecurity() {
        if (IsLocked) {
            var security = new SecurityComponent {
                IsLocked = true,
                RequiresKey = true,
                Owner = Owner
            };
            AddChild(security);
        }
    }
    
    private void OnStorageInteract(Entity interactor) {
        if (IsLocked && !HasKey(interactor)) {
            ShowLockedMessage(interactor);
            return;
        }
        
        if (!_isOpen) {
            OpenContainer();
        }
        
        OpenStorageUI(interactor);
    }
    
    private void OpenContainer() {
        _isOpen = true;
        _lidAnimator?.Play("open");
        AudioManager.PlaySound("chest_open", GlobalPosition);
    }
    
    public void CloseContainer() {
        _isOpen = false;
        _lidAnimator?.Play("close");
        AudioManager.PlaySound("chest_close", GlobalPosition);
    }
    
    private void OpenStorageUI(Entity user) {
        if (user is Player player) {
            player.OpenUI(new StorageInterface {
                Container = this,
                Inventory = Inventory
            });
        }
    }
    
    protected override void SpawnSalvage(DestructionContext context) {
        // Drop container materials based on type
        switch (ContainerType) {
            case ContainerType.Chest:
                SpawnDebris("wood", 5);
                SpawnDebris("iron_nails", 2);
                break;
            case ContainerType.Crate:
                SpawnDebris("wood", 3);
                break;
            case ContainerType.Safe:
                SpawnDebris("steel", 10);
                SpawnDebris("mechanism", 1);
                break;
        }
        
        // Spill contents
        if (Inventory != null) {
            foreach (var item in Inventory.GetAllItems()) {
                for (int i = 0; i < 5; i++) { // Multiple drops for visual effect
                    SpawnItemDrop(item, GlobalPosition + RandomOffset() * 2);
                }
            }
        }
    }
}

public enum ContainerType {
    Chest,      // Standard storage
    Crate,      // Portable storage
    Warehouse,  // Large static storage
    Safe        // Secure storage
}
```

### 4.5 Beds and Furniture

```csharp
public partial class FurnitureEntity : VoxelEntity {
    
    [Export] public FurnitureType FurnitureType { get; set; }
    [Export] public float ComfortLevel { get; set; } = 1.0f;
    [Export] public float DurabilityMax { get; set; } = 100.0f;
    
    private float _currentDurability;
    private Entity _currentUser;
    
    public override void _Ready() {
        base._Ready();
        EntityType = EntityType.Furniture;
        _currentDurability = DurabilityMax;
        
        SetupFurniture();
        SetupInteraction();
    }
    
    private void SetupFurniture() {
        switch (FurnitureType) {
            case FurnitureType.Bed:
                BaseMass = 20.0f;
                ComfortLevel = 1.5f;
                SetupBedMesh();
                break;
            case FurnitureType.Chair:
                BaseMass = 5.0f;
                ComfortLevel = 0.5f;
                SetupChairMesh();
                break;
            case FurnitureType.Table:
                BaseMass = 15.0f;
                SetupTableMesh();
                break;
            case FurnitureType.Bench:
                BaseMass = 10.0f;
                ComfortLevel = 0.3f;
                SetupBenchMesh();
                break;
        }
        
        IsPushable = (BaseMass < 20.0f);
        IsLiftable = (BaseMass < 10.0f);
    }
    
    private void SetupBedMesh() {
        // Bed: 2m long, 1m wide, 0.6m tall
        var frame = new MeshInstance3D();
        frame.Mesh = LoadMesh("res://meshes/entities/bed_frame.obj");
        frame.Scale = new Vector3(1.0f, 0.4f, 2.0f);
        frame.Position = new Vector3(0, 0.2f, 0);
        AddChild(frame);
        
        var mattress = new MeshInstance3D();
        mattress.Mesh = LoadMesh("res://meshes/entities/bed_mattress.obj");
        mattress.Scale = new Vector3(0.9f, 0.2f, 1.9f);
        mattress.Position = new Vector3(0, 0.5f, 0);
        AddChild(mattress);
        
        // Collision
        var shape = new BoxShape3D();
        shape.Size = new Vector3(1.0f, 0.6f, 2.0f);
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.3f, 0);
        AddChild(collision);
        
        // Interaction zone (sleeping area)
        var interactionZone = new Area3D();
        interactionZone.Position = new Vector3(0, 0.6f, 0);
        
        var zoneShape = new BoxShape3D();
        zoneShape.Size = new Vector3(0.8f, 0.5f, 1.8f);
        
        var zoneCollision = new CollisionShape3D();
        zoneCollision.Shape = zoneShape;
        interactionZone.AddChild(zoneCollision);
        AddChild(interactionZone);
    }
    
    private void SetupInteraction() {
        Interaction = new InteractionComponent {
            Range = 1.5f,
            OnInteract = OnFurnitureInteract
        };
        AddChild(Interaction);
    }
    
    private void OnFurnitureInteract(Entity interactor) {
        switch (FurnitureType) {
            case FurnitureType.Bed:
                TrySleep(interactor);
                break;
            case FurnitureType.Chair:
            case FurnitureType.Bench:
                TrySit(interactor);
                break;
            case FurnitureType.Table:
                TryUseTable(interactor);
                break;
        }
    }
    
    private void TrySleep(Entity sleeper) {
        if (_currentUser != null) {
            ShowMessage(sleeper, "Bed is already occupied");
            return;
        }
        
        _currentUser = sleeper;
        
        if (sleeper is Player player) {
            player.EnterSleepState(this);
        }
        
        // Sleep benefits
        ApplySleepBenefits(sleeper);
        
        // Consume durability
        _currentDurability -= 1.0f;
    }
    
    private void ApplySleepBenefits(Entity sleeper) {
        // Restore stamina faster
        sleeper.ModifyStamina(50.0f * ComfortLevel);
        
        // Health regeneration (if damaged)
        if (sleeper is IDamageable damageable) {
            damageable.Heal(10.0f * ComfortLevel);
        }
        
        // Time skip (single player) or wait period (multiplayer)
        // Implementation depends on game mode
    }
    
    private void TrySit(Entity sitter) {
        _currentUser = sitter;
        
        if (sitter is Player player) {
            player.EnterSitState(this);
        }
        
        // Sitting slows stamina drain
        sitter.AddStaminaModifier("sitting", 0.5f);
    }
    
    public void EndUse(Entity user) {
        if (_currentUser == user) {
            _currentUser = null;
            
            if (user is Player player) {
                player.ExitFurnitureState();
            }
            
            user.RemoveStaminaModifier("sitting");
        }
    }
    
    protected override void SpawnSalvage(DestructionContext context) {
        switch (FurnitureType) {
            case FurnitureType.Bed:
                SpawnDebris("wood", 8);
                SpawnDebris("cloth", 3);
                break;
            case FurnitureType.Chair:
                SpawnDebris("wood", 4);
                break;
            case FurnitureType.Table:
                SpawnDebris("wood", 6);
                break;
            case FurnitureType.Bench:
                SpawnDebris("wood", 5);
                SpawnDebris("iron_nails", 2);
                break;
        }
    }
}

public enum FurnitureType {
    Bed,
    Chair,
    Table,
    Bench,
    Bookshelf,
    Dresser,
    Wardrobe
}
```

### 4.6 Doors and Mechanisms

```csharp
public partial class DoorEntity : VoxelEntity {
    
    [Export] public DoorType DoorType { get; set; }
    [Export] public bool IsOpen { get; private set; } = false;
    [Export] public bool IsLocked { get; set; } = false;
    [Export] public float OpenAngle { get; set; } = 90.0f; // Degrees
    [Export] public float OpenSpeed { get; set; } = 2.0f; // Seconds
    
    private AnimationPlayer _animator;
    private float _currentAngle = 0.0f;
    private Vector3 _hingePosition;
    
    public override void _Ready() {
        base._Ready();
        EntityType = EntityType.Door;
        BaseMass = 15.0f;
        
        SetupDoor();
        SetupInteraction();
    }
    
    private void SetupDoor() {
        // Door dimensions based on type
        Vector3 doorSize;
        switch (DoorType) {
            case DoorType.Wooden:
                doorSize = new Vector3(1.0f, 2.0f, 0.1f);
                break;
            case DoorType.Gate:
                doorSize = new Vector3(2.0f, 1.5f, 0.15f);
                break;
            case DoorType.Trapdoor:
                doorSize = new Vector3(1.0f, 1.0f, 0.1f);
                break;
            default:
                doorSize = new Vector3(1.0f, 2.0f, 0.1f);
                break;
        }
        
        // Door mesh
        var doorMesh = new MeshInstance3D();
        doorMesh.Mesh = LoadMesh($"res://meshes/entities/door_{DoorType.ToString().ToLower()}.obj");
        doorMesh.Scale = doorSize;
        doorMesh.Position = new Vector3(doorSize.X / 2, doorSize.Y / 2, 0);
        doorMesh.Name = "DoorPanel";
        AddChild(doorMesh);
        
        // Hinge position (left side by default)
        _hingePosition = new Vector3(-doorSize.X / 2, 0, 0);
        
        // Collision (dynamic - changes when opening)
        SetupDoorCollision(doorSize);
        
        // Static physics (door doesn't move physically, just rotates)
        EntityPhysicsConfig.ConfigureStaticEntity(this);
        
        // Setup animation
        SetupDoorAnimation();
    }
    
    private void SetupDoorCollision(Vector3 size) {
        var collision = new CollisionShape3D();
        collision.Name = "DoorCollision";
        
        var shape = new BoxShape3D();
        shape.Size = size;
        collision.Shape = shape;
        collision.Position = new Vector3(size.X / 2, size.Y / 2, 0);
        
        AddChild(collision);
    }
    
    private void SetupDoorAnimation() {
        _animator = new AnimationPlayer();
        AddChild(_animator);
        
        // Open animation
        var openAnim = new Animation();
        openAnim.AddTrack(Animation.TrackType.Rotation3D);
        openAnim.TrackSetPath(0, "DoorPanel:rotation");
        openAnim.TrackInsertKey(0, 0.0f, new Quaternion(Vector3.Up, 0));
        openAnim.TrackInsertKey(0, OpenSpeed, new Quaternion(Vector3.Up, Mathf.DegToRad(OpenAngle)));
        
        // Update collision track
        openAnim.AddTrack(Animation.TrackType.Method);
        openAnim.TrackSetPath(1, ".");
        openAnim.TrackInsertKey(0, OpenSpeed / 2, new Godot.Collections.Dictionary {
            { "method", "UpdateCollisionForOpen" },
            { "args", new Godot.Collections.Array() }
        });
        
        _animator.AddAnimation("open", openAnim);
        
        // Close animation
        var closeAnim = new Animation();
        closeAnim.AddTrack(Animation.TrackType.Rotation3D);
        closeAnim.TrackSetPath(0, "DoorPanel:rotation");
        closeAnim.TrackInsertKey(0, 0.0f, new Quaternion(Vector3.Up, Mathf.DegToRad(OpenAngle)));
        closeAnim.TrackInsertKey(0, OpenSpeed, new Quaternion(Vector3.Up, 0));
        
        _animator.AddAnimation("close", closeAnim);
    }
    
    private void SetupInteraction() {
        Interaction = new InteractionComponent {
            InteractionType = InteractionType.Door,
            Range = 2.0f,
            OnInteract = OnDoorInteract
        };
        AddChild(Interaction);
    }
    
    private void OnDoorInteract(Entity interactor) {
        if (IsLocked && !HasKey(interactor)) {
            ShowLockedMessage(interactor);
            PlayLockedSound();
            return;
        }
        
        ToggleDoor();
    }
    
    public void ToggleDoor() {
        if (IsOpen) {
            CloseDoor();
        } else {
            OpenDoor();
        }
    }
    
    public void OpenDoor() {
        if (IsOpen) return;
        
        IsOpen = true;
        _animator?.Play("open");
        PlayDoorSound("open");
        
        // Notify nearby entities
        EmitSignal(SignalName.DoorOpened);
    }
    
    public void CloseDoor() {
        if (!IsOpen) return;
        
        IsOpen = false;
        _animator?.Play("close");
        PlayDoorSound("close");
        
        EmitSignal(SignalName.DoorClosed);
    }
    
    private void UpdateCollisionForOpen() {
        // Reduce collision size when open to allow walking through
        var collision = GetNode<CollisionShape3D>("DoorCollision");
        if (collision?.Shape is BoxShape3D box) {
            // Shrink collision to thin line along hinge
            box.Size = new Vector3(0.1f, box.Size.Y, box.Size.Z);
        }
    }
    
    private void UpdateCollisionForClosed() {
        // Restore full collision
        var collision = GetNode<CollisionShape3D>("DoorCollision");
        if (collision?.Shape is BoxShape3D box) {
            box.Size = new Vector3(1.0f, box.Size.Y, 0.1f);
        }
    }
    
    public void Lock() {
        IsLocked = true;
        if (IsOpen) CloseDoor();
    }
    
    public void Unlock(Entity unlocker) {
        if (Owner == null || Owner.Id == unlocker.Id || HasKey(unlocker)) {
            IsLocked = false;
            ShowMessage(unlocker, "Door unlocked");
        }
    }
    
    [Signal]
    public delegate void DoorOpenedEventHandler();
    
    [Signal]
    public delegate void DoorClosedEventHandler();
    
    protected override void SpawnSalvage(DestructionContext context) {
        switch (DoorType) {
            case DoorType.Wooden:
                SpawnDebris("wood", 4);
                SpawnDebris("iron_hinge", 2);
                break;
            case DoorType.Iron:
                SpawnDebris("iron", 6);
                SpawnDebris("mechanism", 1);
                break;
            case DoorType.Gate:
                SpawnDebris("wood", 6);
                SpawnDebris("iron_nails", 4);
                break;
        }
    }
}

public enum DoorType {
    Wooden,
    Iron,
    Gate,
    Trapdoor,
    Reinforced
}
```

---

## 5. Vehicle System

### 5.1 Cart Physics and Movement

See WoodenCartEntity (Section 4.2) for hand-pushed cart implementation.

**Physics Principles**:
```csharp
public class VehiclePhysics {
    
    /// <summary>
    /// Calculates pushing force needed based on mass and terrain.
    /// </summary>
    public static float CalculatePushForce(float mass, TerrainType terrain) {
        float baseFriction = terrain switch {
            TerrainType.Road => 0.1f,
            TerrainType.Grass => 0.3f,
            TerrainType.Dirt => 0.4f,
            TerrainType.Sand => 0.6f,
            TerrainType.Snow => 0.5f,
            _ => 0.3f
        };
        
        // Force = mass × gravity × friction
        float force = mass * 9.8f * baseFriction;
        
        // Add margin for acceleration
        return force * 1.5f;
    }
    
    /// <summary>
    /// Calculates maximum speed based on pusher's capabilities.
    /// </summary>
    public static float CalculateMaxSpeed(Entity pusher, float vehicleMass) {
        float baseSpeed = 3.0f; // Walking speed
        
        // Heavier loads move slower
        float loadFactor = Mathf.Clamp(50.0f / (vehicleMass + 50.0f), 0.3f, 1.0f);
        
        // Strength skill bonus
        float strengthBonus = 1.0f;
        if (pusher.Skills?.GetLevel(SkillType.Strength) is float strength) {
            strengthBonus = 1.0f + (strength * 0.05f); // +5% per level
        }
        
        return baseSpeed * loadFactor * strengthBonus;
    }
    
    /// <summary>
    /// Handles slope movement - carts roll downhill, resist uphill.
    /// </summary>
    public static Vector3 ApplySlopeForces(RigidBody3D vehicle, float slopeAngle) {
        float mass = vehicle.Mass;
        float gravity = 9.8f;
        
        // Downhill force component
        float slopeForce = mass * gravity * Mathf.Sin(Mathf.DegToRad(slopeAngle));
        
        // Apply as impulse in direction of slope
        Vector3 slopeDirection = Vector3.Down; // Simplified
        
        return slopeDirection * slopeForce;
    }
}
```

### 5.2 Load Capacity and Weight

**Weight Distribution**:
```csharp
public class VehicleWeightSystem {
    
    public struct WeightDistribution {
        public float FrontAxleLoad { get; set; }
        public float RearAxleLoad { get; set; }
        public float TotalLoad { get; set; }
        public float CenterOfMassHeight { get; set; }
    }
    
    public static WeightDistribution CalculateDistribution(
        float vehicleMass,
        InventoryComponent cargo,
        Vector3 cargoPosition) {
        
        float cargoMass = cargo?.CurrentWeight ?? 0;
        float totalMass = vehicleMass + cargoMass;
        
        // Simple distribution (50/50 for basic carts)
        // Advanced vehicles would use wheel positions
        float frontLoad = totalMass * 0.5f;
        float rearLoad = totalMass * 0.5f;
        
        return new WeightDistribution {
            FrontAxleLoad = frontLoad,
            RearAxleLoad = rearLoad,
            TotalLoad = totalMass,
            CenterOfMassHeight = cargoPosition.Y
        };
    }
    
    public static bool IsOverloaded(VoxelEntity vehicle, float maxCapacity) {
        if (vehicle is ILoadable loadable) {
            return loadable.CurrentLoad > maxCapacity;
        }
        return false;
    }
    
    public static void ApplyOverloadEffects(RigidBody3D vehicle, float overloadRatio) {
        // Overloaded vehicles handle poorly
        if (overloadRatio > 1.0f) {
            // Reduced max speed
            float speedPenalty = (overloadRatio - 1.0f) * 0.5f;
            
            // Increased tipping risk on slopes
            vehicle.CenterOfMass = new Vector3(
                vehicle.CenterOfMass.X,
                vehicle.CenterOfMass.Y * (1.0f + speedPenalty), // Higher COG
                vehicle.CenterOfMass.Z
            );
        }
    }
}
```

### 5.3 Rail System Mechanics

See MinecartEntity (Section 4.1) for detailed rail physics.

**Rail Types**:
```csharp
public enum RailType {
    Standard,      // Basic rail
    Powered,       // Electric boost
    Detector,      // Activates mechanisms
    Activator,     // Triggers cart actions
    Crossover,     // Switches/junctions
    Elevated       // Raised rail sections
}

public class RailSystem {
    
    public void PlaceRail(Vector3 position, RailType type) {
        // Rail is placed as a special block OR as an entity
        // For Societies: Use block for rail bed, entity for rail logic
        
        // Place rail block
        var railBlock = new BlockData(BlockRegistry.RailBlockId);
        World.SetBlock(position, railBlock);
        
        // Create rail path entity for logic
        var railPath = new RailPath {
            GlobalPosition = position,
            RailType = type,
            Curve = CreateRailCurve(position, GetRailDirection())
        };
        
        GetTree().Root.AddChild(railPath);
    }
    
    private Curve3D CreateRailCurve(Vector3 position, Vector3 direction) {
        var curve = new Curve3D();
        
        // Start point
        curve.AddPoint(Vector3.Zero);
        
        // Control points for curve (if curved rail)
        curve.SetPointTilt(0, 0);
        
        // End point (1m along direction)
        curve.AddPoint(direction * 1.0f);
        
        return curve;
    }
}
```

### 5.4 Player Interaction (Push/Pull)

```csharp
public class VehicleInteraction {
    
    public static void StartPushing(Player player, VoxelEntity vehicle) {
        // Validate range
        if (player.GlobalPosition.DistanceTo(vehicle.GlobalPosition) > 3.0f) {
            return;
        }
        
        // Check if vehicle can be pushed
        if (!vehicle.IsPushable) {
            player.ShowMessage("This object is too heavy to push");
            return;
        }
        
        // Check mass vs player strength
        float maxPushMass = CalculateMaxPushMass(player);
        if (vehicle.Mass > maxPushMass) {
            player.ShowMessage("This object is too heavy for you to move");
            return;
        }
        
        // Start pushing
        if (vehicle is WoodenCartEntity cart) {
            cart.StartPushing(player);
        }
        
        // Animation
        player.PlayAnimation("pushing");
        
        // Stamina drain
        player.StartStaminaDrain("pushing", 2.0f); // 2 stamina/sec
    }
    
    public static void StopPushing(Player player, VoxelEntity vehicle) {
        if (vehicle is WoodenCartEntity cart) {
            cart.StopPushing();
        }
        
        player.StopStaminaDrain("pushing");
        player.PlayAnimation("idle");
    }
    
    public static float CalculateMaxPushMass(Player player) {
        float baseMass = 200.0f; // kg
        
        // Strength bonus
        float strength = player.Skills.GetLevel(SkillType.Strength);
        float strengthBonus = strength * 20.0f; // +20kg per level
        
        // Equipment bonus
        float equipmentBonus = 0;
        if (player.Equipment.HasItem("tool_belt")) {
            equipmentBonus = 50.0f;
        }
        
        return baseMass + strengthBonus + equipmentBonus;
    }
    
    public static void TryLift(Player player, VoxelEntity entity) {
        if (!entity.IsLiftable) {
            player.ShowMessage("This object cannot be carried");
            return;
        }
        
        float maxLiftMass = CalculateMaxLiftMass(player);
        if (entity.Mass > maxLiftMass) {
            player.ShowMessage("This object is too heavy to carry");
            return;
        }
        
        // Pick up entity
        entity.OnPickedUp(player);
        player.CarryEntity(entity);
    }
    
    public static float CalculateMaxLiftMass(Player player) {
        return 50.0f; // Base lift capacity
                   // Could be modified by skills/equipment
    }
}
```

---

## 6. Workshop System

### 6.1 Crafting Station Entities

See WorkshopEntity (Section 4.3) for implementation details.

**Crafting Station Types**:

| Station | Recipes | Requirements | Speed Multiplier |
|---------|---------|--------------|------------------|
| Workbench | Basic tools, simple items | None | 1.0× |
| Furnace | Smelting ore, cooking | Fuel (wood/coal) | 1.0× |
| Anvil | Metal tools, weapons | Hammer tool | 0.8× (faster) |
| Carpentry | Advanced wood items | Saw tool | 0.9× |
| Masonry | Stone crafting | Chisel tool | 1.0× |
| Alchemy | Potions, medicine | Alchemy skill | 0.7× |

### 6.2 Tool Requirements

```csharp
public class CraftingRequirements {
    
    public bool ValidateCraftingRequirements(
        CraftingRecipe recipe,
        Entity crafter,
        WorkshopEntity station) {
        
        // Check tool requirement
        if (!string.IsNullOrEmpty(recipe.RequiredTool)) {
            var equippedTool = crafter.Equipment?.GetEquippedTool();
            if (equippedTool?.ToolType != recipe.RequiredTool) {
                return false;
            }
        }
        
        // Check station requirement
        if (recipe.RequiredStation != null) {
            if (station == null || station.WorkshopType != recipe.RequiredStation) {
                return false;
            }
        }
        
        // Check fuel (for furnaces)
        if (recipe.RequiresFuel) {
            if (!station.HasFuel()) {
                return false;
            }
        }
        
        // Check skill requirement
        if (recipe.RequiredSkill != null) {
            var skillLevel = crafter.Skills?.GetLevel(recipe.RequiredSkill);
            if (skillLevel < recipe.RequiredSkillLevel) {
                return false;
            }
        }
        
        return true;
    }
}
```

### 6.3 Production Animations

```csharp
public class CraftingAnimations {
    
    public static void PlayCraftingAnimation(WorkshopEntity station, CraftingRecipe recipe) {
        var animator = station.GetNode<AnimationPlayer>("AnimationPlayer");
        
        switch (station.WorkshopType) {
            case WorkshopType.Furnace:
                PlayFurnaceAnimation(animator, recipe);
                break;
            case WorkshopType.Anvil:
                PlayAnvilAnimation(animator, recipe);
                break;
            case WorkshopType.Carpentry:
                PlayCarpentryAnimation(animator, recipe);
                break;
            default:
                PlayGenericCraftingAnimation(animator);
                break;
        }
    }
    
    private static void PlayFurnaceAnimation(AnimationPlayer animator, CraftingRecipe recipe) {
        // Glow effect
        var furnaceMesh = animator.GetParent().GetNode<MeshInstance3D>("FurnaceBody");
        var material = furnaceMesh.GetActiveMaterial(0) as StandardMaterial3D;
        if (material != null) {
            material.Emission = new Color(1.0f, 0.3f, 0.0f);
            material.EmissionEnergy = 2.0f;
        }
        
        // Particle effects
        var particles = animator.GetParent().GetNode<GpuParticles3D>("FireParticles");
        particles.Emitting = true;
        
        // Duration based on recipe
        animator.Play("furnace_smelting");
        
        // Sound
        AudioManager.PlayLoop("furnace_burning", furnaceMesh.GlobalPosition);
    }
    
    private static void PlayAnvilAnimation(AnimationPlayer animator, CraftingRecipe recipe) {
        // Hammer strikes synced to animation
        animator.Play("hammer_strikes");
        
        // Sound effects
        animator.Connect("animation_finished", new Callable(() => {
            AudioManager.PlaySound("metal_clang", animator.GlobalPosition);
        }));
    }
}
```

### 6.4 Resource Consumption

```csharp
public class ResourceConsumption {
    
    public bool ConsumeCraftingMaterials(
        CraftingRecipe recipe,
        Entity crafter,
        WorkshopEntity station) {
        
        // Check inventory for materials
        var inventory = crafter.Inventory;
        if (inventory == null) return false;
        
        // Verify all materials present
        foreach (var ingredient in recipe.Ingredients) {
            if (!inventory.HasItem(ingredient.ItemId, ingredient.Quantity)) {
                return false;
            }
        }
        
        // Consume materials
        foreach (var ingredient in recipe.Ingredients) {
            inventory.RemoveItem(ingredient.ItemId, ingredient.Quantity);
        }
        
        // Consume fuel (if applicable)
        if (recipe.RequiresFuel && station != null) {
            station.ConsumeFuel(recipe.FuelAmount);
        }
        
        // Consume tool durability
        if (!string.IsNullOrEmpty(recipe.RequiredTool)) {
            var tool = crafter.Equipment?.GetEquippedTool();
            if (tool != null) {
                tool.Durability -= recipe.ToolWear;
            }
        }
        
        return true;
    }
    
    public void ReturnMaterialsOnCancel(
        CraftingRecipe recipe,
        Entity crafter,
        float progress) {
        
        // Return percentage based on progress
        float returnRatio = 1.0f - progress;
        
        foreach (var ingredient in recipe.Ingredients) {
            int returnAmount = Mathf.FloorToInt(ingredient.Quantity * returnRatio);
            if (returnAmount > 0) {
                crafter.Inventory?.AddItem(ingredient.ItemId, returnAmount);
            }
        }
    }
}
```

---

## 7. Storage System

### 7.1 Container Entities

See StorageContainerEntity (Section 4.4) for implementation.

**Container Hierarchy**:

```
StorageContainerEntity (Base)
├── ChestEntity
│   └── SmallChest (30 slots, 100kg)
│   └── LargeChest (60 slots, 200kg)
├── CrateEntity (20 slots, 50kg, portable)
├── WarehouseEntity (300 slots, 1000kg, multi-user)
└── SafeEntity (10 slots, 50kg, locked, secure)
```

### 7.2 Inventory Capacity

**Capacity Calculation**:
```csharp
public class ContainerCapacity {
    
    public struct CapacityLimits {
        public int SlotCount { get; set; }
        public float WeightLimit { get; set; }
        public float VolumeLimit { get; set; }
    }
    
    public static CapacityLimits GetContainerLimits(ContainerType type) {
        return type switch {
            ContainerType.Chest => new CapacityLimits {
                SlotCount = 30,
                WeightLimit = 100.0f,
                VolumeLimit = 1.0f // m³
            },
            ContainerType.Crate => new CapacityLimits {
                SlotCount = 20,
                WeightLimit = 50.0f,
                VolumeLimit = 0.5f
            },
            ContainerType.Warehouse => new CapacityLimits {
                SlotCount = 300,
                WeightLimit = 1000.0f,
                VolumeLimit = 20.0f
            },
            ContainerType.Safe => new CapacityLimits {
                SlotCount = 10,
                WeightLimit = 50.0f,
                VolumeLimit = 0.3f
            },
            _ => new CapacityLimits { SlotCount = 10, WeightLimit = 50.0f }
        };
    }
    
    public static bool CanAddItem(StorageContainerEntity container, Item item, int quantity) {
        var limits = GetContainerLimits(container.ContainerType);
        var inventory = container.Inventory;
        
        // Check weight
        float itemWeight = item.Weight * quantity;
        if (inventory.CurrentWeight + itemWeight > limits.WeightLimit) {
            return false;
        }
        
        // Check slots (if item doesn't stack or stack is full)
        // ... slot logic
        
        return true;
    }
}
```

### 7.3 Access Permissions

```csharp
public class StoragePermissions {
    
    public enum AccessLevel {
        None,
        View,      // Can see contents but not interact
        Deposit,   // Can add items
        Withdraw,  // Can take items
        Full       // Full access + management
    }
    
    public class PermissionConfig {
        public EntityId Owner { get; set; }
        public AccessLevel DefaultAccess { get; set; } = AccessLevel.None;
        public Dictionary<EntityId, AccessLevel> UserPermissions { get; set; } = new();
        public bool IsLocked { get; set; } = false;
        public string LockKeyId { get; set; }
    }
    
    public static bool CanAccess(
        StorageContainerEntity container,
        Entity user,
        AccessLevel requiredLevel) {
        
        var config = container.GetComponent<SecurityComponent>()?.Config;
        if (config == null) return true; // No security = open access
        
        // Owner has full access
        if (config.Owner == user.Id) return true;
        
        // Check explicit permissions
        if (config.UserPermissions.TryGetValue(user.Id, out var userLevel)) {
            return userLevel >= requiredLevel;
        }
        
        // Check default access
        return config.DefaultAccess >= requiredLevel;
    }
    
    public static bool CanUnlock(
        StorageContainerEntity container,
        Entity user) {
        
        var config = container.GetComponent<SecurityComponent>()?.Config;
        if (config == null || !config.IsLocked) return true;
        
        // Owner can always unlock
        if (config.Owner == user.Id) return true;
        
        // Check for key
        var userKeys = user.Inventory?.GetItemsOfType("key");
        foreach (var key in userKeys) {
            if (key.KeyId == config.LockKeyId) {
                return true;
            }
        }
        
        // Check lockpicking skill
        var lockpickSkill = user.Skills?.GetLevel(SkillType.Lockpicking);
        if (lockpickSkill > 3) {
            // Chance to pick lock
            float pickChance = lockpickSkill * 0.1f;
            return GD.Randf() < pickChance;
        }
        
        return false;
    }
}
```

### 7.4 Physical Placement

```csharp
public class ContainerPlacement {
    
    public static PlacementResult ValidateContainerPlacement(
        ContainerType type,
        Vector3 position,
        Entity placer) {
        
        var result = new PlacementResult();
        
        // Check terrain is flat enough
        if (!IsTerrainFlat(position, 0.3f)) {
            result.AddError("Surface is too uneven");
        }
        
        // Check clearance
        var bounds = GetContainerBounds(type);
        if (IsCollisionAt(bounds)) {
            result.AddError("Space is blocked");
        }
        
        // Check permissions
        if (!HasBuildPermission(placer, position)) {
            result.AddError("No permission to build here");
        }
        
        // Special requirements
        switch (type) {
            case ContainerType.Warehouse:
                // Requires foundation
                if (!HasFoundation(position, bounds.Size)) {
                    result.AddError("Warehouse requires foundation");
                }
                break;
                
            case ContainerType.Safe:
                // Cannot place outdoors
                if (IsExposedToWeather(position)) {
                    result.AddWarning("Safe will be exposed to weather");
                }
                break;
        }
        
        return result;
    }
    
    private static bool IsTerrainFlat(Vector3 position, float tolerance) {
        // Sample height at corners
        float h1 = World.GetTerrainHeight(position + new Vector3(-0.5f, 0, -0.5f));
        float h2 = World.GetTerrainHeight(position + new Vector3(0.5f, 0, -0.5f));
        float h3 = World.GetTerrainHeight(position + new Vector3(-0.5f, 0, 0.5f));
        float h4 = World.GetTerrainHeight(position + new Vector3(0.5f, 0, 0.5f));
        
        float maxDiff = Mathf.Max(
            Mathf.Abs(h1 - h2),
            Mathf.Abs(h1 - h3),
            Mathf.Abs(h1 - h4)
        );
        
        return maxDiff <= tolerance;
    }
}
```

---

## 8. Construction and Placement

### 8.1 Entity Placement Mechanics

```csharp
public class EntityPlacementSystem {
    
    public class PlacementContext {
        public Entity Placer { get; set; }
        public VoxelEntity EntityPrefab { get; set; }
        public Vector3 TargetPosition { get; set; }
        public Vector3 TargetRotation { get; set; }
        public bool IsGhostPreview { get; set; } = false;
    }
    
    public static PlacementResult ValidatePlacement(PlacementContext context) {
        var result = new PlacementResult();
        var entity = context.EntityPrefab;
        var pos = context.TargetPosition;
        
        // 1. Check range
        float distance = context.Placer.GlobalPosition.DistanceTo(pos);
        if (distance > 3.0f) {
            result.AddError("Too far away");
            return result;
        }
        
        // 2. Check line of sight
        if (!HasLineOfSight(context.Placer, pos)) {
            result.AddError("No line of sight");
            return result;
        }
        
        // 3. Check collision
        var bounds = GetEntityBounds(entity);
        bounds.Position += pos;
        if (Physics.CheckBox(bounds.Center, bounds.Size / 2)) {
            result.AddError("Space is occupied");
            return result;
        }
        
        // 4. Check terrain support
        if (!HasGroundSupport(pos, bounds)) {
            result.AddError("No ground support");
            return result;
        }
        
        // 5. Check claim permissions
        if (!HasBuildPermission(context.Placer, pos)) {
            result.AddError("No building permission here");
            return result;
        }
        
        // 6. Check materials (if not ghost)
        if (!context.IsGhostPreview) {
            if (!HasRequiredMaterials(context.Placer, entity)) {
                result.AddError("Missing required materials");
                return result;
            }
        }
        
        result.IsValid = !result.HasErrors;
        return result;
    }
    
    public static VoxelEntity PlaceEntity(PlacementContext context) {
        // Validate
        var validation = ValidatePlacement(context);
        if (!validation.IsValid) {
            return null;
        }
        
        // Consume materials
        ConsumeMaterials(context.Placer, context.EntityPrefab);
        
        // Spawn entity
        var entity = context.EntityPrefab.Duplicate() as VoxelEntity;
        entity.GlobalPosition = context.TargetPosition;
        entity.GlobalRotation = context.TargetRotation;
        
        // Add to world
        GetTree().Root.AddChild(entity);
        
        // Initialize
        entity.OnPlaced(new EntityPlacementContext {
            Placer = context.Placer,
            Position = context.TargetPosition
        });
        
        // Network sync
        if (Multiplayer.IsServer()) {
            SyncEntityPlacement(entity);
        }
        
        return entity;
    }
    
    private static void SyncEntityPlacement(VoxelEntity entity) {
        // RPC to clients
        entity.Rpc(nameof(entity.SyncSpawn), 
            entity.Id,
            (int)entity.EntityType,
            entity.GlobalPosition,
            entity.GlobalRotation);
    }
}
```

### 8.2 Foundation Requirements

```csharp
public class FoundationRequirements {
    
    public static bool CheckFoundation(
        EntityType entityType,
        Vector3 position,
        Vector3 size) {
        
        switch (entityType) {
            case EntityType.Warehouse:
                return CheckWarehouseFoundation(position, size);
                
            case EntityType.Workshop when size.Y > 1.5f:
                return CheckWorkshopFoundation(position, size);
                
            case EntityType.Door:
                // Doors need wall support, not foundation
                return CheckWallSupport(position, size);
                
            default:
                // Light entities just need flat ground
                return IsGroundFlat(position, size);
        }
    }
    
    private static bool CheckWarehouseFoundation(Vector3 pos, Vector3 size) {
        // Warehouse needs 1m deep foundation
        var corners = new Vector3[] {
            pos + new Vector3(-size.X/2, 0, -size.Z/2),
            pos + new Vector3(size.X/2, 0, -size.Z/2),
            pos + new Vector3(-size.X/2, 0, size.Z/2),
            pos + new Vector3(size.X/2, 0, size.Z/2)
        };
        
        foreach (var corner in corners) {
            // Check 1m below
            Vector3 checkPos = corner + new Vector3(0, -1, 0);
            if (World.GetBlock(checkPos).IsAir) {
                return false;
            }
        }
        
        return true;
    }
}
```

### 8.3 Alignment and Rotation

```csharp
public class PlacementAlignment {
    
    public static Vector3 AlignPosition(Vector3 rawPosition, AlignmentMode mode) {
        return mode switch {
            AlignmentMode.VoxelGrid => AlignToVoxelGrid(rawPosition),
            AlignmentMode.HalfGrid => AlignToHalfGrid(rawPosition),
            AlignmentMode.Surface => AlignToSurface(rawPosition),
            AlignmentMode.Free => rawPosition,
            _ => rawPosition
        };
    }
    
    public static Vector3 AlignToVoxelGrid(Vector3 pos) {
        return new Vector3(
            Mathf.Snapped(pos.X, 1.0f) + 0.5f,
            Mathf.Snapped(pos.Y, 1.0f),
            Mathf.Snapped(pos.Z, 1.0f) + 0.5f
        );
    }
    
    public static Vector3 AlignToHalfGrid(Vector3 pos) {
        return new Vector3(
            Mathf.Snapped(pos.X, 0.5f),
            Mathf.Snapped(pos.Y, 0.5f),
            Mathf.Snapped(pos.Z, 0.5f)
        );
    }
    
    public static Vector3 AlignRotation(Vector3 rawRotation, RotationMode mode) {
        return mode switch {
            RotationMode.Snap90 => SnapTo90Degrees(rawRotation),
            RotationMode.Snap45 => SnapTo45Degrees(rawRotation),
            RotationMode.Free => rawRotation,
            _ => rawRotation
        };
    }
    
    private static Vector3 SnapTo90Degrees(Vector3 rot) {
        return new Vector3(
            rot.X,
            Mathf.Snapped(rot.Y, Mathf.Pi / 2),
            rot.Z
        );
    }
}

public enum AlignmentMode {
    VoxelGrid,   // Snap to 1m grid
    HalfGrid,    // Snap to 0.5m grid
    Surface,     // Align to terrain surface
    Free         // No snapping
}

public enum RotationMode {
    Snap90,      // 90 degree increments
    Snap45,      // 45 degree increments
    Free         // Any angle
}
```

### 8.4 Building Permission Checks

```csharp
public class BuildingPermissions {
    
    public static bool CanBuildAt(Entity builder, Vector3 position) {
        // 1. Check if bedrock layer
        if (position.Y <= -200) {
            return false; // Bedrock is immutable
        }
        
        // 2. Check jurisdiction/claims
        var jurisdiction = GovernanceSystem.GetJurisdictionAt(position);
        if (jurisdiction != null) {
            var claim = jurisdiction.GetClaimAt(position.X, position.Z);
            
            if (claim != null) {
                // Private claim - check ownership
                if (claim.OwnerId == builder.Id) {
                    return true;
                }
                
                // Check explicit permissions
                return claim.HasPermission(builder.Id, Permission.Build);
            }
            
            // Public land within jurisdiction - check laws
            return jurisdiction.HasPermission(builder, Permission.BuildOnPublicLand);
        }
        
        // 3. Unclaimed land - free to build (with limits)
        return true;
    }
    
    public static bool CanPlaceEntity(Entity placer, VoxelEntity entity, Vector3 position) {
        // Basic build check
        if (!CanBuildAt(placer, position)) {
            return false;
        }
        
        // Entity-specific checks
        if (entity is DoorEntity && !HasWallSupport(position)) {
            return false;
        }
        
        if (entity is WorkshopEntity workshop) {
            // Workshops may need additional permits
            var jurisdiction = GovernanceSystem.GetJurisdictionAt(position);
            if (jurisdiction?.RequiresWorkshopPermit == true) {
                return jurisdiction.HasWorkshopPermit(placer, workshop.WorkshopType);
            }
        }
        
        return true;
    }
}
```

---

## 9. Destruction and Salvage

### 9.1 Entity Durability

```csharp
public class EntityDurabilitySystem {
    
    public class DurabilityConfig {
        public float MaxDurability { get; set; } = 100.0f;
        public float CurrentDurability { get; set; }
        public DamageType Weakness { get; set; }
        public DamageType Resistance { get; set; }
        public float DecayRate { get; set; } = 0.0f; // Per day
    }
    
    public static float CalculateDamage(
        VoxelEntity entity,
        DamageSource source,
        float baseDamage) {
        
        var config = entity.GetComponent<DurabilityComponent>()?.Config;
        if (config == null) return baseDamage;
        
        float damage = baseDamage;
        
        // Apply weaknesses/resistances
        if (source.DamageType == config.Weakness) {
            damage *= 1.5f;
        }
        if (source.DamageType == config.Resistance) {
            damage *= 0.5f;
        }
        
        // Tool effectiveness
        if (source.Tool != null) {
            damage *= GetToolEffectiveness(source.Tool, entity);
        }
        
        return damage;
    }
    
    public static void ApplyDamage(VoxelEntity entity, float damage) {
        var durability = entity.GetComponent<DurabilityComponent>();
        if (durability == null) return;
        
        durability.CurrentDurability -= damage;
        
        // Visual feedback
        ShowDamageVisuals(entity, damage);
        
        // Check destruction
        if (durability.CurrentDurability <= 0) {
            DestroyEntity(entity);
        }
    }
    
    private static void ShowDamageVisuals(VoxelEntity entity, float damage) {
        // Flash red or show particle effect
        var flash = new Color(1, 0, 0, 0.5f);
        // Apply to material temporarily
        
        // Particle effect
        var particles = new GpuParticles3D();
        particles.Amount = Mathf.Min((int)damage * 10, 50);
        // Configure particle effect
        entity.AddChild(particles);
    }
    
    private static void DestroyEntity(VoxelEntity entity) {
        var context = new DestructionContext {
            Destroyer = null, // Could track who destroyed it
            DamageType = DamageType.Destruction,
            Position = entity.GlobalPosition
        };
        
        entity.OnDestroyed(context);
    }
}
```

### 9.2 Breaking into Components

```csharp
public class ComponentBreakdown {
    
    public class SalvageComponent {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public float DropChance { get; set; } = 1.0f;
        public bool RequiresTool { get; set; } = false;
    }
    
    public static List<SalvageComponent> GetComponents(VoxelEntity entity) {
        return entity.EntityType switch {
            EntityType.WoodenCart => new List<SalvageComponent> {
                new() { ItemId = "wood", Quantity = 8, DropChance = 0.8f },
                new() { ItemId = "iron_nails", Quantity = 4, DropChance = 0.6f },
                new() { ItemId = "wheel", Quantity = 4, DropChance = 0.5f }
            },
            EntityType.Chest => new List<SalvageComponent> {
                new() { ItemId = "wood", Quantity = 5, DropChance = 0.9f },
                new() { ItemId = "iron_hinge", Quantity = 2, DropChance = 0.7f }
            },
            EntityType.Workbench => new List<SalvageComponent> {
                new() { ItemId = "wood", Quantity = 10, DropChance = 0.85f },
                new() { ItemId = "iron_nails", Quantity = 3, DropChance = 0.75f }
            },
            EntityType.Bed => new List<SalvageComponent> {
                new() { ItemId = "wood", Quantity = 6, DropChance = 0.8f },
                new() { ItemId = "cloth", Quantity = 3, DropChance = 0.6f }
            },
            _ => new List<SalvageComponent>()
        };
    }
    
    public static void SpawnComponents(VoxelEntity entity, DestructionContext context) {
        var components = GetComponents(entity);
        var position = entity.GlobalPosition;
        
        foreach (var component in components) {
            // Check drop chance
            if (GD.Randf() > component.DropChance) continue;
            
            // Check tool requirement
            if (component.RequiresTool && context.Tool == null) {
                component.Quantity = Mathf.Max(1, component.Quantity / 2);
            }
            
            // Spawn debris/loot
            for (int i = 0; i < component.Quantity; i++) {
                Vector3 offset = new Vector3(
                    GD.Randf() - 0.5f,
                    GD.Randf() * 0.5f,
                    GD.Randf() - 0.5f
                ) * 2.0f;
                
                SpawnItemDrop(component.ItemId, 1, position + offset);
            }
        }
    }
}
```

### 9.3 Salvage Materials

```csharp
public class SalvageSystem {
    
    public static void SalvageEntity(Entity salvager, VoxelEntity target) {
        // Check tool
        var tool = salvager.Equipment?.GetEquippedTool();
        if (tool?.ToolType != "hammer") {
            salvager.ShowMessage("You need a hammer to salvage this");
            return;
        }
        
        // Check range
        if (salvager.GlobalPosition.DistanceTo(target.GlobalPosition) > 3.0f) {
            return;
        }
        
        // Permission check
        if (target.Owner != null && target.Owner.Id != salvager.Id) {
            salvager.ShowMessage("You don't own this");
            return;
        }
        
        // Calculate salvage yield
        float yieldMultiplier = CalculateYieldMultiplier(tool, salvager.Skills);
        
        // Spawn materials
        var components = ComponentBreakdown.GetComponents(target);
        foreach (var component in components) {
            int quantity = Mathf.FloorToInt(component.Quantity * yieldMultiplier);
            if (quantity > 0) {
                salvager.Inventory?.AddItem(component.ItemId, quantity);
            }
        }
        
        // Destroy entity
        target.OnDestroyed(new DestructionContext {
            Destroyer = salvager,
            IsSalvage = true
        });
        
        // Effects
        AudioManager.PlaySound("salvage_deconstruct", target.GlobalPosition);
        SpawnSalvageParticles(target.GlobalPosition);
    }
    
    private static float CalculateYieldMultiplier(ToolInstance tool, SkillSet skills) {
        float multiplier = 0.5f; // Base 50% return
        
        // Tool quality bonus
        multiplier += (int)tool.Quality * 0.05f;
        
        // Skill bonus
        float craftingSkill = skills?.GetLevel(SkillType.Crafting) ?? 0;
        multiplier += craftingSkill * 0.03f;
        
        return Mathf.Clamp(multiplier, 0.5f, 0.95f); // Max 95% return
    }
}
```

### 9.4 Rubble System Integration

**Eco-Style Debris from Entity Destruction**:
```csharp
public class EntityRubbleSystem {
    
    public static void SpawnRubbleFromEntity(
        VoxelEntity entity,
        DestructionContext context) {
        
        // Get entity mesh info
        var meshes = GetMeshInstances(entity);
        
        // Spawn debris chunks based on mesh material
        foreach (var mesh in meshes) {
            int chunkCount = CalculateChunkCount(mesh);
            
            for (int i = 0; i < chunkCount; i++) {
                SpawnDebrisChunk(entity, mesh, i, chunkCount);
            }
        }
        
        // Large entities spawn more debris
        if (entity.Mass > 50.0f) {
            SpawnLargeDebrisPile(entity.GlobalPosition, entity.Mass);
        }
    }
    
    private static void SpawnDebrisChunk(
        VoxelEntity entity,
        MeshInstance3D sourceMesh,
        int index,
        int total) {
        
        var chunk = new RigidBody3D();
        
        // Random small mesh
        var mesh = new MeshInstance3D();
        mesh.Mesh = CreateRandomDebrisMesh();
        mesh.Scale = Vector3.One * (0.2f + GD.Randf() * 0.3f);
        chunk.AddChild(mesh);
        
        // Collision
        var shape = new BoxShape3D();
        shape.Size = mesh.Scale;
        
        var collision = new CollisionShape3D();
        collision.Shape = shape;
        chunk.AddChild(collision);
        
        // Physics
        chunk.Mass = 2.0f + GD.Randf() * 5.0f;
        chunk.GlobalPosition = entity.GlobalPosition + RandomOffset();
        
        // Explosive scatter
        chunk.LinearVelocity = new Vector3(
            (GD.Randf() - 0.5f) * 5.0f,
            GD.Randf() * 3.0f,
            (GD.Randf() - 0.5f) * 5.0f
        );
        
        // Add to world
        GetTree().Root.AddChild(chunk);
        
        // Auto-cleanup after 5 minutes
        chunk.GetTree().CreateTimer(300.0f).Timeout += () => {
            FadeAndRemove(chunk);
        };
    }
    
    private static void FadeAndRemove(RigidBody3D debris) {
        // Fade out
        var tween = debris.CreateTween();
        tween.TweenProperty(debris, "modulate", new Color(1, 1, 1, 0), 1.0f);
        tween.TweenCallback(Callable.From(() => debris.QueueFree()));
    }
}
```

---

## 10. Technical Implementation

### 10.1 Entity Class Hierarchy

```csharp
// Complete class hierarchy

VoxelEntity (extends RigidBody3D)
├── VehicleEntity
│   ├── MinecartEntity
│   ├── WoodenCartEntity
│   └── MotorVehicleEntity (future)
├── StorageEntity
│   ├── ChestEntity
│   ├── CrateEntity
│   └── WarehouseEntity
├── WorkshopEntity
│   ├── WorkbenchEntity
│   ├── FurnaceEntity
│   └── AnvilEntity
├── FurnitureEntity
│   ├── BedEntity
│   ├── ChairEntity
│   └── TableEntity
└── MechanismEntity
    ├── DoorEntity
    └── ElevatorEntity (future)

// Component System (composition over inheritance)
VoxelEntity
├── InventoryComponent
├── InteractionComponent
├── DurabilityComponent
├── CraftingComponent
├── SecurityComponent
└── VisualComponent
```

### 10.2 VoxelEntity Base Class (Complete)

```csharp
using Godot;
using System;
using System.Collections.Generic;

namespace Societies.Entities {
    
    public partial class VoxelEntity : RigidBody3D {
        
        #region Signals
        [Signal]
        public delegate void PlacedEventHandler();
        
        [Signal]
        public delegate void PickedUpEventHandler(Entity collector);
        
        [Signal]
        public delegate void DroppedEventHandler(Vector3 position);
        
        [Signal]
        public delegate void DestroyedEventHandler(DestructionContext context);
        
        [Signal]
        public delegate void InteractedEventHandler(Entity interactor);
        #endregion
        
        #region Exported Properties
        [Export] public EntityId Id { get; set; } = EntityId.Invalid;
        [Export] public EntityType EntityType { get; set; } = EntityType.Generic;
        [Export] public string DisplayName { get; set; } = "Entity";
        [Export] public string Description { get; set; } = "";
        
        [ExportGroup("Physics")]
        [Export] public float BaseMass { get; set; } = 10.0f;
        [Export] public bool IsPushable { get; set; } = true;
        [Export] public bool IsLiftable { get; set; } = false;
        [Export] public float DragCoefficient { get; set; } = 0.1f;
        [Export] public bool CanSleep { get; set; } = true;
        
        [ExportGroup("Placement")]
        [Export] public bool SnapToGrid { get; set; } = true;
        [Export] public float GridSize { get; set; } = 1.0f;
        [Export] public Vector3 GridOffset { get; set; } = new Vector3(0.5f, 0, 0.5f);
        [Export] public bool SnapRotation { get; set; } = true;
        
        [ExportGroup("Network")]
        [Export] public bool IsServerAuthoritative { get; set; } = true;
        #endregion
        
        #region State Properties
        public EntityState CurrentState { get; protected set; } = EntityState.InInventory;
        public EntityOwner Owner { get; set; }
        public int LastUpdateTick { get; set; }
        
        private Vector3 _lastValidPosition;
        private Vector3 _lastValidVelocity;
        #endregion
        
        #region Components
        protected Dictionary<Type, EntityComponent> _components = new();
        
        public T GetComponent<T>() where T : EntityComponent {
            if (_components.TryGetValue(typeof(T), out var component)) {
                return component as T;
            }
            return null;
        }
        
        public void AddComponent<T>(T component) where T : EntityComponent {
            _components[typeof(T)] = component;
            AddChild(component);
        }
        #endregion
        
        #region Godot Lifecycle
        public override void _Ready() {
            base._Ready();
            
            // Generate ID if not set
            if (Id == EntityId.Invalid) {
                Id = EntityId.Generate();
            }
            
            // Setup physics
            Mass = BaseMass;
            this.CanSleep = this.CanSleep;
            
            // Setup collision layers
            CollisionLayer = (uint)PhysicsLayers.Entities;
            CollisionMask = (uint)(PhysicsLayers.Terrain | PhysicsLayers.Entities | 
                                  PhysicsLayers.Vehicles | PhysicsLayers.Debris);
            
            // Initialize
            InitializeComponents();
            
            // Register with manager
            EntityManager.Instance?.RegisterEntity(this);
            
            _lastValidPosition = GlobalPosition;
            _lastValidVelocity = LinearVelocity;
        }
        
        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            
            // Server-side validation
            if (IsServerAuthoritative && Multiplayer.IsServer()) {
                ValidatePhysicsState();
            }
            
            // Save valid state
            if (IsValidState()) {
                _lastValidPosition = GlobalPosition;
                _lastValidVelocity = LinearVelocity;
            }
        }
        
        public override void _ExitTree() {
            base._ExitTree();
            EntityManager.Instance?.UnregisterEntity(this);
        }
        #endregion
        
        #region Virtual Methods
        protected virtual void InitializeComponents() {
            // Override in subclasses
        }
        
        public virtual void OnPlaced(EntityPlacementContext context) {
            CurrentState = EntityState.Placed;
            
            if (SnapToGrid) {
                GlobalPosition = SnapPositionToGrid(GlobalPosition);
            }
            
            Freeze = !IsPushable; // Static if not pushable
            Owner = context.Placer != null ? new EntityOwner { Id = context.Placer.Id } : null;
            
            EmitSignal(SignalName.Placed);
            
            // Network sync
            if (Multiplayer.IsServer()) {
                Rpc(nameof(SyncPlacement), GlobalPosition, GlobalRotation, 
                    Owner?.Id ?? Guid.Empty);
            }
        }
        
        public virtual void OnPickedUp(Entity collector) {
            CurrentState = EntityState.BeingCarried;
            Freeze = true;
            
            EmitSignal(SignalName.PickedUp, collector);
        }
        
        public virtual void OnDropped(Vector3 position) {
            CurrentState = EntityState.Placed;
            
            if (SnapToGrid) {
                position = SnapPositionToGrid(position);
            }
            
            GlobalPosition = position;
            Freeze = false;
            
            EmitSignal(SignalName.Dropped, position);
        }
        
        public virtual void OnDestroyed(DestructionContext context) {
            CurrentState = EntityState.Destroyed;
            
            SpawnSalvage(context);
            SpawnRubble(context);
            
            EmitSignal(SignalName.Destroyed, context);
            
            // Network destroy
            if (Multiplayer.IsServer()) {
                Rpc(nameof(SyncDestruction));
            }
            
            QueueFree();
        }
        
        public virtual void OnInteract(Entity interactor) {
            EmitSignal(SignalName.Interacted, interactor);
        }
        
        protected virtual void SpawnSalvage(DestructionContext context) {
            var components = ComponentBreakdown.GetComponents(this);
            foreach (var component in components) {
                if (GD.Randf() <= component.DropChance) {
                    for (int i = 0; i < component.Quantity; i++) {
                        SpawnItemDrop(component.ItemId, GlobalPosition + RandomOffset());
                    }
                }
            }
        }
        
        protected virtual void SpawnRubble(DestructionContext context) {
            EntityRubbleSystem.SpawnRubbleFromEntity(this, context);
        }
        #endregion
        
        #region Network Synchronization
        [Rpc(MultiplayerApi.RpcMode.Authority)]
        public void SyncPlacement(Vector3 position, Vector3 rotation, Guid ownerId) {
            if (Multiplayer.IsServer()) return;
            
            GlobalPosition = position;
            GlobalRotation = rotation;
            if (ownerId != Guid.Empty) {
                Owner = new EntityOwner { Id = ownerId };
            }
        }
        
        [Rpc(MultiplayerApi.RpcMode.Authority)]
        public void SyncDestruction() {
            if (Multiplayer.IsServer()) return;
            QueueFree();
        }
        
        [Rpc(MultiplayerApi.RpcMode.Authority)]
        public void SyncPhysicsState(Vector3 position, Vector3 velocity, Vector3 rotation) {
            if (Multiplayer.IsServer()) return;
            
            GlobalPosition = position;
            LinearVelocity = velocity;
            GlobalRotation = rotation;
        }
        #endregion
        
        #region Helper Methods
        private Vector3 SnapPositionToGrid(Vector3 position) {
            return new Vector3(
                Mathf.Snapped(position.X - GridOffset.X, GridSize) + GridOffset.X,
                Mathf.Snapped(position.Y - GridOffset.Y, GridSize) + GridOffset.Y,
                Mathf.Snapped(position.Z - GridOffset.Z, GridSize) + GridOffset.Z
            );
        }
        
        private void ValidatePhysicsState() {
            // Check for unreasonable velocity
            if (LinearVelocity.Length() > 50.0f) {
                LinearVelocity = _lastValidVelocity;
            }
            
            // Check for out of bounds
            if (GlobalPosition.Y < -250) {
                GlobalPosition = _lastValidPosition;
                LinearVelocity = Vector3.Zero;
            }
            
            // Check for NaN
            if (float.IsNaN(GlobalPosition.X) || float.IsNaN(LinearVelocity.X)) {
                GlobalPosition = _lastValidPosition;
                LinearVelocity = Vector3.Zero;
            }
        }
        
        private bool IsValidState() {
            return !float.IsNaN(GlobalPosition.X) && 
                   GlobalPosition.Y > -250 && 
                   LinearVelocity.Length() < 50.0f;
        }
        
        private Vector3 RandomOffset() {
            return new Vector3(
                (GD.Randf() - 0.5f) * 2.0f,
                GD.Randf() * 0.5f,
                (GD.Randf() - 0.5f) * 2.0f
            );
        }
        
        private void SpawnItemDrop(string itemId, Vector3 position) {
            // Spawn physical item drop
            var drop = ItemDropEntity.Create(itemId, 1);
            drop.GlobalPosition = position;
            GetTree().Root.AddChild(drop);
        }
        
        private Mesh LoadMesh(string path) {
            return GD.Load<Mesh>(path) ?? new BoxMesh();
        }
        #endregion
    }
    
    #region Supporting Types
    public struct EntityId : IEquatable<EntityId> {
        public Guid Value { get; set; }
        
        public static EntityId Invalid => new EntityId { Value = Guid.Empty };
        
        public static EntityId Generate() {
            return new EntityId { Value = Guid.NewGuid() };
        }
        
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
    
    public enum EntityType {
        Generic,
        Vehicle,
        Minecart,
        WoodenCart,
        StorageContainer,
        Workshop,
        Furniture,
        Door,
        Mechanism
    }
    
    public enum EntityState {
        InInventory,
        BeingCarried,
        Placed,
        InUse,
        Destroyed
    }
    
    public class EntityOwner {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
    
    public class EntityPlacementContext {
        public Entity Placer { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
    }
    
    public class DestructionContext {
        public Entity Destroyer { get; set; }
        public DamageType DamageType { get; set; }
        public ToolInstance Tool { get; set; }
        public Vector3 Position { get; set; }
        public bool IsSalvage { get; set; }
    }
    
    public enum DamageType {
        Physical,
        Fire,
        Water,
        Explosion,
        Tool,
        Destruction
    }
    #endregion
}
```

### 10.3 Placement Validation

```csharp
public class PlacementValidator {
    
    public struct PlacementResult {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        public void AddError(string error) {
            Errors.Add(error);
            IsValid = false;
        }
        
        public void AddWarning(string warning) {
            Warnings.Add(warning);
        }
        
        public bool HasErrors => Errors.Count > 0;
    }
    
    public static PlacementResult Validate(
        VoxelEntity entity,
        Vector3 position,
        Entity placer) {
        
        var result = new PlacementResult { IsValid = true };
        
        // Check range
        float distance = placer.GlobalPosition.DistanceTo(position);
        if (distance > 3.0f) {
            result.AddError("Target is too far away");
        }
        
        // Check collision
        var bounds = CalculateBounds(entity, position);
        if (CheckCollision(bounds)) {
            result.AddError("Space is occupied by another object");
        }
        
        // Check terrain collision
        if (CheckTerrainCollision(bounds)) {
            result.AddError("Cannot place inside terrain");
        }
        
        // Check ground support
        if (!CheckGroundSupport(bounds)) {
            result.AddError("No ground support - object would fall");
        }
        
        // Check permissions
        if (!BuildingPermissions.CanBuildAt(placer, position)) {
            result.AddError("No permission to build here");
        }
        
        // Check materials
        if (!HasRequiredMaterials(placer, entity)) {
            result.AddError("Missing required materials");
        }
        
        // Check slope
        float slope = GetTerrainSlope(position);
        if (slope > 30.0f) {
            result.AddWarning("Steep slope - object may slide");
        }
        
        return result;
    }
    
    private static Bounds CalculateBounds(VoxelEntity entity, Vector3 position) {
        var aabb = entity.GetAabb();
        return new Bounds {
            Min = aabb.Position + position,
            Max = aabb.End + position
        };
    }
    
    private static bool CheckCollision(Bounds bounds) {
        var space = PhysicsServer3D.SpaceGetDirectState(GetWorld3D().Space);
        var query = new PhysicsShapeQueryParameters3D();
        
        var shape = new BoxShape3D();
        shape.Size = bounds.Size;
        query.Shape = shape;
        query.Transform = new Transform3D(Basis.Identity, bounds.Center);
        
        var results = space.IntersectShape(query);
        return results.Count > 0;
    }
    
    private static bool CheckTerrainCollision(Bounds bounds) {
        // Check if any voxel within bounds is solid
        for (int x = Mathf.FloorToInt(bounds.Min.X); x < Mathf.CeilToInt(bounds.Max.X); x++) {
            for (int y = Mathf.FloorToInt(bounds.Min.Y); y < Mathf.CeilToInt(bounds.Max.Y); y++) {
                for (int z = Mathf.FloorToInt(bounds.Min.Z); z < Mathf.CeilToInt(bounds.Max.Z); z++) {
                    var block = World.GetBlock(x, y, z);
                    if (!block.IsAir) {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    private static bool CheckGroundSupport(Bounds bounds) {
        // Check if bottom of bounds has support
        float bottomY = bounds.Min.Y;
        
        // Sample points on bottom face
        var points = new Vector3[] {
            new Vector3(bounds.Min.X + 0.1f, bottomY - 0.1f, bounds.Min.Z + 0.1f),
            new Vector3(bounds.Max.X - 0.1f, bottomY - 0.1f, bounds.Min.Z + 0.1f),
            new Vector3(bounds.Min.X + 0.1f, bottomY - 0.1f, bounds.Max.Z - 0.1f),
            new Vector3(bounds.Max.X - 0.1f, bottomY - 0.1f, bounds.Max.Z - 0.1f),
            new Vector3((bounds.Min.X + bounds.Max.X) / 2, bottomY - 0.1f, 
                       (bounds.Min.Z + bounds.Max.Z) / 2)
        };
        
        int supportedPoints = 0;
        foreach (var point in points) {
            var block = World.GetBlock(Mathf.FloorToInt(point.X), 
                                      Mathf.FloorToInt(point.Y), 
                                      Mathf.FloorToInt(point.Z));
            if (!block.IsAir) {
                supportedPoints++;
            }
        }
        
        // Require at least 3/5 support points
        return supportedPoints >= 3;
    }
    
    private static float GetTerrainSlope(Vector3 position) {
        // Sample heights in cardinal directions
        float hCenter = World.GetTerrainHeight(position);
        float hNorth = World.GetTerrainHeight(position + new Vector3(0, 0, 1));
        float hSouth = World.GetTerrainHeight(position + new Vector3(0, 0, -1));
        float hEast = World.GetTerrainHeight(position + new Vector3(1, 0, 0));
        float hWest = World.GetTerrainHeight(position + new Vector3(-1, 0, 0));
        
        // Calculate max slope
        float maxDiff = Mathf.Max(
            Mathf.Abs(hCenter - hNorth),
            Mathf.Abs(hCenter - hSouth),
            Mathf.Abs(hCenter - hEast),
            Mathf.Abs(hCenter - hWest)
        );
        
        return Mathf.RadToDeg(Mathf.Atan(maxDiff));
    }
    
    private static bool HasRequiredMaterials(Entity placer, VoxelEntity entity) {
        // Get materials needed to build this entity
        var materials = GetBuildMaterials(entity);
        
        foreach (var material in materials) {
            if (!placer.Inventory?.HasItem(material.Key, material.Value) == true) {
                return false;
            }
        }
        
        return true;
    }
    
    private static Dictionary<string, int> GetBuildMaterials(VoxelEntity entity) {
        // Return materials needed to construct this entity type
        return entity.EntityType switch {
            EntityType.WoodenCart => new Dictionary<string, int> {
                { "wood", 8 },
                { "iron_nails", 4 },
                { "wheel", 4 }
            },
            EntityType.Chest => new Dictionary<string, int> {
                { "wood", 5 },
                { "iron_hinge", 2 }
            },
            _ => new Dictionary<string, int>()
        };
    }
    
    public struct Bounds {
        public Vector3 Min;
        public Vector3 Max;
        
        public Vector3 Center => (Min + Max) / 2;
        public Vector3 Size => Max - Min;
    }
}
```

### 10.4 Network Synchronization

```csharp
public class EntityNetworkSync {
    
    private const float SYNC_INTERVAL = 0.1f; // 10 TPS
    private const float POSITION_THRESHOLD = 0.01f;
    private const float ROTATION_THRESHOLD = 1.0f; // degrees
    
    private VoxelEntity _entity;
    private double _timeSinceLastSync;
    private Vector3 _lastSyncedPosition;
    private Vector3 _lastSyncedRotation;
    private Vector3 _lastSyncedVelocity;
    
    public void Initialize(VoxelEntity entity) {
        _entity = entity;
        _lastSyncedPosition = entity.GlobalPosition;
        _lastSyncedRotation = entity.GlobalRotation;
        _lastSyncedVelocity = entity.LinearVelocity;
    }
    
    public void Update(double delta) {
        if (!Multiplayer.IsServer()) return;
        
        _timeSinceLastSync += delta;
        
        if (_timeSinceLastSync >= SYNC_INTERVAL) {
            if (HasSignificantChange()) {
                SyncToClients();
                _timeSinceLastSync = 0;
            }
        }
    }
    
    private bool HasSignificantChange() {
        float posDiff = _entity.GlobalPosition.DistanceTo(_lastSyncedPosition);
        float rotDiff = _entity.GlobalRotation.DistanceTo(_lastSyncedRotation);
        float velDiff = _entity.LinearVelocity.DistanceTo(_lastSyncedVelocity);
        
        return posDiff > POSITION_THRESHOLD || 
               rotDiff > ROTATION_THRESHOLD || 
               velDiff > 0.1f;
    }
    
    private void SyncToClients() {
        _entity.Rpc(nameof(_entity.SyncPhysicsState),
            _entity.GlobalPosition,
            _entity.LinearVelocity,
            _entity.GlobalRotation);
        
        _lastSyncedPosition = _entity.GlobalPosition;
        _lastSyncedRotation = _entity.GlobalRotation;
        _lastSyncedVelocity = _entity.LinearVelocity;
    }
    
    // Client-side interpolation
    public static class Interpolation {
        
        public struct Snapshot {
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 Velocity;
            public double Timestamp;
        }
        
        private Queue<Snapshot> _snapshotBuffer = new();
        private const int BUFFER_SIZE = 6; // 600ms buffer
        private const double INTERP_DELAY = 0.1; // 100ms behind
        
        public void AddSnapshot(Snapshot snapshot) {
            _snapshotBuffer.Enqueue(snapshot);
            
            // Trim old snapshots
            double cutoffTime = snapshot.Timestamp - (INTERP_DELAY * 2);
            while (_snapshotBuffer.Count > 0 && 
                   _snapshotBuffer.Peek().Timestamp < cutoffTime) {
                _snapshotBuffer.Dequeue();
            }
        }
        
        public Snapshot Interpolate(double renderTime) {
            double targetTime = renderTime - INTERP_DELAY;
            
            // Find surrounding snapshots
            Snapshot? before = null;
            Snapshot? after = null;
            
            foreach (var snap in _snapshotBuffer) {
                if (snap.Timestamp <= targetTime) {
                    before = snap;
                } else if (snap.Timestamp > targetTime && after == null) {
                    after = snap;
                    break;
                }
            }
            
            if (before == null) return after ?? default;
            if (after == null) return before.Value;
            
            // Interpolate
            double t = (targetTime - before.Value.Timestamp) / 
                      (after.Value.Timestamp - before.Value.Timestamp);
            
            return new Snapshot {
                Position = before.Value.Position.Lerp(after.Value.Position, (float)t),
                Rotation = LerpRotation(before.Value.Rotation, after.Value.Rotation, (float)t),
                Velocity = before.Value.Velocity.Lerp(after.Value.Velocity, (float)t),
                Timestamp = targetTime
            };
        }
        
        private Vector3 LerpRotation(Vector3 from, Vector3 to, float t) {
            // Handle rotation wrapping
            return new Vector3(
                Mathf.LerpAngle(from.X, to.X, t),
                Mathf.LerpAngle(from.Y, to.Y, t),
                Mathf.LerpAngle(from.Z, to.Z, t)
            );
        }
    }
}
```

---

## Document History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-02-01 | 1.0 | Initial specification complete | Session 3 |

---

**END OF DOCUMENT**

*This specification defines the hybrid entity-block system for Societies, combining the efficiency of voxel blocks for static world geometry with the flexibility of physics-based entities for interactive objects. All code examples are in C# for Godot 4.x.*
