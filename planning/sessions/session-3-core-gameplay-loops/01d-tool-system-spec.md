# Tool System Specification

**Status**: SPECIFICATION COMPLETE  
**Last Updated**: 2026-02-01  
**Location**: Session 3 - Core Gameplay Loops  
**Dependencies**:  
- `planning/meta/technical-constants.md` (Sections 9, 11)
- `planning/sessions/session-3-core-gameplay-loops/01-moment-to-moment-gameplay.md`
- `planning/sessions/session-2-ai-system-design/06-ai-skills-reference.md`

---

## Document Purpose

This specification defines the complete tool system for Societies, including all 8 tool types, 4 tier progression, durability mechanics, quality systems, and economic values. All numerical values reference `planning/meta/technical-constants.md` as the authoritative source.

---

## 1. Tool Types & Purposes

### 1.1 Gathering Tools (5 Types)

#### 1.1.1 Axe - Wood Gathering
```
Tool ID: tool_axe
Primary Use: Wood gathering from trees and wooden structures
Valid Targets:
  - Trees (all types: oak, pine, birch, etc.)
  - Wooden structures (abandoned buildings, debris)
  - Stumps and fallen logs

Yields:
  - Wood: 3-8 units per tree (varies by tree size)
  - Branches: 1-3 units (25% chance per tree)
  - Bark: 0-2 units (50% chance per tree)
  - Rare: Tree sap, mushrooms (5% chance)

Animation Sequence:
  1. Wind-up: 0.4s (raise axe overhead)
  2. Strike: 0.2s (downward chop motion)
  3. Recovery: 0.3s (return to ready position)
  4. Total cycle time: 0.9s base (modified by tool tier)

Audio:
  - Wind-up: Soft whoosh
  - Impact: Sharp chop sound (varies by material)
  - Recovery: Tool movement rustle
```

#### 1.1.2 Pickaxe - Stone/Mining
```
Tool ID: tool_pickaxe
Primary Use: Mining stone, ore deposits, and stone structures
Valid Targets:
  - Rock outcroppings and boulders
  - Ore deposits (iron, copper, coal, precious metals)
  - Stone walls and foundations
  - Bedrock (requires steel tier)

Yields:
  - Stone: 2-5 units per rock
  - Ore: 1-3 units per deposit (type depends on deposit)
  - Gems: Rare (1-5% chance with steel tier)
  - Minerals: Salt, sulfur (location-dependent)

Mining Tier Requirements:
  - Stone tier: Can mine stone, surface coal
  - Iron tier: Can mine iron ore, copper, deep coal
  - Steel tier: Can mine all ores including gold, silver, gems

Animation Sequence:
  1. Wind-up: 0.5s (raise pick overhead)
  2. Strike: 0.15s (powerful downward strike)
  3. Recovery: 0.4s (extract from material)
  4. Total cycle time: 1.05s base

Audio:
  - Wind-up: Heavy tool lift sound
  - Impact: Sharp metallic strike (varies by hardness)
  - Recovery: Stone/mineral crumble sound
```

#### 1.1.3 Hoe - Soil/Farming
```
Tool ID: tool_hoe
Primary Use: Preparing soil for farming, uncovering buried items
Valid Targets:
  - Soil and dirt terrain
  - Grass (converts to tilled soil)
  - Sand (beach/sand dune areas)

Yields:
  - Tilled soil: 1 tile prepared per use
  - Worms: 1-2 units (30% chance, used for fishing/bait)
  - Seeds: Rare seeds (5% chance, random type)
  - Clay: 1 unit (10% chance in wet soil areas)

Farming Mechanics:
  - Soil quality: Tilled soil has quality 1-5 (affects crop yield)
  - Fertility bonus: High-quality soil increases crop yield by 10-50%
  - Retention: Tilled soil remains for 3 in-game days without crops
  - Decay: Untended tilled soil reverts to dirt after 7 days

Animation Sequence:
  1. Draw back: 0.3s (hoe swings back)
  2. Strike: 0.2s (downward chop into soil)
  3. Pull: 0.3s (draw hoe back, turning soil)
  4. Total cycle time: 0.8s base

Audio:
  - Draw: Soft tool movement
  - Strike: Thud/dirt impact
  - Pull: Soil turning/scraping sound
```

#### 1.1.4 Sickle - Plants/Reaping
```
Tool ID: tool_sickle
Primary Use: Harvesting plants, tall grass, and crops efficiently
Valid Targets:
  - Tall grass (wheat, reeds, wild grasses)
  - Bushes (berry bushes, tea plants)
  - Crops (wheat, barley, rice when mature)
  - Vines and climbing plants

Yields:
  - Plant fiber: 2-4 units per grass patch
  - Seeds: 1-2 units (40% chance, matching plant type)
  - Produce: Berries, tea leaves (if target is bush)
  - Rare: Medicinal herbs (5% chance)

Harvest Efficiency:
  - Stone sickle: 1 tile per swing
  - Iron sickle: 2 tiles per swing (area effect)
  - Steel sickle: 3 tiles per swing (larger area)

Animation Sequence:
  1. Wind-up: 0.25s (sickle arm back)
  2. Slice: 0.2s (horizontal sweeping motion)
  3. Follow-through: 0.25s (continue arc)
  4. Total cycle time: 0.7s base

Audio:
  - Wind-up: Soft swish
  - Slice: Cutting/grass sound
  - Follow-through: Rustling vegetation
```

#### 1.1.5 Shovel - Digging
```
Tool ID: tool_shovel
Primary Use: Digging terrain, uncovering buried resources, grave digging
Valid Targets:
  - Dirt terrain (all types)
  - Sand (beaches, deserts)
  - Gravel and loose stone
  - Snow (if applicable to biome)

Yields:
  - Terrain block: 1 unit per dig (can place elsewhere)
  - Buried items: 5% chance to find buried cache
  - Roots: 1-2 units (20% chance, used for crafting)
  - Fossils: Rare (1% chance, decorative/collectible)

Digging Mechanics:
  - Depth: Shovel removes top 0.5m of terrain
  - Hole creation: Repeated digging creates holes (up to 2m deep)
  - Hole uses: Storage pits, foundations, traps
  - Terrain modification: Permanent terrain change

Animation Sequence:
  1. Stab: 0.3s (drive shovel into ground)
  2. Lever: 0.3s (push handle down, lift dirt)
  3. Toss: 0.3s (throw dirt aside)
  4. Total cycle time: 0.9s base

Audio:
  - Stab: Thud/impact sound
  - Lever: Dirt shifting
  - Toss: Soft dirt scatter
```

### 1.2 Utility Tools (3 Types)

#### 1.2.1 Hammer - Building/Crafting
```
Tool ID: tool_hammer
Primary Use: Building construction, item repairs, crafting
Valid Uses:
  - Building placement (confirms construction)
  - Structure repair (restores durability)
  - Crafting station interaction (required at anvil/workbench)
  - Demolition (removes player-built structures)

Building Mechanics:
  - Placement confirmation: Hammer strike finalizes building
  - Repair: Restores building durability (consumes materials)
  - Upgrade: Improves building tier (requires materials + skill)
  - Demolition: Returns 50% of materials (can be improved with skill)

Repair Formula:
  RepairAmount = BuildingMaxDurability × 0.25 (per repair action)
  MaterialCost = BuildingBaseCost × 0.10 (10% of original cost)
  SkillBonus: +5% repair per Crafting skill level

Animation Sequence:
  1. Raise: 0.3s (hammer drawn back)
  2. Strike: 0.15s (forward hammer motion)
  3. Rebound: 0.2s (hammer bounces back)
  4. Total cycle time: 0.65s base

Audio:
  - Raise: Tool movement
  - Strike: Metallic thud (building) or clang (anvil)
  - Rebound: Tool bounce sound
```

#### 1.2.2 Saw - Wood Processing
```
Tool ID: tool_saw
Primary Use: Processing wood into planks and refined materials
Valid Uses:
  - Workbench crafting (converts wood to planks)
  - Carpentry station (advanced wood crafting)
  - Dismantling wooden objects (returns materials)

Processing Yields:
  - Planks: 4 units per wood log (at workbench)
  - Refined wood: 2 units per plank (at carpentry station)
  - Sawdust: Byproduct (used for paper, compost)

Efficiency by Tier:
  - Stone saw: 1 log → 2 planks (50% waste)
  - Iron saw: 1 log → 4 planks (standard)
  - Steel saw: 1 log → 6 planks (bonus yield)

Animation Sequence:
  1. Position: 0.4s (align saw with wood)
  2. Saw stroke 1: 0.4s (push forward)
  3. Saw stroke 2: 0.4s (pull back)
  4. Repeat: 2-4 cycles per log
  5. Total cycle time: 3.2-5.6s depending on material

Audio:
  - Position: Wood placement
  - Saw strokes: Rhythmic sawing sound
  - Completion: Wood split/crack
```

