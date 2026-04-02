# Terrain Modification System Specification

**Status**: SPECIFICATION COMPLETE  
**Session**: 1 - Technical Architecture  
**Document**: 15  
**Last Updated**: 2026-02-01  
**Dependencies**:
- `planning/sessions/session-3-core-gameplay-loops/01d-tool-system-spec.md`
- `planning/sessions/session-3-core-gameplay-loops/01c-movement-interaction-spec.md`
- `planning/sessions/session-1-technical-architecture/10-event-sourcing.md`
- `planning/meta/technical-constants.md`

---

## 1. Executive Summary

### 1.1 Terrain Modification Philosophy

Societies adopts a **voxel-based terrain system** with 1m³ blocks, emphasizing realistic logistics and meaningful player decisions. Unlike traditional voxel games where players carry infinite materials, Societies implements an **Eco-style weight and burden system** that transforms terrain modification from a trivial activity into a strategic logistical challenge.

**Core Design Pillars**:
1. **Realistic Logistics**: Weight matters - you cannot carry 1000 stone blocks
2. **Tool Specialization**: Different tools for different materials (pickaxes for stone, shovels for dirt)
3. **Physics Debris**: Breaking blocks creates real rubble that must be managed
4. **Strategic Planning**: Terrain modification requires preparation, tools, and transport
5. **Persistence**: All changes are permanent and synchronized across all players

### 1.2 Modification Capabilities

**Both Players and AI Agents can**:
- Mine/remove terrain blocks with appropriate tools
- Place blocks to build structures or terrain
- Use specialized terraforming tools (shovels, leveling tools)
- Transport materials (subject to weight limits)
- Create and manage debris/rubble

**Restrictions**:
- Bedrock layer (Y ≤ 0) is unmodifiable (world boundary)
- Claim-protected areas require permissions
- Weight limits constrain carrying capacity
- Tool tier limits material types that can be harvested

### 1.3 Weight/Burden System Overview

The weight system is the **defining feature** of Societies' terrain modification:

```
Player/Agent Weight Capacity:
- Base Carry: 50 kg (INVENTORY_WEIGHT_PLAYER_BASE_KG)
- Maximum: 100 kg (INVENTORY_WEIGHT_MAX_KG)
- Encumbrance Levels:
  * Light (0-25 kg): 100% speed
  * Medium (25-50 kg): 80% speed
  * Heavy (50-75 kg): 60% speed, no sprint
  * Over-encumbered (75-100 kg): 30% speed, no jump
```

**Strategic Implications**:
- Mining operations require planning (how to transport materials)
- Building projects need supply lines
- Vehicle/ cart systems become essential for large projects
- Cooperative play (shared carrying) is encouraged

---

## 2. Block Removal (Mining/Digging)

### 2.1 Mining Mechanics

**Basic Mining Process**:
```
1. Equip appropriate tool for target material
2. Position within tool range (2.0-3.0 meters)
3. Hold attack button to initiate mining animation
4. After animation completes, block is "loosened"
5. After sufficient hits, block breaks into debris
6. Debris chunks spawn with physics
7. Materials added to inventory (if space and weight allow)
```

**Mining Animation Cycle**:
```
Tool swing duration varies by tool tier:
- Stone tier: 100% speed (baseline)
- Iron tier: 150% speed (1.5× faster)
- Steel tier: 200% speed (2.0× faster)

Per-tool timings (from tool-system-spec.md):
- Pickaxe: 1.05s base cycle
- Shovel: 0.9s base cycle
- Axe: 0.9s base cycle
```

### 2.2 Tool-Block Interaction Matrix

| Block Type | Pickaxe | Shovel | Axe | Hoe | Bare Hands |
|------------|---------|--------|-----|-----|------------|
| **Stone** | ✓ Excellent | ✗ Cannot | ✗ Cannot | ✗ Cannot | ✗ Cannot |
| **Dirt** | △ Slow | ✓ Excellent | ✗ Cannot | ✓ Good | △ Very Slow |
| **Sand** | △ Slow | ✓ Excellent | ✗ Cannot | ✓ Good | △ Very Slow |
| **Wood** | ✗ Cannot | ✗ Cannot | ✓ Excellent | ✗ Cannot | △ Slow |
| **Ore** | ✓ Required | ✗ Cannot | ✗ Cannot | ✗ Cannot | ✗ Cannot |
| **Clay** | ✗ Cannot | ✓ Good | ✗ Cannot | △ Slow | ✗ Cannot |
| **Gravel** | △ Slow | ✓ Good | ✗ Cannot | ✗ Cannot | △ Slow |
| **Snow** | ✗ Cannot | ✓ Excellent | ✗ Cannot | ✗ Cannot | ✓ Good |

**Legend**:
- ✓ Excellent: Standard efficiency, no penalty
- ✓ Good: Slight penalty (-10% speed)
- △ Slow: Significant penalty (-50% speed, -25% yield)
- ✗ Cannot: Tool ineffective, cannot harvest

### 2.3 Mining Speed Calculation

```csharp
public class MiningCalculator {
    public float CalculateMiningSpeed(
        ToolInstance tool,
        BlockType targetBlock,
        float skillLevel,
        float staminaLevel) {
        
        // Base tool efficiency from tier
        float baseEfficiency = tool.Tier switch {
            ToolTier.Stone => TOOL_EFFICIENCY_STONE,    // 1.0f
            ToolTier.Iron => TOOL_EFFICIENCY_IRON,      // 1.5f
            ToolTier.Steel => TOOL_EFFICIENCY_STEEL,    // 2.0f
            _ => 1.0f
        };
        
        // Tool-block compatibility multiplier
        float compatibilityMultiplier = GetCompatibilityMultiplier(
            tool.ToolType, targetBlock);
        
        // Quality bonus (from tool-system-spec.md)
        float qualityMultiplier = GetQualityMultiplier(tool.Quality);
        // Poor: 0.90, Normal: 1.00, Good: 1.05, Excellent: 1.10, Masterwork: 1.15
        
        // Skill bonus (+5% per level)
        float skillMultiplier = 1.0f + (skillLevel * 0.05f);
        
        // Stamina penalty (below 30% stamina = -20% speed)
        float staminaMultiplier = staminaLevel < 30 ? 0.8f : 1.0f;
        
        // Calculate final speed multiplier
        float totalMultiplier = baseEfficiency 
            * compatibilityMultiplier 
            * qualityMultiplier 
            * skillMultiplier 
            * staminaMultiplier;
        
        return totalMultiplier;
    }
    
    private float GetCompatibilityMultiplier(ToolType tool, BlockType block) {
        return (tool, block) switch {
            // Perfect matches
            (ToolType.Pickaxe, BlockType.Stone) => 1.0f,
            (ToolType.Shovel, BlockType.Dirt) => 1.0f,
            (ToolType.Shovel, BlockType.Sand) => 1.0f,
            (ToolType.Axe, BlockType.Wood) => 1.0f,
            
            // Acceptable but slow
            (ToolType.Pickaxe, BlockType.Dirt) => 0.5f,
            (ToolType.Shovel, BlockType.Gravel) => 0.8f,
            (ToolType.Hoe, BlockType.Dirt) => 0.9f,
            
            // Incompatible
            _ => 0.0f
        };
    }
}
```

### 2.4 Tool Durability Consumption

**Standard Durability Cost**:
```
Per successful use: -1 durability

Material hardness multipliers:
- Soft (dirt, sand, snow): 0.5× durability cost (-0.5)
- Normal (stone, wood): 1.0× durability cost (-1)
- Hard (ore, metal): 1.5× durability cost (-1.5)
- Very Hard (bedrock - unmineable): N/A

Skill-based preservation (from tool-system-spec.md):
- Gathering Level 1: -10% durability loss (0.9 per use)
- Gathering Level 5: -30% durability loss (0.7 per use)
- Gathering Level 10: -50% durability loss (0.5 per use)

Critical failures (wrong tool type):
- Wrong material: -5 durability
- Tool breakage risk at 0 durability: 10% chance per use
```

**Durability States** (visual and functional):
```
Perfect (100%): Pristine appearance, full effectiveness
Good (70-99%): Minor wear, 100% effectiveness
Worn (30-69%): Visible wear, 95% effectiveness, yellow warning
Damaged (10-29%): Significant damage, 80% effectiveness, orange warning
Broken (0-9%): Severe damage, 50% effectiveness, unusable at 0%
```

### 2.5 Tool Types for Mining

**Pickaxe** (Primary Mining Tool):
```
Tool ID: tool_pickaxe_[tier]
Valid Targets:
  - Stone (all types)
  - Ore deposits (iron, copper, coal, gold, gems)
  - Stone structures and foundations
  - Bedrock (steel tier only, no yield)

Yields per block (stone):
  Stone tier: 1 stone block (100%)
  Iron tier: 1 stone block + 10% bonus debris
  Steel tier: 1 stone block + 25% bonus debris

Mining Tier Requirements:
  Stone pickaxe: Can mine stone, surface coal
  Iron pickaxe: Can mine iron ore, copper, deep coal
  Steel pickaxe: Can mine all ores including gold, silver, gems
```

**Shovel** (Digging Tool):
```
Tool ID: tool_shovel_[tier]
Valid Targets:
  - Dirt (all soil types)
  - Sand (beaches, deserts)
  - Gravel and loose stone
  - Snow

Digging Mechanics:
  - Removes top 1m of terrain per dig
  - Creates holes (up to unlimited depth)
  - Cannot dig through solid stone

Yields:
  Stone shovel: 1 dirt block (can place elsewhere)
  Iron shovel: 1 dirt block + 10% chance buried items
  Steel shovel: 1 dirt block + 20% chance buried items + 5% rare cache
```