#### 1.2.3 Knife - Crafting/Food Preparation
```
Tool ID: tool_knife
Primary Use: Detailed crafting, food preparation, material processing
Valid Uses:
  - Food preparation (butchering, skinning, filleting)
  - Material processing (leather working, carving)
  - Crafting component creation (handles, triggers)
  - Self-defense (minimal damage, emergency only)

Processing Yields:
  - Meat: Animal → 3-5 meat units (butchering)
  - Hide: Animal → 1 hide unit (skinning)
  - Leather: Hide → 1 leather unit (tanning)
  - Components: Wood/metal → 2-3 component units (carving)

Crafting Uses:
  - Handle crafting: Wood → tool handles
  - Trigger mechanisms: Metal → mechanical parts
  - Decorative items: Various materials → decorations
  - Carving: Wood → specific shapes for recipes

Animation Sequence:
  1. Grip: 0.2s (adjust knife hold)
  2. Cut: 0.2s (precise slicing motion)
  3. Reset: 0.2s (return to ready position)
  4. Total cycle time: 0.6s base

Audio:
  - Grip: Soft adjustment
  - Cut: Sharp slice sound (varies by material)
  - Reset: Tool movement
```

---

## 2. Tool Tiers & Progression

### 2.1 Reference Constants

Per `technical-constants.md` Section 9:
```csharp
// Tool uses before breaking
public const int TOOL_DURABILITY_STONE = 50;
public const int TOOL_DURABILITY_IRON = 150;
public const int TOOL_DURABILITY_STEEL = 500;

// Tool efficiency multipliers
public const float TOOL_EFFICIENCY_STONE = 1.0f;
public const float TOOL_EFFICIENCY_IRON = 1.5f;
public const float TOOL_EFFICIENCY_STEEL = 2.0f;
```

### 2.2 Tier 1: Stone Tools (Basic)

#### Stone Axe
```
Tool ID: tool_axe_stone
Tier: 1 (Stone/Basic)
Durability: 50 uses (per TOOL_DURABILITY_STONE)
Gathering Speed: 100% (baseline, TOOL_EFFICIENCY_STONE)
Material Cost:
  - Stone: 3 units
  - Wood: 2 units (for handle)
Unlock Requirement: None (starting craftable)
Repairable: Yes
Repair Cost: 50% of material cost (1-2 stone + 1 wood)
Repair Durability Restored: 80% (40 uses, per TOOL_REPAIR_DURABILITY_RESTORED)

Visual Appearance:
  - Crude stone head lashed to wooden handle with fiber
  - Rough, chipped stone texture
  - Worn appearance as durability decreases
  
Special Properties:
  - Cannot harvest hardwood trees
  - 25% chance to fail on stone-tier ores (no yield, durability still lost)
  - Baseline for all tool comparisons
```

#### Stone Pickaxe
```
Tool ID: tool_pickaxe_stone
Tier: 1 (Stone/Basic)
Durability: 50 uses
Mining Speed: 100% (baseline)
Material Cost:
  - Stone: 3 units
  - Wood: 2 units
Unlock Requirement: None
Repairable: Yes (same as axe)

Mining Capabilities:
  - Can mine: Stone, surface coal deposits
  - Cannot mine: Iron ore, deep ores, precious metals
  - Failure chance: 25% on hard stone (granite, basalt)
```

#### Stone Hoe
```
Tool ID: tool_hoe_stone
Tier: 1 (Stone/Basic)
Durability: 50 uses
Tilling Speed: 100% (baseline)
Material Cost:
  - Stone: 2 units (flat stone for blade)
  - Wood: 2 units (for handle)
Unlock Requirement: None

Tilling Properties:
  - Creates basic tilled soil (quality 1-2)
  - 20% chance to damage soil (reduced quality)
  - Cannot till clay-heavy soil
```

#### Stone Sickle
```
Tool ID: tool_sickle_stone
Tier: 1 (Stone/Basic)
Durability: 50 uses
Harvest Speed: 100% (baseline)
Harvest Area: 1 tile
Material Cost:
  - Stone: 2 units (sharpened edge)
  - Wood: 1 unit (curved handle)
Unlock Requirement: None

Harvest Properties:
  - Single tile harvest only
  - 15% chance to damage plants (reduced yield)
  - Cannot harvest tough plants (bamboo, woody stems)
```

#### Stone Shovel
```
Tool ID: tool_shovel_stone
Tier: 1 (Stone/Basic)
Durability: 50 uses
Digging Speed: 100% (baseline)
Material Cost:
  - Stone: 2 units (broad flat stone)
  - Wood: 2 units (long handle)
Unlock Requirement: None

Digging Properties:
  - Can dig: Dirt, sand, loose gravel
  - Cannot dig: Packed earth, clay, stone
  - 20% chance to break on rocky soil
```

#### Stone Hammer
```
Tool ID: tool_hammer_stone
Tier: 1 (Stone/Basic)
Durability: 50 uses
Building Speed: 100% (baseline)
Material Cost:
  - Stone: 2 units (rounded head stone)
  - Wood: 1 unit (short handle)
Unlock Requirement: None

Building Properties:
  - Basic building confirmation only
  - Repair effectiveness: 50% of standard
  - Cannot upgrade buildings (only repair)
```

#### Stone Saw
```
Tool ID: tool_saw_stone
Tier: 1 (Stone/Basic)
Durability: 40 uses (lower due to fragility)
Processing Efficiency: 50% (1 log → 2 planks)
Material Cost:
  - Stone: 3 units (serrated edge)
  - Wood: 2 units (frame)
Unlock Requirement: None

Processing Properties:
  - High waste rate (50% material loss)
  - Cannot process hardwood
  - Slow processing time (2× longer)
```

#### Stone Knife
```
Tool ID: tool_knife_stone
Tier: 1 (Stone/Basic)
Durability: 60 uses (higher for delicate work)
Processing Speed: 100% (baseline)
Material Cost:
  - Stone: 1 unit (sharpened flint)
  - Wood: 1 unit (small handle)
Unlock Requirement: None

Processing Properties:
  - Basic butchering and crafting
  - 25% chance to ruin hide when skinning
  - Cannot process tough materials (thick leather, bone)
```

### 2.3 Tier 2: Iron Tools

Per `technical-constants.md`:
```csharp
public const int TOOL_DURABILITY_IRON = 150;          // 3× stone
public const float TOOL_EFFICIENCY_IRON = 1.5f;       // 50% faster
```

#### Iron Axe
```
Tool ID: tool_axe_iron
Tier: 2 (Iron)
Durability: 150 uses (3× stone tier)
Gathering Speed: 150% (50% faster than stone)
Yield Bonus: +10% wood per tree (rounding up)
Material Cost:
  - Iron ingots: 3 units
  - Wood: 2 units (hardwood handle)
Unlock Requirement: Metalworking skill level 2
Crafting Station: Forge (requires smelting knowledge)
Repairable: Yes
Repair Cost: 40% of material cost (reduced vs stone)
Repair Durability Restored: 80% (120 uses)

Visual Appearance:
  - Forged iron head with wooden handle
  - Visible hammer marks from forging
  - Quality-dependent finish (shiny to rough)

Special Properties:
  - Can harvest all wood types including hardwood
  - 10% bonus wood yield (5-9 units per tree vs 3-8)
  - Critical hit chance: 5% (double yield on that strike)
```

#### Iron Pickaxe
```
Tool ID: tool_pickaxe_iron
Tier: 2 (Iron)
Durability: 150 uses
Mining Speed: 150% (50% faster)
Material Cost:
  - Iron ingots: 3 units
  - Wood: 2 units
Unlock Requirement: Metalworking skill level 2

Mining Capabilities:
  - Can mine: Stone, coal, iron ore, copper ore
  - Cannot mine: Gold, silver, gems, bedrock
  - Ore detection: 5% chance to reveal adjacent ore

Ore Yield Bonus:
  - +15% ore yield from deposits
  - Critical strike: 5% chance for double ore
```

#### Iron Hoe
```
Tool ID: tool_hoe_iron
Tier: 2 (Iron)
Durability: 150 uses
Tilling Speed: 150%
Material Cost:
  - Iron ingots: 2 units
  - Wood: 2 units
Unlock Requirement: Metalworking skill level 2

Tilling Properties:
  - Creates quality 2-3 tilled soil
  - Can till clay-heavy soil
  - Fertility bonus: +10% to crop yield on tilled soil
```

#### Iron Sickle
```
Tool ID: tool_sickle_iron
Tier: 2 (Iron)
Durability: 150 uses
Harvest Speed: 150%
Harvest Area: 2 tiles (sweeping arc)
Material Cost:
  - Iron ingots: 2 units
  - Wood: 1 unit
Unlock Requirement: Metalworking skill level 2

Harvest Properties:
  - 2-tile area harvest (1×2 or 2×1)
  - Can harvest tough grasses and reeds
  - Yield bonus: +10% plant fiber
```

#### Iron Shovel
```
Tool ID: tool_shovel_iron
Tier: 2 (Iron)
Durability: 150 uses
Digging Speed: 150%
Material Cost:
  - Iron ingots: 2 units (broad blade)
  - Wood: 2 units
Unlock Requirement: Metalworking skill level 2

Digging Properties:
  - Can dig: All soil types including packed earth
  - Cannot dig: Stone, bedrock
  - Buried item chance: 10% (vs 5% stone)
```

#### Iron Hammer
```
Tool ID: tool_hammer_iron
Tier: 2 (Iron)
Durability: 150 uses
Building Speed: 150%
Repair Effectiveness: 150%
Material Cost:
  - Iron ingots: 3 units
  - Wood: 1 unit
Unlock Requirement: Metalworking skill level 2

Building Properties:
  - Full building functionality
  - Repair effectiveness: 150% (37.5% durability per repair)
  - Can upgrade buildings (with materials and skill)
  - Build quality bonus: +1 quality level when building
```

#### Iron Saw
```
Tool ID: tool_saw_iron
Tier: 2 (Iron)
Durability: 150 uses
Processing Efficiency: 100% (1 log → 4 planks)
Material Cost:
  - Iron ingots: 3 units (blade)
  - Wood: 2 units (frame)
Unlock Requirement: Metalworking skill level 2

Processing Properties:
  - Standard efficiency (no waste)
  - Can process softwood and medium hardwood
  - Cannot process exotic hardwoods (ebony, ironwood)
  - Processing speed: 150%
```

#### Iron Knife
```
Tool ID: tool_knife_iron
Tier: 2 (Iron)
Durability: 150 uses
Processing Speed: 150%
Material Cost:
  - Iron ingots: 1 unit
  - Wood: 1 unit
Unlock Requirement: Metalworking skill level 2

Processing Properties:
  - Clean butchering (no hide damage)
  - Can process leather and bone
  - Crafting precision: +10% quality on crafted components
```

### 2.4 Tier 3: Steel Tools

Per `technical-constants.md`:
```csharp
public const int TOOL_DURABILITY_STEEL = 500;         // 10× stone, 3.3× iron
public const float TOOL_EFFICIENCY_STEEL = 2.0f;      // 100% faster
```

#### Steel Axe
```
Tool ID: tool_axe_steel
Tier: 3 (Steel)
Durability: 500 uses (3.3× iron, 10× stone)
Gathering Speed: 200% (100% faster than stone)
Yield Bonus: +25% wood, chance of hardwood
Material Cost:
  - Steel ingots: 3 units
  - Hardwood: 2 units (reinforced handle)
Unlock Requirement: Advanced metalworking skill level 5
Crafting Station: Advanced forge (requires carbon and expertise)
Repairable: Yes
Repair Cost: 35% of material cost
Repair Durability Restored: 80% (400 uses)

Visual Appearance:
  - Polished steel head with reinforced hardwood handle
  - Quality craftsmanship visible
  - Gleaming finish when new, develops patina with use

Special Properties:
  - +25% wood yield (4-10 units per tree)
  - Hardwood bonus: 15% chance for hardwood drop
  - Critical hit chance: 10% (double yield)
  - Can fell trees in 3 strikes (vs 5-8 for lower tiers)
```

#### Steel Pickaxe
```
Tool ID: tool_pickaxe_steel
Tier: 3 (Steel)
Durability: 500 uses
Mining Speed: 200%
Material Cost:
  - Steel ingots: 3 units
  - Hardwood: 2 units
Unlock Requirement: Advanced metalworking skill level 5

Mining Capabilities:
  - Can mine: All ore types including precious metals
  - Can mine: Bedrock (for foundations, not resources)
  - Ore detection: 15% chance to reveal adjacent ore

Ore Yield Bonuses:
  - +25% ore yield from deposits
  - Rare yield chance: 8% for gems
  - Critical strike: 10% chance for double ore
  - Fortune bonus: Small chance (2%) for triple ore
```

#### Steel Hoe
```
Tool ID: tool_hoe_steel
Tier: 3 (Steel)
Durability: 500 uses
Tilling Speed: 200%
Material Cost:
  - Steel ingots: 2 units
  - Hardwood: 2 units
Unlock Requirement: Advanced metalworking skill level 5

Tilling Properties:
  - Creates quality 3-5 tilled soil
  - Auto-fertilizes with 10% compost bonus
  - Can till any soil type including rock-hard clay
  - Fertility bonus: +20% to crop yield
```

#### Steel Sickle
```
Tool ID: tool_sickle_steel
Tier: 3 (Steel)
Durability: 500 uses
Harvest Speed: 200%
Harvest Area: 3 tiles (wide sweeping arc)
Material Cost:
  - Steel ingots: 2 units
  - Hardwood: 1 unit
Unlock Requirement: Advanced metalworking skill level 5

Harvest Properties:
  - 3-tile area harvest (various shapes)
  - Can harvest all plant types including bamboo
  - Yield bonus: +20% plant fiber and seeds
  - Critical harvest: 10% chance for double yield
```

#### Steel Shovel
```
Tool ID: tool_shovel_steel
Tier: 3 (Steel)
Durability: 500 uses
Digging Speed: 200%
Material Cost:
  - Steel ingots: 2 units
  - Hardwood: 2 units
Unlock Requirement: Advanced metalworking skill level 5

Digging Properties:
  - Can dig: All terrain types
  - Buried item chance: 20% (vs 5% stone, 10% iron)
  - Fortune digging: 5% chance for rare buried cache
  - Can dig through gravel and loose stone efficiently
```

#### Steel Hammer
```
Tool ID: tool_hammer_steel
Tier: 3 (Steel)
Durability: 500 uses
Building Speed: 200%
Repair Effectiveness: 200%
Material Cost:
  - Steel ingots: 3 units
  - Hardwood: 1 unit
Unlock Requirement: Advanced metalworking skill level 5

Building Properties:
  - Expert building functionality
  - Repair effectiveness: 200% (50% durability per repair)
  - Build quality bonus: +2 quality levels when building
  - Demolition return: 75% materials (vs 50% standard)
```

#### Steel Saw
```
Tool ID: tool_saw_steel
Tier: 3 (Steel)
Durability: 500 uses
Processing Efficiency: 150% (1 log → 6 planks)
Material Cost:
  - Steel ingots: 3 units
  - Hardwood: 2 units
Unlock Requirement: Advanced metalworking skill level 5

Processing Properties:
  - High efficiency (bonus yield)
  - Can process all wood types including exotic hardwoods
  - Processing speed: 200%
  - Precision bonus: +15% quality on processed wood items
```

#### Steel Knife
```
Tool ID: tool_knife_steel
Tier: 3 (Steel)
Durability: 500 uses
Processing Speed: 200%
Material Cost:
  - Steel ingots: 1 unit
  - Hardwood: 1 unit
Unlock Requirement: Advanced metalworking skill level 5

Processing Properties:
  - Masterwork butchering (perfect hides always)
  - Can process all materials including exotic
  - Crafting precision: +20% quality on all components
  - Culinary bonus: +10% food value when preparing meals
```

### 2.5 Tier 4: Advanced Tools (Future Expansion)

#### Electric/Powered Tools
```
Tool Category: Electric/Powered
Planned For: Post-MVP expansion
Requirements:
  - Power source (battery or electrical grid)
  - Electronics skill level 3+
  - Advanced materials (copper, circuits, motors)

Speed: 300-500% (3-5× stone tier)
Durability: 1000+ uses
Maintenance:
  - Low durability cost per use (0.5 per use)
  - High power/battery consumption
  - Requires periodic maintenance (repair skill)

Examples (Future):
  - Chainsaw (axe replacement): 400% speed, 1500 durability
  - Jackhammer (pickaxe replacement): 400% speed, 1200 durability
  - Rotary tiller (hoe replacement): 350% speed, 1000 durability
  - Powered sawmill (saw replacement): 500% speed, 2000 durability
```

#### Diamond-Tipped Tools
```
Tool Category: Diamond-Tipped (Ultimate Tier)
Planned For: Late-game/Endgame content
Requirements:
  - Legendary blacksmith skill
  - Rare diamond resource
  - Masterwork crafting station

Speed: 400% (4× stone tier)
Durability: 2000+ uses
Special Properties:
  - Can harvest/collect any material in game
  - No failure chance on any operation
  - 50% bonus yield on all operations
  - Auto-collect feature (items go directly to inventory)
  - Indestructible by normal use (only repairable)

Visual Appearance:
  - Glistening diamond edges
  - Ornate masterwork design
  - Unique particle effects when used
  - Status symbol in multiplayer
```

---

## 3. Durability System

### 3.1 Base Mechanics

Per `technical-constants.md` Section 9:
```csharp
// Tool uses before breaking
public const int TOOL_DURABILITY_STONE = 50;
public const int TOOL_DURABILITY_IRON = 150;
public const int TOOL_DURABILITY_STEEL = 500;

// Tool repair
public const float TOOL_REPAIR_COST_PERCENT = 50.0f;
public const int TOOL_REPAIR_DURABILITY_RESTORED = 80;
```

### 3.2 Durability Consumption

#### Standard Use
```
Durability Cost Per Use:
  - Successful use: -1 durability
  - Tool animation completes
  - Target is appropriate for tool type

Example:
  Stone axe at 50/50 durability
  Chop tree (successful): 49/50 durability
  Chop tree (successful): 48/50 durability
```

#### Critical Failures
```
Durability Cost - Critical Failures:
  - Wrong material type: -5 durability
  - Tool breakage risk: 10% at 0 durability
  - Catastrophic failure: -10 durability (rare, 1% chance on use)

Wrong Material Examples:
  - Axe on stone: -5 durability, no yield
  - Pickaxe on wood: -5 durability, no yield
  - Hoe on stone: -5 durability, no yield

Visual/Audio Feedback:
  - Wrong material: Tool bounces, dull thud sound
  - Catastrophic failure: Tool cracks, loud snap sound
```