---

## 3. Block Placement (Building)

### 3.1 Placement Mechanics

**Block Placement Process**:
```
1. Select block from inventory (or have materials available)
2. Enter build mode (B key or right-click with building tool)
3. Position ghost block using mouse/aim
4. Validate placement (collision checks, support checks)
5. Click to place (initiates placement animation)
6. Materials consumed from inventory
7. Block appears in world
```

**Placement Animation**:
```
Duration: 1.0-3.0 seconds depending on block weight
- Light blocks (wood, dirt): 1.0s
- Medium blocks (stone): 2.0s
- Heavy blocks (metal, reinforced): 3.0s

Animation steps:
1. Reach (0.3s): Character reaches toward placement point
2. Place (0.4s): Block positioning motion
3. Confirm (0.3s): Final adjustment and release

Hammer tool accelerates placement by 50%
```

### 3.2 Placement Validation

**Collision Checks**:
```csharp
public class PlacementValidator {
    public PlacementResult ValidatePlacement(
        Vector3 position,
        BlockType blockType,
        Player player) {
        
        var result = new PlacementResult();
        
        // Check 1: Collision with existing entities
        if (Physics.CheckBox(position, Vector3.One * 0.5f, 
            out var colliders)) {
            result.AddFailure("Collision detected with existing object");
        }
        
        // Check 2: Ground support
        var supportBlock = position - Vector3.Up;
        if (!IsSolidBlock(supportBlock)) {
            result.AddFailure("No ground support - blocks cannot float");
        }
        
        // Check 3: Line of sight
        if (!HasLineOfSight(player.Position, position)) {
            result.AddFailure("No line of sight to placement location");
        }
        
        // Check 4: Range
        if (Vector3.Distance(player.Position, position) > 3.0f) {
            result.AddFailure("Target too far - move closer");
        }
        
        // Check 5: Claim permission
        if (!HasBuildPermission(player, position)) {
            result.AddFailure("No building permission in this area");
        }
        
        // Check 6: Weight capacity
        var blockWeight = GetBlockWeight(blockType);
        if (player.CurrentWeight + blockWeight > player.MaxWeight) {
            result.AddFailure("Too heavy - cannot carry block to place");
        }
        
        return result;
    }
}
```

**Placement Constraints**:
```
Maximum placement slope: 45 degrees
Minimum clearance: 0.5m from other objects
Maximum reach: 3.0 meters
Must have line of sight (unless admin mode)
Must have building permission in jurisdiction

Structural Requirements:
- Blocks must have support from below OR
- Must attach to at least 2 adjacent blocks (for walls)
- Cannot place in mid-air (anti-griefing)
```

### 3.3 Building vs Modifying Terrain

**Key Distinction**:
```
Building (Structures):
- Uses separate building system
- Blocks can be removed and replaced
- Subject to structural integrity rules
- Can be demolished for 50-75% material return
- Examples: Houses, walls, bridges

Terrain Modification:
- Direct voxel world changes
- Permanent (no undo without admin)
- Affects terrain generation algorithms
- Examples: Flattening hills, digging trenches, raising ground
```

**Terrain Modification Types**:
```
1. Block Removal (Mining):
   - Pickaxe: Removes stone blocks
   - Shovel: Removes dirt/sand blocks
   - Result: Air block replaces solid

2. Block Addition (Filling):
   - Place dirt/stone to raise terrain
   - Must have materials in inventory
   - Subject to weight limits

3. Block Replacement:
   - Replace dirt with stone (road building)
   - Replace stone with wood (flooring)
   - Requires both removal and placement
```

---

## 4. Physics Debris System

### 4.1 Rubble Generation

**When Blocks Break**:
```
Block break event triggers debris spawn:

Small blocks (dirt, sand, snow):
  - Spawn 2-4 debris chunks
  - Each chunk: 0.25-0.5m³
  - Total volume: 75% of original block

Medium blocks (stone, wood):
  - Spawn 3-6 debris chunks
  - Each chunk: 0.2-0.4m³
  - Total volume: 60% of original block

Hard blocks (ore, metal):
  - Spawn 4-8 debris chunks
  - Each chunk: 0.15-0.3m³
  - Total volume: 50% of original block

Bonus: 10% chance for intact "rubble block" (collectable)
```

**Debris Chunk Properties**:
```csharp
public class DebrisChunk {
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Mass { get; set; } // 5-20 kg
    public float Size { get; set; } // 0.15-0.5m
    public MaterialType Material { get; set; }
    public float Lifetime { get; set; } // Seconds until cleanup
    public bool IsCollectable { get; set; }
    
    // Physics properties
    public float Restitution { get; set; } = 0.3f; // Bounciness
    public float Friction { get; set; } = 0.6f;
    public float Drag { get; set; } = 0.1f;
}
```

### 4.2 Physics-Based Debris

**Godot 4.x Physics Integration**:
```csharp
public class DebrisSystem : Node3D {
    [Export] public int MaxDebrisCount = 500;
    [Export] public float DebrisLifetime = 300.0f; // 5 minutes
    
    private List<RigidBody3D> _activeDebris = new();
    private Queue<RigidBody3D> _debrisPool = new();
    
    public void SpawnDebris(Vector3 position, BlockType blockType, int count) {
        // Check performance limits
        if (_activeDebris.Count >= MaxDebrisCount) {
            MergeOldestDebris();
        }
        
        for (int i = 0; i < count; i++) {
            var debris = GetDebrisFromPool();
            
            // Random spread
            var offset = new Vector3(
                GD.Randf() - 0.5f,
                GD.Randf() * 0.5f,
                GD.Randf() - 0.5f
            ) * 0.5f;
            
            debris.GlobalPosition = position + offset;
            
            // Initial velocity (explosive scatter)
            debris.LinearVelocity = new Vector3(
                (GD.Randf() - 0.5f) * 3.0f,
                GD.Randf() * 2.0f + 1.0f,
                (GD.Randf() - 0.5f) * 3.0f
            );
            
            // Set material properties
            SetupDebrisMaterial(debris, blockType);
            
            _activeDebris.Add(debris);
            AddChild(debris);
        }
    }
    
    private void SetupDebrisMaterial(RigidBody3D debris, BlockType type) {
        var collider = debris.GetChild<CollisionShape3D>(0);
        
        switch (type) {
            case BlockType.Stone:
                debris.Mass = 10.0f;
                debris.PhysicsMaterialOverride = new PhysicsMaterial {
                    Friction = 0.7f,
                    Bounce = 0.2f
                };
                break;
            case BlockType.Dirt:
                debris.Mass = 5.0f;
                debris.PhysicsMaterialOverride = new PhysicsMaterial {
                    Friction = 0.9f,
                    Bounce = 0.1f
                };
                break;
            case BlockType.Wood:
                debris.Mass = 3.0f;
                debris.PhysicsMaterialOverride = new PhysicsMaterial {
                    Friction = 0.5f,
                    Bounce = 0.3f
                };
                break;
        }
    }
}
```

### 4.3 Debris Cleanup and Merging

**Automatic Cleanup**:
```
Debris lifecycle:
1. Spawn: Physics-active for 30 seconds
2. Settle: After 30s of low velocity, becomes static
3. Decay: Timer starts (5 minutes default)
4. Cleanup: After timer expires:
   - If collectable: Convert to item drop
   - If not: Despawn with particle effect

Manual cleanup options:
- Shovel tool: Can clear debris piles (right-click)
- Time acceleration: Debris despawns faster when no players nearby
- Merging: Nearby debris auto-combines into larger piles
```

**Debris Merging Algorithm**:
```csharp
public void MergeNearbyDebris() {
    var mergeDistance = 1.5f;
    var maxPileSize = 10; // Max chunks per pile
    
    for (int i = 0; i < _activeDebris.Count; i++) {
        var debrisA = _activeDebris[i];
        if (!debrisA.IsStatic) continue;
        
        for (int j = i + 1; j < _activeDebris.Count; j++) {
            var debrisB = _activeDebris[j];
            if (!debrisB.IsStatic) continue;
            
            // Check material compatibility
            if (debrisA.Material != debrisB.Material) continue;
            
            // Check distance
            if (debrisA.GlobalPosition.DistanceTo(debrisB.GlobalPosition) > mergeDistance)
                continue;
            
            // Merge B into A
            debrisA.Mass += debrisB.Mass;
            debrisA.Size = Mathf.Min(debrisA.Size + debrisB.Size, 1.0f);
            
            // Remove B
            ReturnDebrisToPool(debrisB);
            _activeDebris.RemoveAt(j);
            j--;
            
            // Check pile limit
            if (debrisA.Mass > maxPileSize * 10.0f) break;
        }
    }
}
```

### 4.4 Performance Limits

**Debris Limits by Category**:
```
Client-Side (per player view):
- Maximum visible debris: 200 chunks
- LOD system: Simplified physics beyond 50m
- Culling: Debris hidden behind terrain not rendered

Server-Side (per chunk):
- Maximum persistent debris: 50 piles
- Auto-merge when count exceeds 30
- Priority: Player-created > natural > ambient

Global Limits:
- World max debris: 5000 chunks
- Oldest debris auto-cleaned when limit reached
- Critical events (combat, disasters) can temporarily exceed limits
```