#### Skill-Based Durability Preservation
```
Per `technical-constants.md` Section 6 (Skills):

Gathering Skill Impact (affects gathering tools):
  Level 1: -10% durability loss (0.9 per use)
  Level 3: -20% durability loss (0.8 per use)
  Level 5: -30% durability loss (0.7 per use)
  Level 7: -40% durability loss (0.6 per use)
  Level 10: -50% durability loss (0.5 per use)

Crafting Skill Impact (affects utility tools):
  Level 1: -5% durability loss when building/crafting
  Level 3: -10% durability loss
  Level 5: -15% durability loss
  Level 7: -25% durability loss
  Level 10: -35% durability loss

Example - Master Gatherer (Level 10) with Stone Axe:
  Normal use: 0.5 durability per chop
  Stone axe effective durability: 50 ÷ 0.5 = 100 uses
```

### 3.3 Durability States

#### State Thresholds
```
Durability States (percentage-based):

Perfect (100%):
  - Range: 100% durability
  - Visual: Pristine, shiny, no wear
  - Effectiveness: 100%
  - Audio: Full-quality tool sounds

Good (70-99%):
  - Range: 70-99% durability remaining
  - Visual: Minor wear, slight discoloration
  - Effectiveness: 100%
  - Audio: Normal tool sounds

Worn (30-69%):
  - Range: 30-69% durability remaining
  - Visual: Noticeable wear, chips, scratches
  - Effectiveness: 95% (slight reduction)
  - Audio: Slightly duller sounds
  - Warning: UI indicator turns yellow
  - Tooltip: "Tool is showing signs of wear"

Damaged (10-29%):
  - Range: 10-29% durability remaining
  - Visual: Significant damage, cracks, bent edges
  - Effectiveness: 80% (20% reduction)
  - Audio: Rattling, loose sounds
  - Warning: UI indicator turns orange
  - Tooltip: "Tool requires repair soon"
  - Critical warning at 25%: "Tool will break soon!"

Broken (0-9%):
  - Range: 0-9% durability remaining
  - Visual: Severely damaged, barely functional
  - Effectiveness: 50% (50% reduction)
  - Audio: Grinding, scraping sounds
  - Warning: UI indicator turns red
  - Tooltip: "Tool is nearly broken! Repair immediately!"
  - At 0%: Tool unusable until repaired

Color Coding (UI):
  - Perfect/Good: Green (#00FF00)
  - Worn: Yellow (#FFFF00)
  - Damaged: Orange (#FFA500)
  - Broken: Red (#FF0000)
```

#### Visual Wear System
```
Visual Wear Implementation:

Each tool has 5 visual states:
  1. Pristine (100%): Full detail, clean textures
  2. Slight wear (70-99%): Minor scratches, slight darkening
  3. Moderate wear (30-69%): Visible chips, texture degradation
  4. Heavy wear (10-29%): Cracks, bent edges, missing pieces
  5. Broken (0-9%): Severe damage, non-functional appearance

Transition Points:
  - 99% → 70%: Gradual scratch accumulation
  - 69% → 30%: Chip geometry appears
  - 29% → 10%: Crack decals activated
  - 9% → 0%: Full damage state, particle effects on use

Per-Tool Wear Patterns:
  - Axe: Edge chips, handle wear
  - Pickaxe: Head dulling, shaft splintering
  - Hoe: Blade dulling, handle loosening
  - Sickle: Blade nicks, curve deformation
  - Shovel: Blade bending, edge rolling
  - Hammer: Head mushrooming, handle cracking
  - Saw: Tooth damage, frame warping
  - Knife: Edge rolling, tip damage
```

### 3.4 Repair System

#### Repair Cost Formula
```
Per `technical-constants.md`:
public const float TOOL_REPAIR_COST_PERCENT = 50.0f;
public const int TOOL_REPAIR_DURABILITY_RESTORED = 80;

Base Repair Formula:
  MissingDurability = MaxDurability - CurrentDurability
  RepairPercentage = MissingDurability / MaxDurability
  BaseMaterialCost = Original tool material cost
  RepairCostMultiplier = TOOL_REPAIR_COST_PERCENT / 100 = 0.5
  MaterialsNeeded = ceil(RepairPercentage × BaseMaterialCost × RepairCostMultiplier)

Durability Restored:
  FixedAmount = MaxDurability × (TOOL_REPAIR_DURABILITY_RESTORED / 100)
  For stone: 50 × 0.8 = 40 uses restored
  For iron: 150 × 0.8 = 120 uses restored
  For steel: 500 × 0.8 = 400 uses restored

Example 1 - Repairing Iron Axe:
  Current durability: 50/150 (missing 100)
  Max durability: 150
  Repair percentage: 100/150 = 66.7%
  Base material cost: 3 iron + 2 wood
  Repair cost multiplier: 0.5
  Materials needed: ceil(0.667 × 0.5 × cost) = ceil(0.333 × cost)
  Actual: 1 iron ingot + 1 wood
  Durability restored: 120 (to 170, capped at 150)
  Result: 150/150 (full repair, slight waste)

Example 2 - Repairing Steel Pickaxe:
  Current durability: 200/500 (missing 300)
  Repair percentage: 300/500 = 60%
  Materials needed: ceil(0.6 × 0.5 × 3 steel + 2 wood) = 1 steel + 1 wood
  But wait: Using 35% cost for steel tier (tier bonus)
  Actual: ceil(0.6 × 0.35 × cost) = 1 steel + 1 wood
  Durability restored: 400
  Result: 500/500 (full repair to max)
```

#### Repair Locations
```
Repair Station Tiers:

Self-Repair (Field Repair):
  - Requires: Repair kit item (craftable)
  - Efficiency: 70% durability restored (vs 80% standard)
  - Material cost: Same as standard repair
  - Time: 10 seconds animation
  - Limitations: Cannot repair advanced tiers (steel+)
  - Portable: Can be done anywhere
  
Workbench Repair:
  - Requires: Basic workbench
  - Efficiency: 80% durability restored (standard)
  - Material cost: Standard (50-35% depending on tier)
  - Time: 5 seconds animation
  - Limitations: Basic repairs only
  - Location: Fixed structure

Smithy/Forge Repair:
  - Requires: Forge or smithy building
  - Efficiency: 90% durability restored (bonus)
  - Material cost: 80% of standard (efficient)
  - Time: 3 seconds animation
  - Bonus: Quality restoration possible (removes quality degradation)
  - Requires: Metalworking skill level 2+

Professional NPC Repair:
  - Requires: Blacksmith NPC service
  - Efficiency: 100% durability restored (full)
  - Material cost: 120% of standard (labor cost)
  - Time: Instant (NPC handles it)
  - Bonus: Can add quality improvements
  - Cost: Credits + materials
```

#### Repair Quality
```
Repair Quality Levels:

Amateur Repair (Self, low skill):
  - Durability restored: 70% of max
  - Permanent degradation: 10% max durability loss
  - Visual: Visible repair patches
  - Example: Stone axe repaired by unskilled user
    Max drops from 50 → 45 permanently
    Each repair further reduces max by 10%

Standard Repair (Workbench, medium skill):
  - Durability restored: 80% of max
  - Permanent degradation: 5% max durability loss
  - Visual: Subtle repair marks
  - Balanced option for most players

Professional Repair (Forge, high skill):
  - Durability restored: 90% of max
  - Permanent degradation: 2% max durability loss
  - Visual: Nearly invisible repairs
  - Best for valuable tools

Masterwork Repair (Legendary blacksmith):
  - Durability restored: 100% of max
  - Permanent degradation: 0% (no loss)
  - Visual: Perfect restoration
  - Bonus: Can improve tool quality by 5-10 points
  - Rare and expensive

Quality Degradation Cap:
  - Tools cannot lose more than 50% max durability from repairs
  - At 50% max, tool becomes "heirloom" status
  - Heirloom tools have sentimental value but reduced functionality
  - Eventually must be replaced
```

---

## 4. Tool Quality System

### 4.1 Quality Levels

Per `technical-constants.md` Section 11:
```csharp
// Item quality levels
public const int QUALITY_POOR_MIN = 0;
public const int QUALITY_POOR_MAX = 25;
public const int QUALITY_NORMAL_MIN = 26;
public const int QUALITY_NORMAL_MAX = 50;
public const int QUALITY_GOOD_MIN = 51;
public const int QUALITY_GOOD_MAX = 75;
public const int QUALITY_EXCELLENT_MIN = 76;
public const int QUALITY_EXCELLENT_MAX = 95;
public const int QUALITY_MASTERWORK_MIN = 96;
public const int QUALITY_MASTERWORK_MAX = 100;

// Quality modifiers
public const float QUALITY_MULTIPLIER_POOR = 0.70f;
public const float QUALITY_MULTIPLIER_NORMAL = 1.00f;
public const float QUALITY_MULTIPLIER_GOOD = 1.15f;
public const float QUALITY_MULTIPLIER_EXCELLENT = 1.30f;
public const float QUALITY_MULTIPLIER_MASTERWORK = 1.50f;
```

#### Quality Tier Definitions
```
Quality Tier 1: Poor (0-25 quality)
  - Visual: Rough, crude construction
  - Durability: -30% (multiplier 0.70)
  - Speed: -10% (slower use)
  - Yield: -15% (reduced resources)
  - Visual markers: Misaligned parts, rough edges, visible flaws
  - Tooltip: "Poor quality - roughly made"
  - Market value: 50% of base price

Quality Tier 2: Normal (26-50 quality)
  - Visual: Standard construction
  - Durability: Standard (multiplier 1.00)
  - Speed: Standard
  - Yield: Standard
  - Visual markers: Clean construction, no major flaws
  - Tooltip: "Normal quality - standard make"
  - Market value: 100% of base price

Quality Tier 3: Good (51-75 quality)
  - Visual: Well-crafted appearance
  - Durability: +15% (multiplier 1.15)
  - Speed: +5% (slightly faster)
  - Yield: +5% (bonus resources)
  - Visual markers: Even lines, polished surfaces, balanced proportions
  - Tooltip: "Good quality - well-crafted"
  - Market value: 120% of base price

Quality Tier 4: Excellent (76-95 quality)
  - Visual: Fine craftsmanship
  - Durability: +30% (multiplier 1.30)
  - Speed: +10% (noticeably faster)
  - Yield: +15% (significant bonus)
  - Visual markers: Detailed work, perfect alignment, quality materials visible
  - Tooltip: "Excellent quality - finely made"
  - Market value: 150% of base price

Quality Tier 5: Masterwork (96-100 quality)
  - Visual: Master craftsman piece
  - Durability: +50% (multiplier 1.50)
  - Speed: +15% (expert handling)
  - Yield: +25% (exceptional bonus)
  - Special: Unique visual design, possible bonus trait
  - Visual markers: Ornate details, perfect balance, glowing quality sheen
  - Tooltip: "Masterwork - a masterpiece of craftsmanship"
  - Market value: 200% of base price (collector's item)
  - Rare: Only 1% of crafted tools reach this tier
```

#### Quality Impact Calculations
```
Effective Durability Calculation:
  BaseDurability = TOOL_DURABILITY_TIER
  QualityMultiplier = QUALITY_MULTIPLIER_TIER
  EffectiveDurability = BaseDurability × QualityMultiplier

Example - Iron Axe by Quality:
  Poor (25): 150 × 0.70 = 105 uses
  Normal (40): 150 × 1.00 = 150 uses
  Good (60): 150 × 1.15 = 173 uses
  Excellent (85): 150 × 1.30 = 195 uses
  Masterwork (98): 150 × 1.50 = 225 uses

Speed Calculation:
  BaseSpeed = TOOL_EFFICIENCY_TIER
  QualitySpeedBonus = QualityTierBonus
  EffectiveSpeed = BaseSpeed × (1 + QualitySpeedBonus)

Example - Steel Pickaxe by Quality:
  Base speed: 200%
  Poor: 200% × 0.90 = 180% (10% slower)
  Normal: 200% × 1.00 = 200%
  Good: 200% × 1.05 = 210%
  Excellent: 200% × 1.10 = 220%
  Masterwork: 200% × 1.15 = 230%

Yield Calculation:
  BaseYield = Standard yield amount
  QualityYieldBonus = QualityTierBonus
  EffectiveYield = BaseYield × (1 + QualityYieldBonus)

Example - Wood gathering with Steel Axe:
  Base yield: 4-10 wood (avg 7)
  Poor: 7 × 0.85 = 5.95 → 6 wood
  Normal: 7 × 1.00 = 7 wood
  Good: 7 × 1.05 = 7.35 → 7-8 wood
  Excellent: 7 × 1.15 = 8.05 → 8 wood
  Masterwork: 7 × 1.25 = 8.75 → 9 wood
```

### 4.2 Quality Determinants

#### Quality Calculation Formula
```
Base Quality Formula:
  CrafterSkillBonus = CrafterSkillLevel × 2 (max 20 at level 10)
  ToolQualityBonus = ToolQualityUsedToCraft × 0.05 (max 5 points)
  WorkstationQualityBonus = WorkstationTier × 2 (max 10 points)
  MaterialQualityBonus = MaterialQualityAverage × 0.05 (max 5 points)
  RandomVariance = Random.Range(-5, +5)
  
  BaseQuality = 20 + CrafterSkillBonus + ToolQualityBonus + 
                WorkstationQualityBonus + MaterialQualityBonus + 
                RandomVariance
  
  FinalQuality = Clamp(BaseQuality, 0, 100)

Max Theoretical Quality:
  Base: 20
  Skill (level 10): +20
  Masterwork tool: +5
  Masterwork station: +10
  Excellent materials: +5
  Max random: +5
  Total: 65 + 5 = 70 (without variance)
  With max variance: 75
  
  To reach masterwork (96-100):
  - Requires special materials (rare metals, gems)
  - Requires legendary skill (level 10 + specialization)
  - Requires inspiration buff or special conditions
  - Masterwork chance: ~1% at max skill

Example Calculations:

Example 1 - Novice Crafter (Level 1):
  Skill bonus: 1 × 2 = 2
  Tool quality: Poor stone hammer = 1
  Workstation: Basic = 2
  Materials: Normal = 2.5
  Random: 0 (average)
  Base: 20 + 2 + 1 + 2 + 2.5 + 0 = 27.5
  Result: Normal quality (26-50)

Example 2 - Expert Crafter (Level 7):
  Skill bonus: 7 × 2 = 14
  Tool quality: Good iron hammer = 3
  Workstation: Advanced = 6
  Materials: Good = 3.75
  Random: +3
  Base: 20 + 14 + 3 + 6 + 3.75 + 3 = 49.75
  Result: High Normal / Low Good (50 border)

Example 3 - Master Crafter (Level 10):
  Skill bonus: 10 × 2 = 20
  Tool quality: Excellent steel hammer = 4.75
  Workstation: Masterwork = 10
  Materials: Excellent = 4.75
  Random: +5
  Base: 20 + 20 + 4.75 + 10 + 4.75 + 5 = 64.5
  Result: Good quality (51-75)
  With legendary materials and buff: Could reach 75-85 (Excellent)
```

#### Material Quality
```
Material Quality Tiers:

Poor Materials (0-20 quality):
  - Source: Low-skill gathering, depleted sources
  - Examples: Rotten wood, impure ore, contaminated soil
  - Bonus: 0-1 quality points

Normal Materials (21-50 quality):
  - Source: Standard gathering
  - Examples: Standard wood, common ore, average soil
  - Bonus: 2-2.5 quality points

Good Materials (51-75 quality):
  - Source: Skilled gathering, premium sources
  - Examples: Seasoned wood, pure ore, rich soil
  - Bonus: 3-3.75 quality points

Excellent Materials (76-95 quality):
  - Source: Expert gathering, rare sources
  - Examples: Aged hardwood, refined ingots, fertile loam
  - Bonus: 4-4.75 quality points

Legendary Materials (96-100 quality):
  - Source: Master gathering, legendary sources
  - Examples: Ancient heartwood, flawless gems, blessed earth
  - Bonus: 5 quality points
  - Special: May add unique properties to crafted tool
```

#### Workstation Quality
```
Workstation Quality Tiers:

Basic Workstation (Tier 1):
  - Quality bonus: +2 points
  - Examples: Makeshift bench, outdoor workspace
  - Limitation: Cannot craft above Good quality (51-75)

Standard Workstation (Tier 2):
  - Quality bonus: +4 points
  - Examples: Crafting bench, basic forge
  - Limitation: Cannot craft above Excellent quality (76-95)

Advanced Workstation (Tier 3):
  - Quality bonus: +6 points
  - Examples: Advanced forge, carpenter's workshop
  - Capable of all quality tiers (with sufficient skill)

Masterwork Workstation (Tier 4):
  - Quality bonus: +10 points
  - Examples: Legendary forge, master craftsman's shop
  - Special: +5% masterwork chance bonus
  - Unique: Can add decorative elements without cost
```

---

## 5. Tool Specialization & Modifications

### 5.1 Tool Attachments (Post-MVP Feature)

#### Attachment System Overview
```
Planned for: Post-MVP update
System type: Modular tool upgrades
Attachment slots: 1-3 per tool (depends on tool type)
Attachment categories:
  - Efficiency mods (speed, yield)
  - Durability mods (longevity, repair)
  - Special ability mods (detection, automation)
  - Visual mods (appearance, effects)
```

#### Axe Modifications
```
Attachment 1: Sharpener Kit
  - Effect: +10% gathering speed
  - Trade-off: +5% durability consumption
  - Visual: Gleaming edge
  - Crafting: 1 whetstone + 1 oil

Attachment 2: Counterweight
  - Effect: -20% stamina cost per swing
  - Trade-off: -5% speed
  - Visual: Weight on handle end
  - Crafting: 1 metal ingot + 1 leather

Attachment 3: Precision Edge
  - Effect: +15% yield bonus
  - Trade-off: Requires maintenance (sharpening every 20 uses)
  - Visual: Unusually sharp edge geometry
  - Crafting: 2 steel ingots + 1 diamond dust

Attachment 4: Splitting Wedge
  - Effect: 25% chance to split logs automatically (bonus planks)
  - Trade-off: Cannot use on living trees (only fallen logs)
  - Visual: Modified axe head geometry
  - Crafting: 3 steel ingots
```