**Optimization Strategies**:
```csharp
public class DebrisOptimizer {
    // Distance-based LOD
    public void UpdateDebrisLOD(Vector3 playerPosition) {
        foreach (var debris in _activeDebris) {
            var distance = debris.GlobalPosition.DistanceTo(playerPosition);
            
            if (distance < 20.0f) {
                // Full physics detail
                debris.PhysicsInterpolationMode = PhysicsInterpolationMode.Fixed;
                debris.Sleeping = false;
            } else if (distance < 50.0f) {
                // Reduced physics
                debris.PhysicsInterpolationMode = PhysicsInterpolationMode.Idle;
            } else {
                // Sleep physics, visual only
                debris.Sleeping = true;
            }
        }
    }
    
    // Spatial partitioning for collision
    public void OrganizeDebrisIntoChunks() {
        var chunkSize = 16.0f;
        var chunks = new Dictionary<Vector2I, List<RigidBody3D>>();
        
        foreach (var debris in _activeDebris) {
            var chunkX = Mathf.FloorToInt(debris.GlobalPosition.X / chunkSize);
            var chunkZ = Mathf.FloorToInt(debris.GlobalPosition.Z / chunkSize);
            var chunkCoord = new Vector2I(chunkX, chunkZ);
            
            if (!chunks.ContainsKey(chunkCoord)) {
                chunks[chunkCoord] = new List<RigidBody3D>();
            }
            chunks[chunkCoord].Add(debris);
        }
    }
}
```

---

## 5. Weight and Carrying System

### 5.1 Weight System Overview

**Design Philosophy**: Realistic logistics where material transport is a meaningful challenge.

**Weight Capacity Tiers**:
```
Character Capacity (Player/Agent):
- Base Carry: 50 kg (INVENTORY_WEIGHT_PLAYER_BASE_KG)
- Maximum: 100 kg (INVENTORY_WEIGHT_MAX_KG)
- Empty inventory weight: 0 kg

Encumbrance Effects (from movement-interaction-spec.md):
- Light Load (0-25 kg): 100% speed (3.0 m/s walk, 6.0 m/s sprint)
- Medium Load (25-50 kg): 80% speed (2.4 m/s walk, 4.8 m/s sprint)
- Heavy Load (50-75 kg): 60% speed (1.8 m/s walk), no sprint
- Over-encumbered (75-100 kg): 30% speed (0.9 m/s), no sprint, no jump
```

### 5.2 Block Weights Catalog

**Material Weight Table**:
```
Block Type          | Weight (kg/m³) | Carry Limit (blocks)
--------------------|----------------|----------------------
Air                 | 0              | N/A
Dirt                | 15             | 3-6 (depending on load)
Sand                | 16             | 3-6
Clay                | 18             | 2-5
Gravel              | 17             | 2-5
Stone (generic)     | 25             | 2-4
Granite             | 27             | 1-3
Limestone           | 24             | 2-4
Sandstone           | 22             | 2-4
Coal                | 13             | 3-7
Iron Ore            | 35             | 1-2
Copper Ore          | 32             | 1-3
Gold Ore            | 40             | 1-2
Wood (generic)      | 8              | 6-12
Oak                 | 9              | 5-11
Pine                | 7              | 7-14
Birch               | 8              | 6-12
Hardwood            | 12             | 4-8
Planks              | 6              | 8-16
Snow                | 5              | 10-20
Ice                 | 9              | 5-11
Glass               | 14             | 3-7
Metal (generic)     | 45             | 1-2
Iron Ingot          | 50             | 1-2
Steel Ingot         | 48             | 1-2
Gold Ingot          | 60             | 1
```

**Weight Calculation Formula**:
```csharp
public class WeightCalculator {
    // Block weights in kg per cubic meter
    public static readonly Dictionary<BlockType, float> BlockWeights = new() {
        { BlockType.Dirt, 15.0f },
        { BlockType.Sand, 16.0f },
        { BlockType.Stone, 25.0f },
        { BlockType.Granite, 27.0f },
        { BlockType.Coal, 13.0f },
        { BlockType.IronOre, 35.0f },
        { BlockType.Wood, 8.0f },
        { BlockType.Planks, 6.0f },
        { BlockType.Snow, 5.0f },
        { BlockType.Metal, 45.0f },
    };
    
    public float CalculateBlockWeight(BlockType type, float volume = 1.0f) {
        if (BlockWeights.TryGetValue(type, out var density)) {
            return density * volume;
        }
        return 20.0f * volume; // Default assumption
    }
    
    public float CalculateInventoryWeight(Inventory inventory) {
        float totalWeight = 0;
        
        foreach (var slot in inventory.Slots) {
            if (slot.Item != null) {
                var itemWeight = slot.Item.Weight * slot.Quantity;
                totalWeight += itemWeight;
            }
        }
        
        return totalWeight;
    }
}
```

### 5.3 Encumbrance Effects

**Movement Speed Calculation**:
```csharp
public class EncumbranceSystem {
    public MovementStats CalculateMovementSpeed(float currentWeight, float maxWeight) {
        var ratio = currentWeight / maxWeight;
        
        // Base speeds from technical-constants.md
        float baseWalk = 3.0f; // MOVEMENT_SPEED_WALK
        float baseSprint = 6.0f; // MOVEMENT_SPEED_SPRINT
        
        float walkSpeed, sprintSpeed;
        bool canSprint, canJump;
        
        if (ratio <= 0.25f) {
            // Light load
            walkSpeed = baseWalk * 1.0f;
            sprintSpeed = baseSprint * 1.0f;
            canSprint = true;
            canJump = true;
        } else if (ratio <= 0.50f) {
            // Medium load
            walkSpeed = baseWalk * 0.8f;
            sprintSpeed = baseSprint * 0.8f;
            canSprint = true;
            canJump = true;
        } else if (ratio <= 0.75f) {
            // Heavy load
            walkSpeed = baseWalk * 0.6f;
            sprintSpeed = 0; // Sprint disabled
            canSprint = false;
            canJump = true;
        } else {
            // Over-encumbered
            walkSpeed = baseWalk * 0.3f;
            sprintSpeed = 0;
            canSprint = false;
            canJump = false;
        }
        
        return new MovementStats {
            WalkSpeed = walkSpeed,
            SprintSpeed = sprintSpeed,
            CanSprint = canSprint,
            CanJump = canJump,
            EncumbranceLevel = GetEncumbranceLevel(ratio)
        };
    }
}
```

**Visual Feedback**:
```
UI Indicators:
- Weight bar: Shows current/max (0-100 kg)
- Color coding: Green (0-50%), Yellow (50-75%), Red (75-100%)
- Warning at 75%: "Heavy load - movement slowed"
- Warning at 90%: "Critical weight - cannot move freely"

Character Animation:
- Light: Normal walking animation
- Medium: Slightly hunched posture
- Heavy: Bent forward, slower gait
- Over-encumbered: Dragging feet, heavy breathing

Audio Feedback:
- Footstep volume increases with weight
- Heavy breathing begins at 60% capacity
- Straining sounds at 80%+ capacity
```

### 5.4 Carrying Capacity Calculation

**Base Capacity**:
```
Player Base: 50 kg
Agent Base: 40 kg (AI agents carry slightly less)

Capacity Modifiers:
- Strength skill level: +2 kg per level (max +20 kg at level 10)
- Backpack item: +15 kg capacity
- Belt item: +5 kg capacity
- Pockets item: +3 kg capacity

Maximum Theoretical:
- Base: 50 kg
- Strength 10: +20 kg
- Backpack: +15 kg
- Belt: +5 kg
- Pockets: +3 kg
- Total: 93 kg (soft cap at 100 kg)
```

**Capacity Calculation**:
```csharp
public class CapacityCalculator {
    public float CalculateMaxCapacity(Entity entity) {
        float baseCapacity = entity.IsPlayer ? 50.0f : 40.0f;
        
        // Strength bonus
        float strengthLevel = entity.Skills.GetLevel(SkillType.Strength);
        float strengthBonus = strengthLevel * 2.0f;
        
        // Equipment bonuses
        float equipmentBonus = 0;
        if (entity.Equipment.HasItem("backpack"))
            equipmentBonus += 15.0f;
        if (entity.Equipment.HasItem("tool_belt"))
            equipmentBonus += 5.0f;
        if (entity.Equipment.HasItem("pockets"))
            equipmentBonus += 3.0f;
        
        // Specialization bonus
        float specBonus = 0;
        if (entity.HasSpecialization(SpecializationType.Porter))
            specBonus = 10.0f;
        
        float totalCapacity = baseCapacity + strengthBonus + 
                              equipmentBonus + specBonus;
        
        // Hard cap
        return Mathf.Min(totalCapacity, 100.0f);
    }
}
```

### 5.5 Vehicle/Transport Requirements

**Transport Tiers**:
```
Tier 1 - Manual Transport:
- Wheelbarrow: Capacity 100 kg, player pushes
- Hand cart: Capacity 150 kg, requires roads
- Sled: Capacity 80 kg, works on snow/ice
- Usage: Early game, small projects

Tier 2 - Animal Transport:
- Pack animal (donkey): Capacity 200 kg
- Cart (horse-drawn): Capacity 500 kg
- Usage: Mid-game, medium projects

Tier 3 - Mechanical Transport:
- Motor cart: Capacity 1000 kg, requires fuel
- Conveyor belts: Unlimited capacity, fixed routes
- Usage: Late-game, industrial projects

Tier 4 - Advanced Transport:
- Pneumatic tubes: Instant transport, high power cost
- Rail systems: 5000+ kg capacity
- Usage: Endgame, mega projects
```