#### Pickaxe Modifications
```
Attachment 1: Reinforced Head
  - Effect: +25% durability
  - Trade-off: +10% weight (slower movement when equipped)
  - Visual: Thicker, reinforced head
  - Crafting: 2 steel ingots + 1 carbon

Attachment 2: Prospector's Lens
  - Effect: Highlights ore veins within 10m (pulse every 5 seconds)
  - Trade-off: -5% mining speed
  - Visual: Small lens attachment on handle
  - Crafting: 1 glass + 1 rare crystal

Attachment 3: Auto-Collector
  - Effect: Mined items go directly to inventory (no ground drop)
  - Trade-off: Requires power (battery consumption)
  - Visual: Small vacuum mechanism near head
  - Crafting: 2 copper + 1 motor + 1 circuit board

Attachment 4: Vibrating Head
  - Effect: +20% mining speed on soft stone
  - Trade-off: +15% durability loss on hard stone
  - Visual: Mechanism at head-base connection
  - Crafting: 1 motor + 1 battery + 2 gears
```

#### Universal Attachments
```
Grip Wrap (All tools):
  - Effect: +10% grip (reduces slip chance in rain/wet)
  - Visual: Wrapped handle
  - Crafting: 1 leather or 2 plant fiber

Lanyard (All tools):
  - Effect: Tool returns to inventory if dropped (1 minute cooldown)
  - Visual: Cord attached to tool and player
  - Crafting: 1 rope + 1 metal clip

Tracer Module (All tools - requires electronics):
  - Effect: Tool location shown on map if lost
  - Visual: Small glowing module on tool
  - Crafting: 1 circuit board + 1 battery + 1 antenna

Cosmetic Engraving (All tools):
  - Effect: Visual only - custom appearance
  - Visual: Custom patterns/text on tool
  - Crafting: 1 chisel + creative input
```

### 5.2 Tool Specialization Paths

#### Gathering Specialization
```
Path: Master Gatherer
Unlock: Gathering skill level 5+
Benefits:
  - Tool effectiveness: +10% when gathering primary resource
  - Stamina efficiency: -15% cost when gathering
  - Yield bonus: +20% from primary gathering tool type
  
Specialization Selection (choose one at level 5):
  1. Lumberjack (Axe specialization)
     - Tree felling: 30% faster
     - Wood yield: +25%
     - Special: Can climb trees for rare resources
     
  2. Miner (Pickaxe specialization)
     - Mining speed: +25%
     - Ore detection: +30% range
     - Special: Can place temporary lighting in mines
     
  3. Farmer (Hoe/Sickle specialization)
     - Tilling speed: +30%
     - Crop yield: +20%
     - Special: Can identify soil quality at a glance
     
  4. Excavator (Shovel specialization)
     - Digging speed: +25%
     - Buried item chance: +50%
     - Special: Can sense buried objects within 5m
```

#### Crafting Specialization
```
Path: Master Craftsman
Unlock: Crafting skill level 5+
Benefits:
  - Tool crafting quality: +15 base quality points
  - Repair efficiency: +20%
  - Material efficiency: -10% material cost

Specialization Selection (choose one at level 5):
  1. Blacksmith (Hammer specialization)
     - Building speed: +30%
     - Building durability: +20%
     - Special: Can reinforce buildings (extra durability)
     
  2. Carpenter (Saw specialization)
     - Processing speed: +30%
     - Wood yield: +25%
     - Special: Can create custom wood patterns
     
  3. Artisan (Knife specialization)
     - Crafting precision: +25% quality
     - Material waste: -20%
     - Special: Can add decorative elements to items
```

---

## 6. Tool Switching & UI

### 6.1 Equipping Tools

#### Input Methods
```
Method 1: Hotbar Selection
  - Default hotbar slots: 1-9
  - Tool assignment: Drag tool to hotbar slot
  - Activation: Press corresponding number key (1-9)
  - Visual: Hotbar highlights selected slot
  - Audio: Tool-specific equip sound

Method 2: Inventory Selection
  - Open inventory: Tab or I key
  - Click tool to equip
  - Right-click for context menu (Equip, Drop, Repair)
  - Visual: Tool moves to "equipped" slot

Method 3: Auto-Equip (Contextual)
  - Trigger: Look at valid target for extended period (2 seconds)
  - System suggests optimal tool via UI popup
  - Accept suggestion: Press suggested hotkey or click popup
  - Cancel: Move away or press escape
  - Example: Looking at tree suggests axe, shows "Press 1 to equip Axe"

Method 4: Quick-Swap
  - Hold tool key: Shows radial menu of available tools
  - Mouse/stick direction: Select tool
  - Release: Equip selected tool
  - Visual: Radial menu with tool icons
```

#### Equip Animation Sequence
```
Unequip Current Tool:
  Duration: 0.3 seconds
  Animation:
    1. Tool lowers from ready position (0.1s)
    2. Tool moves to unequip position (0.1s)
    3. Tool disappears/slots into inventory (0.1s)
  Audio: Tool-specific put-away sound

Equip New Tool:
  Duration: 0.3 seconds
  Animation:
    1. Tool appears in equip position (0.1s)
    2. Tool raises to ready position (0.1s)
    3. Tool settles into idle sway (0.1s)
  Audio: Tool-specific draw sound

Total Switch Time: 0.6 seconds
Interruptible: Yes (can cancel mid-switch)
Stamina cost: 0 (no cost for switching)

Per-Tool Audio:
  - Axe: Slide sound (metal on leather/wood)
  - Pickaxe: Clang sound (metal impact)
  - Hoe: Thud sound (blade movement)
  - Sickle: Swish sound (blade cutting air)
  - Shovel: Slide sound (shaft movement)
  - Hammer: Thud sound (weighty tool)
  - Saw: Click sound (blade lock)
  - Knife: Shing sound (blade draw)
```

### 6.2 Tool Display

#### First-Person View
```
Tool Model Visibility:
  - Position: Lower right of screen (held in right hand)
  - Visible portion: 30-40% of tool (handle and partial head)
  - Idle animation: Subtle sway (breathing motion)
  - Movement animation: Bounce with footsteps

Idle Animations:
  - Axe: Gentle sway, blade catches light
  - Pickaxe: Slight head bob
  - Hoe: Blade tilts slightly
  - Sickle: Curved blade rotates subtly
  - Shovel: Shaft sways
  - Hammer: Head tilts
  - Saw: Blade glints
  - Knife: Blade reflects light

Use Animations:
  - Full tool motion visible
  - Camera shake on impact (subtle)
  - Particle effects on hit (chips, dust)
  - Screen effects: Brief motion blur on fast swings

Durability Visuals (First-Person):
  - 70-100%: Pristine, no visible wear
  - 30-69%: Minor scratches on visible parts
  - 10-29%: Cracks and damage visible
  - 0-9%: Severely damaged, loose parts

Durability Indicator:
  - Position: Bottom of tool model or corner of screen
  - Style: Horizontal bar
  - Colors: Green → Yellow → Orange → Red
  - Show on: Tool use, damage taken, or key hold
```

#### Third-Person View
```
Unequipped State:
  - Position: Tool sheathed on character back
  - Axe/Pickaxe/Sickle: Slung across back
  - Hoe/Shovel: Vertical on back
  - Hammer: Loop on belt
  - Saw: Horizontal on back
  - Knife: Sheath on belt/leg
  - Visible: Full tool model

Equipped State:
  - Position: Held in right hand
  - Idle: Tool at rest position
  - Animation: Idle sway synchronized with character

Other Player Visibility:
  - Always see equipped tool in hand
  - See tool usage animations clearly
  - Durability state visible (rough vs polished)
  - Quality visible (subtle glow for masterwork)

Multiplayer Tool Display:
  - Tool changes network synchronized
  - Usage animations played for all viewers
  - Durability changes not synced (client-only visual)
  - Quality visible to all (status symbol)
```

#### UI Elements
```
Hotbar Display:
  - 9 slots at bottom of screen
  - Tool icons show durability mini-bar
  - Quality border: None (Normal), Silver (Good), Gold (Excellent), 
    Rainbow shimmer (Masterwork)
  - Durability warning: Icon flashes red when <10%
  - Empty slot: Press number to suggest tools from inventory

Inventory Tool Display:
  - Grid view with tool icons
  - Tooltip on hover:
    - Tool name
    - Quality tier (color-coded)
    - Durability: "45/150 uses"
    - Durability state: "Good condition"
    - Special properties list
    - Estimated market value
    - Crafter name (if not player-made)

Tool Comparison:
  - Hover over tool in inventory
  - Press comparison key (default: Shift)
  - Side-by-side stats with equipped tool
  - Green/red arrows for better/worse stats
  - Shows effective durability with quality bonus

Radial Menu (Quick-Swap):
  - Center: Currently equipped tool (large icon)
  - Surrounding: Available tools by type
  - Organization: Clockwise by tool type
    - Top: Axe (12 o'clock)
    - Right: Pickaxe, Hoe (2, 4 o'clock)
    - Bottom: Sickle, Shovel (6, 8 o'clock)
    - Left: Hammer, Saw, Knife (10 o'clock)
  - Tool condition shown via icon color
  - Quality shown via border effect
```

---

## 7. Tool Skills Integration

### 7.1 Gathering Skill Effects

Per `technical-constants.md` Section 6:
```csharp
public const int SKILL_LEVELS_COUNT = 10;
public const float SKILL_BONUS_PER_LEVEL_PERCENT = 5.0f;
```