**Vehicle Capacity System**:
```csharp
public class TransportVehicle {
    public string VehicleId { get; set; }
    public VehicleType Type { get; set; }
    public float MaxCapacity { get; set; } // kg
    public float CurrentLoad { get; private set; }
    public List<Inventory> StorageSlots { get; set; }
    
    public bool CanAddLoad(float weight) {
        return (CurrentLoad + weight) <= MaxCapacity;
    }
    
    public bool AddCargo(ItemStack stack) {
        var weight = stack.TotalWeight;
        
        if (!CanAddLoad(weight)) {
            return false;
        }
        
        // Find appropriate slot
        foreach (var slot in StorageSlots) {
            if (slot.CanAdd(stack)) {
                slot.Add(stack);
                CurrentLoad += weight;
                return true;
            }
        }
        
        return false;
    }
    
    public void RemoveCargo(ItemStack stack) {
        foreach (var slot in StorageSlots) {
            if (slot.Contains(stack)) {
                slot.Remove(stack);
                CurrentLoad -= stack.TotalWeight;
                return;
            }
        }
    }
}

public enum VehicleType {
    Wheelbarrow,    // 100 kg
    HandCart,       // 150 kg
    Sled,           // 80 kg
    PackAnimal,     // 200 kg
    HorseCart,      // 500 kg
    MotorCart,      // 1000 kg
    Conveyor,       // Unlimited (but slow)
    RailCart        // 5000 kg
}
```

### 5.6 Weight-Based Inventory Management

**Slot and Weight Hybrid System**:
```
Societies uses a hybrid inventory system:
- Slots: Organize items (64 slots per player)
- Weight: Limits total carrying capacity

Both must be satisfied:
- Item requires 1 slot AND adds weight
- Can have empty slots but be at weight limit
- Can be at slot limit but under weight

Block Stacking:
- Dirt: Stack up to 20 (300 kg at max - 6× base capacity)
- Stone: Stack up to 10 (250 kg)
- Wood: Stack up to 30 (240 kg)
- Ore: Stack up to 5 (175 kg)

Weight-based slot limits:
- When placing heavy blocks, may limit stack size
- Example: Stone limited to 2 slots per inventory
- Prevents one material type from dominating inventory
```

**Inventory Weight UI**:
```
Inventory Screen Layout:
┌─────────────────────────────────────┐
│ Inventory           Weight: 45/50 kg│
│ [██████████████████░░] 90%         │
├─────────────────────────────────────┤
│ [Dirt ×20] 300 kg    [Wood ×10]     │
│ [Stone ×2] 50 kg     [Empty]        │
│ [Pickaxe] 3 kg       [Food ×5]      │
└─────────────────────────────────────┘

Color coding:
- Green (< 60%): Normal operation
- Yellow (60-80%): Encumbered warning
- Orange (80-95%): Heavy load warning
- Red (95-100%): Critical - cannot add items

Tooltip on weight bar:
"45.2 kg / 50 kg base (+15 kg backpack bonus)
Strength bonus: +10 kg
Equipment bonus: +18 kg"
```

---

## 6. Material Properties

### 6.1 Block Weight Reference Table

**Complete Weight Catalog**:
```
Natural Materials:
┌─────────────────┬────────────┬─────────────┬──────────────────┐
│ Block Type      │ Weight(kg) │ Stack Size  │ Max Carry(blocks)│
├─────────────────┼────────────┼─────────────┼──────────────────┤
│ Dirt            │ 15         │ 20          │ 3-6              │
│ Sand            │ 16         │ 20          │ 3-6              │
│ Clay            │ 18         │ 15          │ 2-5              │
│ Gravel          │ 17         │ 15          │ 2-5              │
│ Mud             │ 19         │ 15          │ 2-5              │
│ Peat            │ 12         │ 25          │ 4-8              │
│ Silt            │ 16         │ 20          │ 3-6              │
│ Topsoil         │ 14         │ 25          │ 3-7              │
└─────────────────┴────────────┴─────────────┴──────────────────┘

Stone Materials:
┌─────────────────┬────────────┬─────────────┬──────────────────┐
│ Block Type      │ Weight(kg) │ Stack Size  │ Max Carry(blocks)│
├─────────────────┼────────────┼─────────────┼──────────────────┤
│ Stone (generic) │ 25         │ 10          │ 2-4              │
│ Granite         │ 27         │ 8           │ 1-3              │
│ Basalt          │ 29         │ 8           │ 1-3              │
│ Limestone       │ 24         │ 10          │ 2-4              │
│ Sandstone       │ 22         │ 12          │ 2-4              │
│ Slate           │ 26         │ 10          │ 1-3              │
│ Marble          │ 28         │ 8           │ 1-3              │
│ Obsidian        │ 30         │ 5           │ 1-3              │
│ Bedrock         │ N/A        │ N/A         │ Unmineable       │
└─────────────────┴────────────┴─────────────┴──────────────────┘

Ore Materials:
┌─────────────────┬────────────┬─────────────┬──────────────────┐
│ Block Type      │ Weight(kg) │ Stack Size  │ Max Carry(blocks)│
├─────────────────┼────────────┼─────────────┼──────────────────┤
│ Coal            │ 13         │ 20          │ 3-7              │
│ Iron Ore        │ 35         │ 5           │ 1-2              │
│ Copper Ore      │ 32         │ 5           │ 1-3              │
│ Tin Ore         │ 28         │ 8           │ 1-3              │
│ Lead Ore        │ 38         │ 5           │ 1-2              │
│ Silver Ore      │ 36         │ 5           │ 1-2              │
│ Gold Ore        │ 40         │ 5           │ 1-2              │
│ Gem Ore         │ 33         │ 5           │ 1-3              │
│ Uranium Ore     │ 45         │ 3           │ 1                │
└─────────────────┴────────────┴─────────────┴──────────────────┘

Wood Materials:
┌─────────────────┬────────────┬─────────────┬──────────────────┐
│ Block Type      │ Weight(kg) │ Stack Size  │ Max Carry(blocks)│
├─────────────────┼────────────┼─────────────┼──────────────────┤
│ Oak Wood        │ 9          │ 30          │ 5-11             │
│ Pine Wood       │ 7          │ 35          │ 7-14             │
│ Birch Wood      │ 8          │ 30          │ 6-12             │
│ Spruce Wood     │ 7          │ 35          │ 7-14             │
│ Maple Wood      │ 9          │ 30          │ 5-11             │
│ Walnut Wood     │ 10         │ 25          │ 5-10             │
│ Ebony Wood      │ 12         │ 20          │ 4-8              │
│ Bamboo          │ 4          │ 50          │ 12-25            │
│ Planks (any)    │ 6          │ 40          │ 8-16             │
│ Wood Chips      │ 3          │ 100         │ 16-33            │
└─────────────────┴────────────┴─────────────┴──────────────────┘

Other Materials:
┌─────────────────┬────────────┬─────────────┬──────────────────┐
│ Block Type      │ Weight(kg) │ Stack Size  │ Max Carry(blocks)│
├─────────────────┼────────────┼─────────────┼──────────────────┤
│ Snow            │ 5          │ 50          │ 10-20            │
│ Ice             │ 9          │ 30          │ 5-11             │
│ Packed Ice      │ 11         │ 25          │ 4-9              │
│ Glass           │ 14         │ 20          │ 3-7              │
│ Brick           │ 20         │ 15          │ 2-5              │
│ Concrete        │ 23         │ 12          │ 2-4              │
│ Metal (generic) │ 45         │ 5           │ 1-2              │
│ Iron Ingot      │ 50         │ 3           │ 1                │
│ Steel Ingot     │ 48         │ 3           │ 1                │
│ Gold Ingot      │ 60         │ 2           │ 1                │
│ Copper Ingot    │ 44         │ 4           │ 1-2              │
└─────────────────┴────────────┴─────────────┴──────────────────┘
```

### 6.2 Durability/Hardness Table

**Mining Difficulty**:
```
Block Hardness determines mining time and tool durability cost:

Hardness Levels:
- Very Soft (0.5×): Dirt, sand, snow, peat
- Soft (0.75×): Clay, gravel, wood, mud
- Normal (1.0×): Stone, coal, brick
- Hard (1.5×): Granite, ore deposits, concrete
- Very Hard (2.0×): Obsidian, bedrock (unmineable)

Mining Time Formula:
BaseTime = 1.0 second
AdjustedTime = BaseTime × HardnessMultiplier / MiningSpeed

Example - Iron Pickaxe on Granite:
- Base time: 1.0s
- Hardness: 1.5× (Granite)
- Mining speed: 1.5× (Iron tier)
- Result: 1.0 × 1.5 / 1.5 = 1.0 second per hit

Example - Stone Pickaxe on Granite:
- Base time: 1.0s
- Hardness: 1.5×
- Mining speed: 1.0× (Stone tier)
- Result: 1.0 × 1.5 / 1.0 = 1.5 seconds per hit
```

**Durability Cost by Material**:
```
Material        │ Durability Cost │ Hits to Break (Stone Tool)
────────────────┼─────────────────┼────────────────────────────
Dirt            │ 0.5             │ 100
Sand            │ 0.5             │ 100
Snow            │ 0.3             │ 166
Wood            │ 0.75            │ 66
Stone           │ 1.0             │ 50
Granite         │ 1.25            │ 40
Coal            │ 0.8             │ 62
Iron Ore        │ 1.5             │ 33
Copper Ore      │ 1.4             │ 35
Gold Ore        │ 1.6             │ 31
Obsidian        │ 2.0             │ 25 (steel only)
```

### 6.3 Tool Effectiveness Matrix

**Tool vs Material Effectiveness**:
```
Tool Type   │ Soft │ Normal │ Hard │ Notes
────────────┼──────┼────────┼──────┼────────────────────
Pickaxe     │ 50%  │ 100%   │ 100% │ Primary mining tool
Shovel      │ 100% │ 80%    │ 0%   │ Digging only
Axe         │ 0%   │ 0%     │ 0%   │ Wood only
Hoe         │ 90%  │ 10%    │ 0%   │ Farming focus
Sickle      │ 0%   │ 0%     │ 0%   │ Plants only
Hammer      │ 10%  │ 20%    │ 0%   │ Building only
Bare Hands  │ 20%  │ 0%     │ 0%   │ Very slow, limited

Effectiveness = Yield multiplier and Speed multiplier
- 100% = Full yield, full speed
- 50% = Half yield, half speed (or cannot harvest)
- 0% = Cannot harvest material
```

**Yield Modifiers**:
```
Tool Tier Bonus (yield increase):
- Stone: Base yield (100%)
- Iron: +15% yield
- Steel: +25% yield

Quality Bonus:
- Poor: -15% yield
- Normal: 0%
- Good: +5% yield
- Excellent: +15% yield
- Masterwork: +25% yield

Skill Bonus:
- +2% yield per gathering level (max +20% at level 10)

Critical Hits (5-10% chance):
- Double yield on that hit
```

### 6.4 Material Categories

**Category Organization**:
```
Category 1: Earth Materials
- Dirt, Sand, Clay, Gravel, Silt, Mud, Peat, Topsoil
- Tools: Shovel (primary), Hoe (secondary)
- Weight range: 12-19 kg/m³

Category 2: Stone Materials
- Stone, Granite, Basalt, Limestone, Sandstone, Slate, Marble
- Tools: Pickaxe (required)
- Weight range: 22-30 kg/m³

Category 3: Ore Materials
- Coal, Iron Ore, Copper Ore, Precious Metals
- Tools: Pickaxe (tier requirements apply)
- Weight range: 13-45 kg/m³

Category 4: Wood Materials
- All wood types, planks, processed wood
- Tools: Axe (primary), Saw (processing)
- Weight range: 3-12 kg/m³

Category 5: Ice/Snow Materials
- Snow, Ice, Packed Ice
- Tools: Shovel (primary), Pickaxe (secondary)
- Weight range: 5-11 kg/m³

Category 6: Manufactured Materials
- Brick, Concrete, Glass, Metal
- Tools: Pickaxe (usually)
- Weight range: 14-60 kg/m³
```

---

## 7. Terraforming Tools

### 7.1 Shovel Mechanics

**Detailed Shovel Operation** (from tool-system-spec.md):
```
Tool ID: tool_shovel_[tier]

Digging Mechanics:
  - Removes top 1m of terrain (full block)
  - Can create holes (unlimited depth)
  - Hole creation: Each dig goes down 1m

Valid Targets:
  Stone tier: Dirt, sand, loose gravel, snow
  Iron tier: + Packed earth, clay
  Steel tier: + All soil types, shallow gravel

Yields by Tier:
  Stone shovel:
    - 1 terrain block per dig
    - 5% chance buried cache
    - 20% chance to break on rocky soil
    
  Iron shovel:
    - 1 terrain block per dig
    - 10% chance buried cache
    - Can dig all soil types
    
  Steel shovel:
    - 1 terrain block per dig
    - 20% chance buried cache
    - 5% chance rare buried cache
    - Can dig through gravel efficiently

Animation Sequence:
  1. Stab: 0.3s (drive shovel into ground)
  2. Lever: 0.3s (push handle down, lift material)
  3. Toss: 0.3s (throw material aside)
  4. Total cycle: 0.9s base

Durability: 50/150/500 uses (Stone/Iron/Steel)
```

**Shovel Special Abilities**:
```
Right-Click Functions:
- Clear debris: Remove debris piles in 2m radius
- Fill hole: Place held material into hole
- Flatten: Level terrain in 1m radius (3 uses)

Excavator Specialization (Shovel skill level 5+):
- Digging speed: +25%
- Buried item detection: +50% chance
- Sense buried objects within 5m
- Can create 2×2 holes in single dig
```

### 7.2 Leveling Tools

**Leveling Tool (Post-MVP Feature)**:
```
Tool ID: tool_leveler_[tier]
Purpose: Efficient terrain smoothing and grading

Mechanics:
  - Select target elevation
  - Tool automatically calculates cuts/fills
  - Shows material surplus/deficit
  - Can operate in 3 modes:

Modes:
  1. Cut Mode: Remove material above target level
     - Materials collected to inventory
     - Cannot exceed weight limit
     - Produces debris

  2. Fill Mode: Add material below target level
     - Consumes material from inventory
     - Requires sufficient materials

  3. Smooth Mode: Average terrain in radius
     - Balances highs and lows
     - Surplus materials collected
     - Deficit areas flagged

Range:
  Stone: 3m radius
  Iron: 5m radius
  Steel: 8m radius

Efficiency:
  - Processes 1 block per second (base)
  - Affected by tool tier
  - Skill bonus applies
```

**Bulldozer/Earthmover (Late-Game)**:
```
Vehicle-based terraforming:
- Capacity: 5000 kg material storage
- Speed: 10× faster than hand tools
- Fuel requirement: Diesel/oil
- Clearance: Can push debris and vegetation

Usage:
1. Enter vehicle
2. Select mode (push/scoop/grade)
3. Drive to target area
4. Materials automatically collected in hopper
5. Dump at designated location
```

### 7.3 Fill/Dig Tools

**Trenching Tool**:
```
Tool ID: tool_trencher
Purpose: Create linear excavations (trenches, foundations)

Mechanics:
  - Define trench path (click start, click end)
  - Shows preview of excavation
  - Calculates material requirements/surplus
  - Can create stepped trenches

Parameters:
  - Width: 1-3 meters
  - Depth: 1-5 meters
  - Length: Unlimited (within stamina/tools)

Execution:
  - Character follows trench path
  - Automatic digging at set intervals
  - Materials collected or placed beside trench
  - Progress bar shows completion

Requirements:
  - Shovel tool equipped
  - Sufficient durability
  - Stamina for duration
  - Inventory space or nearby container
```

**Fill Tool**:
```
Tool ID: tool_filler
Purpose: Rapid area filling and raising

Mechanics:
  - Select target area
  - Tool calculates fill volume
  - Shows material requirement
  - Can use multiple material types

Fill Types:
  - Uniform: Single material throughout
  - Layered: Different materials by depth
  - Smart: Uses available materials efficiently

Execution:
  - Place materials from inventory
  - Progress is gradual (not instant)
  - Can pause and resume
  - Materials can be added mid-operation
```

### 7.4 Road Building

**Road Construction System**:
```
Road Types by Material:

1. Dirt Road:
   - Materials: Dirt or gravel
   - Speed bonus: +10% movement
   - Durability: Low (requires maintenance)
   - Construction time: Fast

2. Gravel Road:
   - Materials: Gravel
   - Speed bonus: +20% movement
   - Durability: Medium
   - Weather resistance: Good

3. Cobblestone Road:
   - Materials: Stone
   - Speed bonus: +25% movement
   - Durability: High
   - Construction time: Slow

4. Paved Road:
   - Materials: Brick or concrete
   - Speed bonus: +30% movement
   - Durability: Very high
   - Requires: Advanced materials

Construction Process:
1. Enter road building mode
2. Define road path (waypoints)
3. Select road type
4. System calculates materials needed
5. Gather materials (or use from inventory)
6. Build road (progressive construction)
7. Road appears and provides bonuses

Road Width Options:
- Narrow: 2m (foot traffic)
- Standard: 4m (carts, animals)
- Wide: 6m (vehicles, high traffic)

Maintenance:
- Roads degrade over time
- Weather accelerates degradation
- Repair requires 25% of original materials
- Neglected roads revert to terrain
```

**Road Tool Mechanics**:
```
Tool ID: tool_road_builder
Usage: Specialized road construction interface

Features:
  - Path planning with visual preview
  - Elevation smoothing (bridges/embankments)
  - Material estimation
  - Build queue (segment by segment)
  - Team building (multiple workers)

Integration:
  - Uses hammer tool for placement
  - Uses shovel for grading
  - Uses materials from inventory/containers
  - Requires building skill
```

---

## 8. Persistence and Sync

### 8.1 Block Change Events

**Event Sourcing for Terrain** (from 10-event-sourcing.md):
```csharp
// Terrain modification event types
public enum TerrainEventType : short {
    BlockRemoved = 1000,
    BlockPlaced = 1001,
    BlockReplaced = 1002,
    TerrainModified = 1003,
    DebrisSpawned = 1004,
    DebrisCollected = 1005,
    DebrisMerged = 1006,
    DebrisCleaned = 1007
}

// Block removed (mining/digging)
public class BlockRemovedPayload {
    public Vector3I Position { get; set; }  // Block coordinates
    public BlockType BlockType { get; set; }
    public float DurabilityConsumed { get; set; }
    public string ToolUsed { get; set; }
    public Guid ActorId { get; set; }
    public ActorType ActorType { get; set; }
    public float TimeSpent { get; set; }
    public List<ItemYield> Yields { get; set; }
    public List<DebrisInfo> DebrisSpawned { get; set; }
}

// Block placed (building)
public class BlockPlacedPayload {
    public Vector3I Position { get; set; }
    public BlockType BlockType { get; set; }
    public float MaterialWeight { get; set; }
    public Guid ActorId { get; set; }
    public ActorType ActorType { get; set; }
    public float BuildTime { get; set; }
    public bool WasValidPlacement { get; set; }
    public string InvalidReason { get; set; } // If failed
}
```

**Event Properties**:
```
Event Structure:
- EventId: Sequential unique ID
- WorldId: Target world
- Tick: Simulation tick when event occurred
- Position: Vector3 coordinates
- Actor: Who performed the action (player/agent ID)
- Payload: Event-specific data (MessagePack)
- Timestamp: Real-world time
- Checksum: Data integrity verification
```

### 8.2 Network Synchronization