#### Gathering Skill Progression
```
Level 0: Unskilled
  - No bonuses
  - 100% durability consumption
  - Standard yield

Level 1: Beginner (100 XP)
  - Speed bonus: +5%
  - XP to next: 200

Level 2: Apprentice (300 XP total)
  - Speed bonus: +10%
  - Durability preservation: -10% loss
  - XP to next: 400

Level 3: Journeyman (700 XP total)
  - Speed bonus: +15%
  - Durability preservation: -20% loss
  - Yield bonus: +5%
  - XP to next: 800

Level 4: Competent (1500 XP total)
  - Speed bonus: +20%
  - Durability preservation: -25% loss
  - XP to next: 1600

Level 5: Skilled (3100 XP total)
  - Speed bonus: +25%
  - Durability preservation: -30% loss
  - Yield bonus: +10%
  - Specialization unlock
  - XP to next: 3200

Level 6: Proficient (6300 XP total)
  - Speed bonus: +30%
  - Durability preservation: -35% loss
  - XP to next: 6400

Level 7: Expert (12700 XP total)
  - Speed bonus: +35%
  - Durability preservation: -40% loss
  - Yield bonus: +15%
  - XP to next: 12800

Level 8: Advanced (25500 XP total)
  - Speed bonus: +40%
  - Durability preservation: -45% loss
  - XP to next: 25600

Level 9: Master (51100 XP total)
  - Speed bonus: +45%
  - Durability preservation: -48% loss
  - Yield bonus: +20%
  - XP to next: 51200

Level 10: Legendary (102300 XP total)
  - Speed bonus: +50%
  - Durability preservation: -50% loss
  - Yield bonus: +25%
  - Masterwork crafting chance: +5%
  - Title: "Legendary Gatherer"

XP Rewards per Action:
  Basic resource gathering: 5 XP
  Advanced resource gathering: 15 XP
  Rare resource discovery: 25 XP
  Tool crafting (if tool used): 10 XP
```

#### Skill Effects by Tool Type
```
Axe Skill Bonuses:
  - Tree felling speed: +5% per level (max +50%)
  - Wood yield: +2% per level (max +20% at level 10)
  - Special unlocks:
    - Level 3: Can harvest hardwood
    - Level 5: 10% chance for double wood on crit
    - Level 7: Can identify tree age (affects yield)
    - Level 10: Can harvest from standing position (no climb needed)

Pickaxe Skill Bonuses:
  - Mining speed: +5% per level (max +50%)
  - Ore detection: +2m range per level (max +20m)
  - Special unlocks:
    - Level 3: Can mine iron tier ores efficiently
    - Level 5: 15% ore yield bonus
    - Level 7: Can identify ore deposits by sight
    - Level 10: Can mine 2 blocks at once (area mining)

Hoe Skill Bonuses:
  - Tilling speed: +5% per level (max +50%)
  - Soil quality bonus: +0.5 per level (max +5 quality)
  - Special unlocks:
    - Level 3: Can till clay soil
    - Level 5: Auto-fertilize option (compost bonus)
    - Level 7: Can create raised beds (better drainage)
    - Level 10: Instant tilling (no animation)

Sickle Skill Bonuses:
  - Harvest speed: +5% per level (max +50%)
  - Harvest area: +1 tile per 3 levels (max +3 tiles at level 9)
  - Special unlocks:
    - Level 3: Can harvest tough plants
    - Level 5: Seed yield +20%
    - Level 7: Can harvest without damaging regrowth
    - Level 10: Harvested plants auto-replant (if seeds available)

Shovel Skill Bonuses:
  - Digging speed: +5% per level (max +50%)
  - Buried item detection: +5% chance per level (max +50%)
  - Special unlocks:
    - Level 3: Can dig packed earth
    - Level 5: Can dig 2× deeper per action
    - Level 7: Buried cache detection range +10m
    - Level 10: Can dig underwater (suction removal)
```

### 7.2 Crafting Skill Effects

Per `technical-constants.md` Section 6:
```csharp
public const float SKILL_BONUS_PER_LEVEL_PERCENT = 5.0f;
```

#### Crafting Skill Progression
```
Level 0-10 Progression:
  Same XP thresholds as Gathering skill (100, 300, 700, etc.)
  Total for max level: 102,300 XP

Crafting XP Rewards:
  Simple tool crafting: 10 XP
  Complex tool crafting: 25 XP
  Tool repair: 5 XP
  Masterwork creation: 100 XP bonus

Quality Range by Level:
  Level 1: Base quality 20-30 (Poor to low Normal)
  Level 3: Base quality 30-40 (Normal)
  Level 5: Base quality 40-50 (Normal to low Good)
  Level 7: Base quality 50-60 (Good)
  Level 10: Base quality 60-70 (Good to low Excellent)
  
  With max bonuses: Can reach 80-90 (Excellent)
  Masterwork (96-100): Requires special conditions + ~1% chance
```

#### Crafting Skill Tool Effects
```
Hammer Skill Bonuses:
  - Building speed: +5% per level (max +50%)
  - Building durability: +3% per level (max +30%)
  - Repair efficiency: +5% per level (max +50%)
  - Special unlocks:
    - Level 3: Can upgrade buildings
    - Level 5: 20% material return on demolition
    - Level 7: Can build reinforced structures
    - Level 10: Instant building placement

Saw Skill Bonuses:
  - Processing speed: +5% per level (max +50%)
  - Material efficiency: +3% per level (max +30% yield)
  - Special unlocks:
    - Level 3: Can process medium hardwood
    - Level 5: 2× processing (batch processing)
    - Level 7: Can create composite materials
    - Level 10: Zero waste processing

Knife Skill Bonuses:
  - Crafting quality: +3 base points per level (max +30)
  - Processing speed: +5% per level (max +50%)
  - Special unlocks:
    - Level 3: Can process leather
    - Level 5: Can carve intricate designs
    - Level 7: Can work bone and horn
    - Level 10: Can craft masterwork components
```

---

## 8. Tool Economy

### 8.1 Base Tool Values

Per `technical-constants.md` Section 4:
```csharp
// Price ranges (Day 7 economy - established)
public const float PRICE_DAY7_TOOLS_MIN = 30.0f;
public const float PRICE_DAY7_TOOLS_MAX = 80.0f;
```

#### Tool Base Prices
```
Stone Tools (Day 7 Economy):
  Base price range: 20-40 credits
  
  Stone Axe: 25-35 Cr
  Stone Pickaxe: 25-35 Cr
  Stone Hoe: 20-30 Cr
  Stone Sickle: 20-30 Cr
  Stone Shovel: 20-30 Cr
  Stone Hammer: 30-40 Cr
  Stone Saw: 25-35 Cr
  Stone Knife: 20-30 Cr

Iron Tools (Day 7 Economy):
  Base price range: 80-150 credits
  
  Iron Axe: 100-140 Cr
  Iron Pickaxe: 100-140 Cr
  Iron Hoe: 80-120 Cr
  Iron Sickle: 80-120 Cr
  Iron Shovel: 80-120 Cr
  Iron Hammer: 120-150 Cr
  Iron Saw: 100-140 Cr
  Iron Knife: 90-130 Cr

Steel Tools (Day 7 Economy):
  Base price range: 200-400 credits
  
  Steel Axe: 250-350 Cr
  Steel Pickaxe: 250-350 Cr
  Steel Hoe: 200-280 Cr
  Steel Sickle: 200-280 Cr
  Steel Shovel: 200-280 Cr
  Steel Hammer: 300-400 Cr
  Steel Saw: 250-350 Cr
  Steel Knife: 220-320 Cr

Price Factors:
  - Tool tier: +300% per tier (stone → iron → steel)
  - Tool type: Utility tools 20% more expensive (hammer, saw)
  - Material costs reflected in pricing
  - Time investment included (XP equivalent)
```

### 8.2 Price Modifiers

#### Quality Multipliers
```
Per `technical-constants.md` Section 11:

Quality Price Multipliers:
  Poor (0-25): 0.60× base price (-40%)
  Normal (26-50): 1.00× base price (standard)
  Good (51-75): 1.25× base price (+25%)
  Excellent (76-95): 1.60× base price (+60%)
  Masterwork (96-100): 2.50× base price (+150%)

Example - Iron Axe Pricing:
  Base price: 120 Cr
  Poor: 120 × 0.60 = 72 Cr
  Normal: 120 × 1.00 = 120 Cr
  Good: 120 × 1.25 = 150 Cr
  Excellent: 120 × 1.60 = 192 Cr
  Masterwork: 120 × 2.50 = 300 Cr
```

#### Durability Modifiers
```
Durability Price Formula:
  RemainingPercentage = CurrentDurability / MaxDurability
  DurabilityMultiplier = max(0.10, RemainingPercentage)
  
  Tool is unusable at 0% (cannot sell broken tools)
  Minimum sell price: 10% of tool value (for heavily damaged)

Example Pricing:
  Iron Axe (Normal quality): 120 Cr base
  
  100% durability (150/150): 120 × 1.00 = 120 Cr
  75% durability (112/150): 120 × 0.75 = 90 Cr
  50% durability (75/150): 120 × 0.50 = 60 Cr
  25% durability (37/150): 120 × 0.25 = 30 Cr
  10% durability (15/150): 120 × 0.10 = 12 Cr (minimum)
  0% durability (0/150): Cannot sell (must repair first)

Combined Example:
  Iron Axe, Excellent quality, 60% durability
  Base: 120 Cr
  Quality: × 1.60 = 192 Cr
  Durability: × 0.60 = 115.20 Cr
  Final: 115 Cr (rounded)
```