**Synchronization Strategy**:
```
Authoritative Server Model:
- Server validates all terrain changes
- Client predicts for responsiveness
- Server corrects if prediction wrong
- Changes broadcast to all clients

Synchronization Flow:
1. Client requests block change
2. Server validates (permissions, range, resources)
3. Server applies change to world state
4. Server generates event
5. Server broadcasts to all clients in chunk
6. Clients apply change visually
7. Event persisted to database

Update Frequency:
- Immediate: Player who made change
- Within 50ms: Same chunk
- Within 100ms: Adjacent chunks
- Within 200ms: All clients
```

**Godot 4.x Network Implementation**:
```csharp
public class TerrainNetworkSync : Node {
    [Export] public int ChunkSyncDistance = 3;
    
    private VoxelWorld _voxelWorld;
    private NetworkManager _network;
    
    public override void _Ready() {
        _voxelWorld = GetNode<VoxelWorld>("VoxelWorld");
        _network = GetNode<NetworkManager>("/root/NetworkManager");
        
        // Subscribe to block changes
        _voxelWorld.BlockModified += OnBlockModified;
    }
    
    private void OnBlockModified(Vector3I pos, BlockChange change) {
        if (_network.IsServer) {
            // Server: Broadcast to clients
            BroadcastBlockChange(pos, change);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    public void SyncBlockChange(Vector3I pos, int blockType, Guid actorId) {
        // Clients receive this
        _voxelWorld.SetBlock(pos, (BlockType)blockType, false); // Don't trigger event
        SpawnModificationEffects(pos);
    }
    
    [Rpc(MultiplayerApi.RpcMode.Any)]
    public void RequestBlockChange(Vector3I pos, int toolType, int toolTier) {
        // Server receives request from client
        var player = _network.GetPlayer(Multiplayer.GetRemoteSenderId());
        
        if (ValidateBlockChange(player, pos, toolType)) {
            // Apply change
            var change = ApplyBlockChange(pos, toolType, toolTier, player.Id);
            
            // Sync to all
            SyncBlockChange(pos, (int)change.NewBlockType, player.Id);
        } else {
            // Reject
            RpcId(Multiplayer.GetRemoteSenderId(), nameof(RejectBlockChange), pos, "Invalid");
        }
    }
    
    private bool ValidateBlockChange(Player player, Vector3I pos, int toolType) {
        // Check range
        if (player.Position.DistanceTo(new Vector3(pos.X, pos.Y, pos.Z)) > 3.0f)
            return false;
        
        // Check permissions
        if (!HasMiningPermission(player, pos))
            return false;
        
        // Check tool validity
        var block = _voxelWorld.GetBlock(pos);
        if (!IsToolValidForBlock((ToolType)toolType, block))
            return false;
        
        return true;
    }
}
```

### 8.3 Rollback Capability

**Rollback System**:
```
Purpose: Undo griefing, fix bugs, revert mistakes

Rollback Types:
1. Player Rollback (self-service):
   - Last 5 minutes of changes
   - Limited to 20 blocks
   - Personal changes only
   - 10 minute cooldown

2. Admin Rollback:
   - Any time range
   - Any area size
   - Any player's changes
   - Requires admin privileges

3. Event-Driven Rollback:
   - Rollback specific event
   - Undo cascading effects
   - Restore materials to inventory

Rollback Process:
1. Query events in time/area range
2. Reverse events in reverse chronological order
3. Restore previous block states
4. Handle inventory restoration
5. Notify affected players
```

**Rollback Implementation**:
```csharp
public class TerrainRollbackSystem {
    private IEventRepository _eventRepo;
    private VoxelWorld _voxelWorld;
    
    public async Task RollbackAsync(
        Guid worldId, 
        BoundingBox area, 
        TimeRange timeRange,
        RollbackOptions options) {
        
        // 1. Query terrain events in range
        var events = await _eventRepo.QueryEventsAsync(
            worldId,
            timeRange.Start,
            timeRange.End,
            new[] { TerrainEventType.BlockRemoved, TerrainEventType.BlockPlaced }
        );
        
        // 2. Filter by area
        var affectedEvents = events
            .Where(e => area.Contains(e.Position))
            .OrderByDescending(e => e.Tick) // Reverse chronological
            .ToList();
        
        // 3. Apply reversions
        foreach (var evt in affectedEvents) {
            await RevertEventAsync(evt);
        }
        
        // 4. Log rollback
        await LogRollbackAsync(worldId, area, timeRange, affectedEvents.Count);
    }
    
    private async Task RevertEventAsync(GameEvent evt) {
        switch (evt.Type) {
            case TerrainEventType.BlockRemoved:
                // Revert: Place block back
                var removedPayload = evt.DeserializePayload<BlockRemovedPayload>();
                _voxelWorld.SetBlock(removedPayload.Position, removedPayload.BlockType);
                break;
                
            case TerrainEventType.BlockPlaced:
                // Revert: Remove block
                var placedPayload = evt.DeserializePayload<BlockPlacedPayload>();
                _voxelWorld.SetBlock(placedPayload.Position, BlockType.Air);
                
                // Return materials if requested
                if (options.ReturnMaterials) {
                    await ReturnMaterialsToOwner(placedPayload);
                }
                break;
        }
    }
}
```

### 8.4 Anti-Griefing Measures

**Protection Systems**:
```
Claim Protection:
- Personal claim: Owner has full rights
- Town claim: Citizens have build rights
- Permissions: Granular (mine, build, use)
- Violation: Action blocked, warning issued

Rate Limiting:
- Max blocks per minute: 60
- Burst allowance: 10 blocks instantly
- Cooldown: 2 second penalty if exceeded
- Exemption: VIPs, admins

New Player Restrictions:
- First 24 hours: Cannot modify protected areas
- First hour: Rate limit halved
- Reputation threshold needed for large projects

Automated Detection:
- Pattern analysis (mass destruction)
- Unusual behavior flags
- Admin notifications for review
- Auto-rollback for confirmed griefing
```

**Implementation**:
```csharp
public class AntiGriefingSystem {
    private Dictionary<Guid, PlayerMiningStats> _playerStats = new();
    
    public bool CanModifyBlock(Player player, Vector3I pos, BlockChangeType changeType) {
        // Check claim permissions
        var claim = _claimManager.GetClaimAt(pos);
        if (claim != null && !claim.HasPermission(player, changeType)) {
            NotifyPlayer(player, "No permission to modify in this area");
            return false;
        }
        
        // Check rate limits
        var stats = GetOrCreateStats(player.Id);
        if (!stats.CanMine()) {
            NotifyPlayer(player, "Mining too fast - please slow down");
            return false;
        }
        
        // Check new player restrictions
        if (player.AccountAge < TimeSpan.FromHours(24) && 
            claim?.IsProtected == true) {
            NotifyPlayer(player, "New players cannot modify protected areas");
            return false;
        }
        
        // Record action
        stats.RecordAction();
        
        return true;
    }
    
    public void DetectGriefingPatterns() {
        foreach (var kvp in _playerStats) {
            var stats = kvp.Value;
            
            // Detect mass destruction
            if (stats.BlocksRemovedLastHour > 1000 &&
                stats.UniqueAreasModified > 10) {
                FlagForReview(kvp.Key, "Suspicious mass destruction pattern");
            }
            
            // Detect protected area attacks
            if (stats.ProtectedAreaViolations > 5) {
                TemporarilyRestrict(kvp.Key, TimeSpan.FromHours(1));
            }
        }
    }
}
```

---

## 9. Technical Implementation

### 9.1 VoxelWorld.ModifyBlock() API

**Core API Design**:
```csharp
public class VoxelWorld : Node3D {
    private VoxelData _voxelData;
    private TerrainNetworkSync _networkSync;
    private EventSourcing _eventSourcing;
    
    /// <summary>
    /// Modify a block at the specified position
    /// </summary>
    public BlockChangeResult ModifyBlock(
        Vector3I position,
        BlockModification modification,
        Entity actor) {
        
        // 1. Validate
        var validation = ValidateModification(position, modification, actor);
        if (!validation.IsValid) {
            return BlockChangeResult.Failed(validation.ErrorMessage);
        }
        
        // 2. Get current state
        var oldBlock = _voxelData.GetBlock(position);
        
        // 3. Apply change
        BlockType newBlock;
        List<ItemStack> yields;
        
        switch (modification.Type) {
            case ModificationType.Remove:
                (newBlock, yields) = RemoveBlock(position, oldBlock, modification.Tool);
                break;
                
            case ModificationType.Place:
                (newBlock, yields) = PlaceBlock(position, modification.BlockType, actor);
                break;
                
            case ModificationType.Replace:
                (newBlock, yields) = ReplaceBlock(position, oldBlock, modification.BlockType, actor);
                break;
                
            default:
                return BlockChangeResult.Failed("Unknown modification type");
        }
        
        // 4. Update voxel data
        _voxelData.SetBlock(position, newBlock);
        
        // 5. Update mesh
        UpdateChunkMesh(position);
        
        // 6. Spawn effects
        SpawnModificationEffects(position, oldBlock, newBlock, modification.Type);
        
        // 7. Create event
        var evt = CreateTerrainEvent(position, oldBlock, newBlock, modification, actor, yields);
        _eventSourcing.RecordEvent(evt);
        
        // 8. Network sync
        _networkSync.BroadcastChange(position, newBlock, actor.Id);
        
        // 9. Return result
        return BlockChangeResult.Success(newBlock, yields, evt.EventId);
    }
    
    private (BlockType newBlock, List<ItemStack> yields) RemoveBlock(
        Vector3I pos, BlockType oldBlock, ToolInstance tool) {
        
        // Calculate yields
        var yields = CalculateMiningYields(oldBlock, tool);
        
        // Spawn debris
        SpawnDebris(pos, oldBlock, tool.Tier);
        
        // Return air block
        return (BlockType.Air, yields);
    }
    
    private (BlockType newBlock, List<ItemStack> yields) PlaceBlock(
        Vector3I pos, BlockType blockType, Entity actor) {
        
        // Consume materials from actor inventory
        var requiredStack = new ItemStack(blockType, 1);
        if (!actor.Inventory.Remove(requiredStack)) {
            return (BlockType.Air, new List<ItemStack>()); // Failed
        }
        
        // Return placed block
        return (blockType, new List<ItemStack>());
    }
}

// Supporting types
public struct BlockModification {
    public ModificationType Type { get; set; }
    public BlockType? BlockType { get; set; }
    public ToolInstance Tool { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public enum ModificationType {
    Remove,
    Place,
    Replace
}

public class BlockChangeResult {
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public BlockType NewBlockType { get; set; }
    public List<ItemStack> Yields { get; set; }
    public long EventId { get; set; }
    
    public static BlockChangeResult Success(BlockType newBlock, List<ItemStack> yields, long eventId) {
        return new BlockChangeResult { 
            Success = true, 
            NewBlockType = newBlock, 
            Yields = yields,
            EventId = eventId
        };
    }
    
    public static BlockChangeResult Failed(string message) {
        return new BlockChangeResult { Success = false, ErrorMessage = message };
    }
}
```

### 9.2 Event System for Changes

**Event Creation and Distribution**:
```csharp
public class TerrainEventSystem {
    public event Action<TerrainEvent> OnTerrainModified;
    public event Action<DebrisEvent> OnDebrisSpawned;
    
    public TerrainEvent CreateTerrainEvent(
        Vector3I position,
        BlockType oldBlock,
        BlockType newBlock,
        BlockModification modification,
        Entity actor,
        List<ItemStack> yields) {
        
        var eventType = modification.Type switch {
            ModificationType.Remove => TerrainEventType.BlockRemoved,
            ModificationType.Place => TerrainEventType.BlockPlaced,
            ModificationType.Replace => TerrainEventType.BlockReplaced,
            _ => throw new ArgumentException()
        };
        
        var payload = new BlockChangePayload {
            Position = position,
            OldBlockType = oldBlock,
            NewBlockType = newBlock,
            ModificationType = modification.Type,
            ActorId = actor.Id,
            ActorType = actor.IsPlayer ? ActorType.Player : ActorType.Agent,
            ToolUsed = modification.Tool?.ToolId,
            Yields = yields.Select(y => new ItemYield {
                ItemId = y.ItemId,
                Quantity = y.Quantity,
                Quality = y.Quality
            }).ToList(),
            Timestamp = DateTime.UtcNow
        };
        
        var evt = new TerrainEvent {
            EventId = GenerateEventId(),
            Type = eventType,
            Position = new Vector3(position.X, position.Y, position.Z),
            ActorId = actor.Id,
            Payload = MessagePackSerializer.Serialize(payload),
            Timestamp = DateTime.UtcNow,
            Tick = _gameState.CurrentTick
        };
        
        // Fire event
        OnTerrainModified?.Invoke(evt);
        
        return evt;
    }
    
    public DebrisEvent CreateDebrisEvent(
        Vector3 position,
        BlockType sourceMaterial,
        int chunkCount,
        float totalMass) {
        
        var payload = new DebrisSpawnedPayload {
            SpawnPosition = position,
            SourceBlockType = sourceMaterial,
            ChunkCount = chunkCount,
            TotalMass = totalMass,
            Lifetime = 300.0f // 5 minutes
        };
        
        var evt = new DebrisEvent {
            EventId = GenerateEventId(),
            Type = TerrainEventType.DebrisSpawned,
            Position = position,
            Payload = MessagePackSerializer.Serialize(payload),
            Timestamp = DateTime.UtcNow
        };
        
        OnDebrisSpawned?.Invoke(evt);
        
        return evt;
    }
}
```

### 9.3 Undo/Redo System

**Personal Undo Stack**:
```csharp
public class PersonalUndoSystem {
    private const int MaxUndoHistory = 20;
    private Dictionary<Guid, Stack<TerrainAction>> _playerUndoStacks = new();
    private Dictionary<Guid, Stack<TerrainAction>> _playerRedoStacks = new();
    
    public void RecordAction(Player player, TerrainAction action) {
        var stack = GetUndoStack(player.Id);
        
        // Add to undo stack
        stack.Push(action);
        
        // Trim if exceeds max
        while (stack.Count > MaxUndoHistory) {
            var old = stack.Pop();
            old.Dispose(); // Clean up resources
        }
        
        // Clear redo stack on new action
        ClearRedoStack(player.Id);
    }
    
    public bool CanUndo(Player player) {
        return GetUndoStack(player.Id).Count > 0;
    }
    
    public UndoResult Undo(Player player) {
        if (!CanUndo(player)) {
            return UndoResult.Failed("No actions to undo");
        }
        
        var stack = GetUndoStack(player.Id);
        var action = stack.Pop();
        
        // Revert the action
        var revertResult = action.Revert();
        
        if (revertResult.Success) {
            // Add to redo stack
            GetRedoStack(player.Id).Push(action);
            return UndoResult.Success(action.Description);
        } else {
            // Put back on undo stack if failed
            stack.Push(action);
            return UndoResult.Failed(revertResult.ErrorMessage);
        }
    }
    
    public bool CanRedo(Player player) {
        return GetRedoStack(player.Id).Count > 0;
    }
    
    public RedoResult Redo(Player player) {
        if (!CanRedo(player)) {
            return RedoResult.Failed("No actions to redo");
        }
        
        var stack = GetRedoStack(player.Id);
        var action = stack.Pop();
        
        // Re-apply the action
        var applyResult = action.Apply();
        
        if (applyResult.Success) {
            // Add back to undo stack
            GetUndoStack(player.Id).Push(action);
            return RedoResult.Success(action.Description);
        } else {
            // Put back on redo stack if failed
            stack.Push(action);
            return RedoResult.Failed(applyResult.ErrorMessage);
        }
    }
    
    private Stack<TerrainAction> GetUndoStack(Guid playerId) {
        if (!_playerUndoStacks.ContainsKey(playerId)) {
            _playerUndoStacks[playerId] = new Stack<TerrainAction>();
        }
        return _playerUndoStacks[playerId];
    }
    
    private Stack<TerrainAction> GetRedoStack(Guid playerId) {
        if (!_playerRedoStacks.ContainsKey(playerId)) {
            _playerRedoStacks[playerId] = new Stack<TerrainAction>();
        }
        return _playerRedoStacks[playerId];
    }
}

// Action representation
public abstract class TerrainAction {
    public Guid ActionId { get; set; }
    public string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public Vector3I Position { get; set; }
    
    public abstract ActionResult Apply();
    public abstract ActionResult Revert();
    public abstract void Dispose();
}

public class BlockRemovalAction : TerrainAction {
    public BlockType BlockType { get; set; }
    public List<ItemStack> Yields { get; set; }
    
    public override ActionResult Revert() {
        // Place block back
        return _voxelWorld.ModifyBlock(
            Position,
            new BlockModification { 
                Type = ModificationType.Place, 
                BlockType = BlockType 
            },
            _systemActor
        ).ToActionResult();
    }
    
    public override ActionResult Apply() {
        // Remove block again
        return _voxelWorld.ModifyBlock(
            Position,
            new BlockModification { Type = ModificationType.Remove },
            _systemActor
        ).ToActionResult();
    }
}
```

### 9.4 C# Code Examples

**Example 1: Mining a Block**:
```csharp
public class MiningController : Node {
    [Export] public float MiningRange = 3.0f;
    
    private Player _player;
    private VoxelWorld _voxelWorld;
    private ToolSystem _toolSystem;
    
    public void AttemptMining(Vector3 targetPosition) {
        // Check range
        if (_player.Position.DistanceTo(targetPosition) > MiningRange) {
            ShowMessage("Too far away");
            return;
        }
        
        // Get equipped tool
        var tool = _player.Equipment.GetEquippedTool();
        if (tool == null) {
            ShowMessage("No tool equipped");
            return;
        }
        
        // Get target block
        var blockPos = WorldToBlockPosition(targetPosition);
        var block = _voxelWorld.GetBlock(blockPos);
        
        // Check if tool can mine this block
        if (!tool.CanMine(block)) {
            ShowMessage($"Cannot mine {block} with {tool.ToolType}");
            return;
        }
        
        // Check if block is mineable
        if (block == BlockType.Bedrock) {
            ShowMessage("Cannot mine bedrock");
            return;
        }
        
        // Check inventory space
        var expectedYields = _toolSystem.CalculateYields(block, tool, _player.Skills.Gathering);
        var totalWeight = expectedYields.Sum(y => y.Weight);
        
        if (_player.Inventory.CurrentWeight + totalWeight > _player.MaxWeight) {
            ShowMessage("Too heavy - cannot carry more materials");
            return;
        }
        
        // Perform mining
        var result = _voxelWorld.ModifyBlock(
            blockPos,
            new BlockModification {
                Type = ModificationType.Remove,
                Tool = tool
            },
            _player
        );
        
        if (result.Success) {
            // Consume tool durability
            tool.ConsumeDurability(block.Hardness);
            
            // Add yields to inventory
            foreach (var yield in result.Yields) {
                _player.Inventory.Add(yield);
            }
            
            // Award skill XP
            _player.Skills.Gathering.AddXp(CalculateXpGain(block, tool));
            
            // Play effects
            PlayMiningEffects(blockPos, block, tool);
        } else {
            ShowMessage($"Mining failed: {result.ErrorMessage}");
        }
    }
}
```