#### Material Cost Modifiers
```
Special Material Bonuses:
  - Rare wood handle: +10-20% value
  - Decorative elements: +5-15% value
  - Custom engraving: +5-10% value
  - Named craftsman: +10-25% value (reputation-based)
  - Collector's appeal: Variable (1.5-5× for rare/legendary)

Example - Special Iron Axe:
  Base: 120 Cr
  Quality (Excellent): × 1.60 = 192 Cr
  Rare hardwood handle: +15% = 220.80 Cr
  Master craftsman (famous): +20% = 264.96 Cr
  Custom engraving: +5% = 278.21 Cr
  Durability 90%: × 0.90 = 250.39 Cr
  Final: 250 Cr
```

### 8.3 Market Dynamics

#### Demand Factors
```
Population-Based Demand:
  New Player Wave:
    - Stone tools: Very High demand (+50% price)
    - Iron tools: High demand (+30% price)
    - Steel tools: Normal demand
    - Duration: 1-2 real days

  Established Server:
    - Stone tools: Low demand (-20% price)
    - Iron tools: Normal demand
    - Steel tools: High demand (+25% price)
    - Advanced tools: Very High demand (+50% price)

Conflict/Disaster Events:
  War/Building Boom:
    - Hammers: Very High demand (+100% price)
    - Saws: High demand (+50% price)
    - All tools: High durability loss = high demand (+40%)
    - Duration: During conflict + 1 day after

  Meteor Event (Day 20-30):
    - All tools: Extreme demand (+150% price)
    - Steel tools: Critical demand (+200% price)
    - Duration: Days 20-35

Seasonal Demand:
  Planting Season:
    - Hoes: +40% price
    - Sickles: +20% price
    
  Harvest Season:
    - Sickles: +60% price
    - Axes: +20% price (for building storage)
    
  Building Season (post-harvest):
    - Hammers: +50% price
    - Saws: +40% price
    - All gathering tools: -10% price
```

#### Supply Factors
```
Blacksmith Population:
  0-2 blacksmiths:
    - Tool supply: Very Low
    - Price modifier: +100-200%
    - Quality availability: Poor-Normal only
    
  3-5 blacksmiths:
    - Tool supply: Low
    - Price modifier: +50-100%
    - Quality availability: Poor-Good
    
  6-10 blacksmiths:
    - Tool supply: Normal
    - Price modifier: Standard
    - Quality availability: All tiers
    
  10+ blacksmiths:
    - Tool supply: High
    - Price modifier: -10-30%
    - Quality availability: All tiers, competitive pricing

Material Availability:
  Iron shortage:
    - Iron tools: +50% price
    - Steel tools: +25% price (cascading effect)
    
  Wood shortage:
    - All tools: +10-20% price (handles affected)
    
  Rare materials abundant:
    - High-quality tools: -20% price
    - Masterwork tools: More available

Technology Level:
  Low tech (mostly stone):
    - Iron tools: +100% premium
    - Steel tools: +300% premium (if available)
    
  Medium tech (iron common):
    - Standard pricing
    
  High tech (steel production):
    - Steel tools: -20% price
    - Stone tools: -50% price (obsolete)
```

### 8.4 Economic Formulas
```
Final Price Calculation:
  
  BasePrice = ToolTierBasePrice
  
  QualityModifier = QUALITY_MULTIPLIER_TIER
  DurabilityModifier = CurrentDurability / MaxDurability
  MaterialBonus = 1 + (SpecialMaterialValue / BasePrice)
  CraftsmanBonus = 1 + (CraftsmanReputation / 100)
  
  DemandMultiplier = MarketDemandIndex (0.5 to 3.0)
  SupplyMultiplier = MarketSupplyIndex (0.5 to 1.5)
  
  FinalPrice = BasePrice × QualityModifier × max(0.10, DurabilityModifier) × 
               MaterialBonus × CraftsmanBonus × 
               DemandMultiplier × SupplyMultiplier
  
  MinPrice = BasePrice × 0.10 (10% minimum)
  FinalPrice = max(MinPrice, FinalPrice)

Market Indices:
  DemandIndex calculation:
    Base: 1.0
    Player count factor: +0.1 per 5 players above 10
    Event factor: +0.5 to +2.0 during events
    Season factor: ±0.2 seasonal
    
  SupplyIndex calculation:
    Base: 1.0
    Blacksmith factor: -0.1 per blacksmith above 5
    Stockpile factor: -0.2 if excess inventory
    Material factor: +0.2 to +0.5 if material shortage
```

---

## 9. Implementation Notes

### 9.1 Data Structure
```csharp
public class ToolItem : Item
{
    // Tool identification
    public ToolType Type { get; set; }              // Axe, Pickaxe, etc.
    public ToolTier Tier { get; set; }              // Stone, Iron, Steel
    
    // Durability
    public int CurrentDurability { get; set; }
    public int MaxDurability { get; set; }
    public int TimesRepaired { get; set; }
    public float MaxDurabilityDegradation { get; set; }  // From repairs
    
    // Quality
    public int QualityScore { get; set; }           // 0-100
    public QualityTier QualityTier { get; set; }    // Poor, Normal, etc.
    
    // Crafter info
    public string CrafterId { get; set; }
    public string CrafterName { get; set; }
    public DateTime CraftedDate { get; set; }
    
    // Special properties
    public List<ToolAttachment> Attachments { get; set; }
    public Dictionary<string, float> SpecialStats { get; set; }
    
    // Visual state
    public int VisualWearState { get; set; }        // 0-4
    
    // Calculated effective stats
    public float GetEffectiveDurability()
    {
        float qualityMult = QualitySystem.GetDurabilityMultiplier(QualityTier);
        return (MaxDurability - MaxDurabilityDegradation) * qualityMult;
    }
    
    public float GetEffectiveSpeed()
    {
        float tierSpeed = TechnicalConstants.GetToolEfficiency(Tier);
        float qualityBonus = QualitySystem.GetSpeedBonus(QualityTier);
        return tierSpeed * (1 + qualityBonus);
    }
}
```

### 9.2 Balance Reference Table
```
Tier Comparison Summary:

| Tool   | Stone (T1) | Iron (T2) | Steel (T3) | Advanced (T4) |
|--------|-----------|-----------|------------|---------------|
| Dur.   | 50 uses   | 150 uses  | 500 uses   | 1000+ uses    |
| Speed  | 100%      | 150%      | 200%       | 300-500%      |
| Yield  | 100%      | 110%      | 125%       | 150%+         |
| Cost   | 20-40 Cr  | 80-150 Cr | 200-400 Cr | 500+ Cr       |
| Unlock | None      | Skill 2   | Skill 5    | Special       |

Quality Comparison Summary:

| Quality   | Dur. Mult | Speed | Yield | Price Mult |
|-----------|-----------|-------|-------|------------|
| Poor      | 0.70×     | -10%  | -15%  | 0.60×      |
| Normal    | 1.00×     | 0%    | 0%    | 1.00×      |
| Good      | 1.15×     | +5%   | +5%   | 1.25×      |
| Excellent | 1.30×     | +10%  | +15%  | 1.60×      |
| Masterwork| 1.50×     | +15%  | +25%  | 2.50×      |
```

### 9.3 Tool Constants Reference
```
All tool constants must reference technical-constants.md:

Durability:
  - TOOL_DURABILITY_STONE = 50
  - TOOL_DURABILITY_IRON = 150
  - TOOL_DURABILITY_STEEL = 500

Efficiency:
  - TOOL_EFFICIENCY_STONE = 1.0
  - TOOL_EFFICIENCY_IRON = 1.5
  - TOOL_EFFICIENCY_STEEL = 2.0

Repair:
  - TOOL_REPAIR_COST_PERCENT = 50.0
  - TOOL_REPAIR_DURABILITY_RESTORED = 80

Quality:
  - QUALITY_MULTIPLIER_POOR = 0.70
  - QUALITY_MULTIPLIER_NORMAL = 1.00
  - QUALITY_MULTIPLIER_GOOD = 1.15
  - QUALITY_MULTIPLIER_EXCELLENT = 1.30
  - QUALITY_MULTIPLIER_MASTERWORK = 1.50

Skills:
  - SKILL_LEVELS_COUNT = 10
  - SKILL_BONUS_PER_LEVEL_PERCENT = 5.0
```

---

## 10. Summary

This specification defines a comprehensive 8-tool system with:
- **5 gathering tools**: Axe, Pickaxe, Hoe, Sickle, Shovel
- **3 utility tools**: Hammer, Saw, Knife
- **4 progression tiers**: Stone (50 uses), Iron (150 uses), Steel (500 uses), Advanced (1000+)
- **5 quality tiers**: Poor to Masterwork with measurable bonuses
- **Complete durability system**: Visual states, repair mechanics, degradation
- **Skill integration**: 10-level progression affecting all tool aspects
- **Economic model**: Dynamic pricing based on quality, durability, supply, demand

All numerical values reference `planning/meta/technical-constants.md` for single-source-of-truth consistency.

---

**END OF SPECIFICATION**

*This document is part of Session 3: Core Gameplay Loops planning. For questions or updates, reference the technical-constants.md file for all numerical values.*