**Example 2: Placing a Block**:
```csharp
public class BuildingController : Node {
    private Player _player;
    private VoxelWorld _voxelWorld;
    private GhostPreview _ghostPreview;
    
    public void EnterBuildMode(BlockType blockType) {
        // Create ghost preview
        _ghostPreview.Show(blockType);
        _ghostPreview.SetValid(false);
    }
    
    public void UpdateGhostPosition(Vector3 targetPosition) {
        var blockPos = WorldToBlockPosition(targetPosition);
        _ghostPreview.Position = blockPos;
        
        // Validate placement
        var canPlace = ValidatePlacement(blockPos);
        _ghostPreview.SetValid(canPlace);
    }
    
    public void PlaceBlock() {
        var blockPos = _ghostPreview.Position;
        var blockType = _ghostPreview.BlockType;
        
        // Final validation
        var validation = ValidatePlacementDetailed(blockPos, blockType);
        if (!validation.IsValid) {
            ShowMessage(validation.ErrorMessage);
            return;
        }
        
        // Check weight
        var blockWeight = BlockDatabase.GetWeight(blockType);
        if (_player.CurrentWeight + blockWeight > _player.MaxWeight) {
            ShowMessage("Too heavy to carry this block");
            return;
        }
        
        // Place block
        var result = _voxelWorld.ModifyBlock(
            blockPos,
            new BlockModification {
                Type = ModificationType.Place,
                BlockType = blockType
            },
            _player
        );
        
        if (result.Success) {
            PlayPlacementEffects(blockPos, blockType);
            
            // Award skill XP
            _player.Skills.Building.AddXp(5);
        }
    }
    
    private ValidationResult ValidatePlacementDetailed(Vector3I pos, BlockType type) {
        // Check collision
        if (_voxelWorld.IsBlockOccupied(pos)) {
            return ValidationResult.Fail("Space is occupied");
        }
        
        // Check ground support
        var supportPos = pos - Vector3I.Up;
        if (!_voxelWorld.IsSolidBlock(supportPos)) {
            return ValidationResult.Fail("No ground support");
        }
        
        // Check permissions
        if (!_claimManager.HasBuildPermission(_player, pos)) {
            return ValidationResult.Fail("No building permission");
        }
        
        return ValidationResult.Success();
    }
}
```

**Example 3: Weight Management**:
```csharp
public class WeightSystem : Node {
    private Player _player;
    private Label _weightLabel;
    private ProgressBar _weightBar;
    
    public override void _Ready() {
        _player.Inventory.OnInventoryChanged += UpdateWeightDisplay;
        UpdateWeightDisplay();
    }
    
    private void UpdateWeightDisplay() {
        var current = _player.Inventory.TotalWeight;
        var max = _player.MaxWeight;
        var ratio = current / max;
        
        // Update UI
        _weightLabel.Text = $"{current:F1} / {max:F0} kg";
        _weightBar.Value = ratio * 100;
        
        // Update colors
        if (ratio < 0.5f) {
            _weightBar.Modulate = Colors.Green;
        } else if (ratio < 0.75f) {
            _weightBar.Modulate = Colors.Yellow;
        } else if (ratio < 0.9f) {
            _weightBar.Modulate = Colors.Orange;
        } else {
            _weightBar.Modulate = Colors.Red;
        }
        
        // Apply encumbrance effects
        ApplyEncumbranceEffects(ratio);
    }
    
    private void ApplyEncumbranceEffects(float ratio) {
        var movement = _player.GetComponent<MovementController>();
        
        if (ratio <= 0.25f) {
            // Light load
            movement.SetSpeedMultiplier(1.0f);
            movement.CanSprint = true;
            movement.CanJump = true;
        } else if (ratio <= 0.5f) {
            // Medium load
            movement.SetSpeedMultiplier(0.8f);
            movement.CanSprint = true;
            movement.CanJump = true;
        } else if (ratio <= 0.75f) {
            // Heavy load
            movement.SetSpeedMultiplier(0.6f);
            movement.CanSprint = false;
            movement.CanJump = true;
        } else {
            // Over-encumbered
            movement.SetSpeedMultiplier(0.3f);
            movement.CanSprint = false;
            movement.CanJump = false;
        }
    }
    
    public bool CanAddWeight(float weight) {
        return (_player.Inventory.TotalWeight + weight) <= _player.MaxWeight;
    }
}
```

---

## 10. Balance Considerations

### 10.1 Early Game vs Late Game Modification

**Early Game (First 1-2 Hours)**:
```
Characteristics:
- Stone tools only (50 durability, 100% speed)
- Limited carrying capacity (40-50 kg)
- Can only mine basic materials (stone, dirt, surface coal)
- No access to ores or hard materials
- Manual transport only (no vehicles)

Balancing Goals:
- Make early building meaningful but challenging
- Force strategic decisions about what to carry
- Encourage resource prioritization
- Create sense of progression

Implementation:
- Stone pickaxe: 1.0 second per stone block
- Can carry: 2-3 stone blocks or 3-6 dirt blocks
- Early projects should be small (tool shed, basic shelter)
```

**Mid Game (Hours 2-10)**:
```
Characteristics:
- Iron tools available (150 durability, 150% speed)
- Access to all ore types
- Can mine deeper, larger projects feasible
- Animal transport available (200-500 kg capacity)
- Increased carrying capacity through equipment

Balancing Goals:
- Significant improvement over stone
- Enable medium-sized building projects
- Make mining operations worthwhile
- Bridge to advanced gameplay

Implementation:
- Iron pickaxe: 0.67 seconds per stone block (50% faster)
- Can carry: 5-10 stone blocks with backpack
- Enable small mining operations
- Road building becomes efficient
```

**Late Game (10+ Hours)**:
```
Characteristics:
- Steel tools (500 durability, 200% speed)
- Mechanical transport (1000+ kg)
- Massive terraforming projects possible
- Specialized tools (leveler, trencher)
- High skill levels reduce costs

Balancing Goals:
- Enable ambitious projects
- Reduce tedium for experienced players
- Support cooperative mega-projects
- Maintain meaningful choices

Implementation:
- Steel pickaxe: 0.5 seconds per stone block
- Vehicles carry 1000s of kg
- Can reshape large areas
- Automation reduces manual labor
```

### 10.2 Tool Progression

**Progression Curve**:
```
Tier     Durability  Speed   Yield Bonus  Relative Power
───────────────────────────────────────────────────────
Stone    50          1.0×    0%           1.0 (baseline)
Iron     150         1.5×    +15%         2.6×
Steel    500         2.0×    +25%         6.7×

Power calculation: (Durability/50) × Speed × (1 + YieldBonus)
Stone: 1 × 1.0 × 1.0 = 1.0
Iron: 3 × 1.5 × 1.15 = 5.2 (but limited by durability usage)
Steel: 10 × 2.0 × 1.25 = 25.0

Realistic effective power:
Stone: 1.0
Iron: 2.6 (accounting for repair, efficiency)
Steel: 6.7 (late-game convenience)
```

**Tool Economics**:
```
Cost vs Benefit Analysis:

Stone Pickaxe:
- Cost: 3 stone + 2 wood (easily renewable)
- Effective uses: ~40 (accounting for repair degradation)
- Value: High (essential starting tool)

Iron Pickaxe:
- Cost: 3 iron + 2 wood (requires mining, smelting)
- Effective uses: ~120
- Break-even: After mining 80 stone blocks
- Value: Very high (enables ore mining)

Steel Pickaxe:
- Cost: 3 steel + 2 hardwood (advanced materials)
- Effective uses: ~400
- Break-even: After mining 300 stone blocks
- Value: High (convenience, speed)
```

### 10.3 Resource Abundance/Scarcity

**Resource Distribution Philosophy**:
```
Design Goals:
- Resources should feel valuable
- Over-harvesting should have consequences
- Location should matter for specialization
- Late-game resources rarer than early-game

Stone/Dirt (Abundant):
- Everywhere on surface
- Regenerates slowly (weeks real-time)
- No strategic value
- Weight is the limiting factor

Wood (Abundant but Renewed):
- Forest biomes plentiful
- Requires time to regrow (days)
- Management creates emergent gameplay
- Deforestation possible

Ores (Scarce, Strategic):
- Rare surface deposits
- Underground veins
- Finite (no regeneration)
- Creates economic value

Special Materials (Very Scarce):
- Gold, gems, rare woods
- Strategic control points
- Drives conflict and cooperation
- Economic power concentration
```

**Scarcity Implementation**:
```
Ore Distribution Model:
- Surface deposits: 5% of chunks, visible
- Shallow veins (10-30m): 15% of chunks
- Deep veins (30-100m): 25% of chunks
- Richness varies (poor/average/rich/very rich)
- Depletion tracking per vein

Consequences of Scarcity:
- Mining rights become valuable
- Trade networks emerge
- Conflicts over resources
- Encourages exploration
- Creates specialization (miners vs builders)

Renewable vs Non-Renewable:
Renewable:
- Dirt (slow geological processes)
- Wood (replanting)
- Stone (effectively infinite)

Non-Renewable:
- All ores
- Coal
- Special materials
- Drives economic activity
```

---

## Document History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-02-01 | 1.0 | Initial comprehensive specification | Session 1 |

---

**END OF DOCUMENT**

*All numerical values must reference `planning/meta/technical-constants.md`. Discrepancies should be flagged for resolution.*

**Key Integration Points**:
- Tool system: `planning/sessions/session-3-core-gameplay-loops/01d-tool-system-spec.md`
- Movement/weight: `planning/sessions/session-3-core-gameplay-loops/01c-movement-interaction-spec.md`
- Event sourcing: `planning/sessions/session-1-technical-architecture/10-event-sourcing.md`
- Technical constants: `planning/meta/technical-constants.md`
