# Comprehensive Entity Catalog

> **PROJECT**: Societies - AI-Powered Society Simulation Game  
> **DOCUMENT TYPE**: Technical Reference - Entity Specifications  
> **STATUS**: 📝 MVP+ Foundation Document  
> **LAST UPDATED**: 2026-02-01  
> **VERSION**: v1.0.0

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Transportation Entities](#2-transportation-entities)
3. [Crafting/Production Entities](#3-craftingproduction-entities)
4. [Storage Entities](#4-storage-entities)
5. [Furniture/Decorative Entities](#5-furnituredecorative-entities)
6. [Infrastructure Entities](#6-infrastructure-entities)
7. [Entity Specifications Template](#7-entity-specifications-template)
8. [Art/Visual Guidelines](#8-artvisual-guidelines)
9. [Technical Implementation Notes](#9-technical-implementation-notes)
10. [Development Priority Matrix](#10-development-priority-matrix)
11. [Appendices](#11-appendices)

---

## 1. Executive Summary

### 1.1 Purpose

This document serves as the **authoritative reference** for all custom-shaped entities in Societies. Unlike the 1m³ voxel block system, entities are physics-based objects with complex geometries, custom interactions, and specialized behaviors. This catalog provides:

- Complete specifications for every entity type
- Development phase organization (MVP vs Post-MVP)
- Technical implementation guidelines
- Art and asset requirements
- Priority and dependency tracking

### 1.2 Organization Methodology

Entities are organized using a **dual-axis system**:

**Axis 1: Functional Category**
- Transportation (movement and logistics)
- Crafting/Production (item creation and processing)
- Storage (inventory management)
- Furniture/Decorative (environment and comfort)
- Infrastructure (world modification and connectivity)

**Axis 2: Development Phase**

| Phase | Scope | Timeline | Entity Count |
|-------|-------|----------|--------------|
| **MVP** | Core gameplay essential | Months 1-3 | ~20 entities |
| **Post-MVP Phase 1** | Enhanced systems | Months 4-6 | ~25 entities |
| **Post-MVP Phase 2** | Advanced features | Months 7-12 | ~20 entities |
| **Post-MVP Phase 3** | Expansion content | Year 2+ | TBD |

### 1.3 Key Technical Concepts

#### Hybrid Block-Entity System
Societies uses two distinct object types:

**Voxel Blocks (1m³)**
- Uniform grid-based placement
- Simple collision (AABB)
- Terrain and structural elements
- Efficient batch rendering
- Static or limited animation

**Custom Entities (This Document)**
- Arbitrary shapes and sizes
- Physics-based (RigidBody3D)
- Complex interactions
- Dynamic positioning
- Weight and carrying mechanics

#### Physics Integration
All entities in this catalog:
- Extend `RigidBody3D` or appropriate physics body
- Participate in collision detection
- Respond to forces and impulses
- Support carrying/transportation mechanics
- Synchronize across network

#### Weight/Carrying System
- **Base Weight**: Entity mass when empty
- **Load Weight**: Additional mass from contents
- **Max Carry**: Player/agent carrying capacity
- **Encumbrance**: Movement speed modifier based on load

### 1.4 Document Usage

**For Designers**: Reference entity capabilities and interactions
**For Artists**: Understand visual requirements and style guidelines
**For Programmers**: Review technical specs and implementation patterns
**For QA**: Verify entity behavior against specifications

---

## 2. Transportation Entities

### 2.1 MVP Transportation

#### 2.1.1 Hand Cart

**Overview**
The hand cart is the most basic transportation entity, serving as the entry-point logistics tool for players and AI agents.

**Specifications**
```yaml
Entity ID: transport_hand_cart_01
Name: Hand Cart
Description: A simple wooden cart with two wheels, designed for manual pushing
Category: Transportation
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.2m x 0.8m x 1.5m (W x H x L)
Base Weight: 15 kg
Max Load: 80 kg
Total Max Weight: 95 kg
Center of Mass: 0.4m from ground (empty), 0.6m (loaded)
```

**Capacity**
```yaml
Inventory Slots: 12
Slot Size: Standard (1m³ equivalent volume)
Volume Capacity: 0.8m³
Weight Capacity: 80 kg
```

**Crafting Requirements**
```yaml
Workbench: Carpentry Table
Recipe:
  - Planks (Wooden): 8 units
  - Wooden Sticks: 4 units
  - Iron Nails: 16 units
  - Wheel (Wooden): 2 units
Craft Time: 45 seconds
Skill Required: Carpentry (Level 1)
```

**Interactions**
- **Push/Pull**: Character applies force to move cart
- **Load/Unload**: Inventory management interface
- **Lock/Unlock**: Prevent unauthorized access (key or permission-based)
- **Attach**: Connect to other carts (train up to 3)

**Physics Properties**
```yaml
Body Type: RigidBody3D
Mass: 15 kg (base)
Friction: 0.3 (wood on various surfaces)
Rolling Friction: 0.05 (wheel efficiency)
Angular Damping: 0.5
Linear Damping: 0.2
Collision Layers: Entity, Transport, Interactable
Collision Mask: Terrain, Block, Entity, Player
```

**Network Sync**
```yaml
Sync Priority: High (when moving)
Sync Rate: 20 Hz (active), 1 Hz (stationary)
Interpolation: Yes (client-side prediction)
Ownership: Player/Agent currently pushing
```

---

#### 2.1.2 Minecart

**Overview**
Rail-based transportation for automated logistics in mines and established routes.

**Specifications**
```yaml
Entity ID: transport_minecart_01
Name: Minecart
Description: A metal cart designed to run on rails, capable of carrying heavy loads
Category: Transportation
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.0m x 0.9m x 1.8m (W x H x L)
Base Weight: 45 kg
Max Load: 200 kg
Total Max Weight: 245 kg
Center of Mass: 0.5m from ground (fixed)
```

**Capacity**
```yaml
Inventory Slots: 18
Slot Size: Standard
Volume Capacity: 1.2m³
Weight Capacity: 200 kg
```

**Crafting Requirements**
```yaml
Workbench: Anvil
Recipe:
  - Iron Plates: 6 units
  - Iron Rods: 4 units
  - Wheel (Metal): 4 units
  - Lubricant: 1 unit
Craft Time: 90 seconds
Skill Required: Metalworking (Level 2)
```

**Interactions**
- **Load/Unload**: Inventory interface
- **Couple/Decouple**: Connect to other minecarts (up to 5)
- **Brake**: Manual stopping mechanism
- **Launch**: Powered start from stations

**Physics Properties**
```yaml
Body Type: RigidBody3D (constrained to rails)
Mass: 45 kg (base)
Friction: 0.1 (rail wheels)
Rolling Friction: 0.02 (low resistance)
Angular Damping: 1.0 (constrained)
Linear Damping: 0.05
Rail Constraint: HingeJoint along rail path
Collision Layers: Entity, Transport, RailVehicle
Collision Mask: Rails, Terrain
```

**Network Sync**
```yaml
Sync Priority: High
Sync Rate: 20 Hz
Interpolation: Yes
Path Prediction: Rail-based position extrapolation
```

---

#### 2.1.3 Rail Segments (Basic)

**Overview**
Straight and curved rail segments for minecart infrastructure.

**Specifications**
```yaml
Entity ID: infrastructure_rail_straight_01
Name: Straight Rail Segment
Description: 2-meter straight rail section for minecart tracks
Category: Infrastructure / Transportation Support
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 0.3m x 0.15m x 2.0m (W x H x L)
Base Weight: 8 kg per segment
Stackable: Yes (inventory)
Stack Size: 32
```

**Capacity**
- Not applicable (infrastructure, not container)

**Crafting Requirements**
```yaml
Workbench: Anvil
Recipe (per 4 segments):
  - Iron Rods: 8 units
  - Wooden Ties: 4 units
  - Iron Spikes: 8 units
Craft Time: 30 seconds per batch
Skill Required: Metalworking (Level 1)
```

**Placement**
```yaml
Placement Type: Surface (on blocks)
Orientation: Cardinal directions + 45° variants
Connects To: Other rail segments
Slope Support: Yes (0° to 30° incline)
```

**Interactions**
- **Place**: Standard block-like placement
- **Remove**: Returns to inventory (if no cart present)
- **Configure**: Direction and junction settings

**Physics Properties**
```yaml
Body Type: StaticBody3D
Collision Shape: Box + Rail groove
Collision Layers: Rails, Infrastructure
Collision Mask: None (passive)
Minecart Guide: Custom collision groove
```

**Variants**
- Straight Rail (2m)
- Curved Rail (90°, 2m radius)
- Junction Rail (Y-split)
- Elevated Rail (with supports)

---

### 2.2 Post-MVP Phase 1 Transportation

#### 2.2.1 Wheelbarrow

**Specifications**
```yaml
Entity ID: transport_wheelbarrow_01
Name: Wheelbarrow
Description: Single-wheel cart for narrow spaces and gardening
Phase: Post-MVP Phase 1
Bounding Box: 0.8m x 0.9m x 1.2m
Base Weight: 10 kg
Max Load: 60 kg
Capacity: 8 slots, 0.5m³
```

**Key Differences from Hand Cart**
- Narrower profile (0.8m vs 1.2m)
- Single wheel (tighter turning radius)
- Lower capacity but more maneuverable
- Tilting dump mechanism

**Crafting**
```yaml
Recipe:
  - Planks: 6 units
  - Wheel: 1 unit
  - Wooden Handle: 2 units
Skill: Carpentry (Level 2)
```

---

#### 2.2.2 Wagon (Animal-Pulled)

**Specifications**
```yaml
Entity ID: transport_wagon_01
Name: Wagon
Description: Large four-wheeled cart designed for animal harness
Phase: Post-MVP Phase 1
Bounding Box: 2.0m x 1.4m x 3.0m
Base Weight: 120 kg
Max Load: 500 kg
Capacity: 36 slots, 3.0m³
```

**Requirements**
- Animal harness system (post-MVP)
- Hitching post entity
- Pathfinding for animals

**Physics**
```yaml
Mass: 120 kg base
Pull Force Required: 150N minimum
Animal Capacity: 1-2 draft animals
Brake System: Manual + automatic on slopes
```

---

#### 2.2.3 Basic Vehicle Chassis

**Specifications**
```yaml
Entity ID: transport_vehicle_chassis_01
Name: Basic Vehicle Chassis
Description: Modular vehicle frame for custom construction
Phase: Post-MVP Phase 1
Bounding Box: 2.0m x 0.5m x 4.0m (chassis only)
Base Weight: 200 kg
Max Load: 800 kg
```

**Modular Components**
- Engine mount (future steam/power)
- Seat attachments (1-4 seats)
- Cargo bed options
- Wheel configurations (4, 6 wheel)

---

### 2.3 Post-MVP Phase 2 Transportation

#### 2.3.1 Steam Vehicle

**Specifications**
```yaml
Entity ID: transport_steam_vehicle_01
Name: Steam Vehicle
Description: Self-propelled vehicle powered by steam engine
Phase: Post-MVP Phase 2
Bounding Box: 2.2m x 2.0m x 4.5m
Base Weight: 800 kg
Max Load: 1000 kg
Fuel: Coal, Wood
Capacity: 24 slots + fuel slot
```

**Systems**
- Steam engine (boiler, piston, drive train)
- Fuel consumption
- Water management
- Pressure mechanics
- Speed control (throttle)

---

#### 2.3.2 Advanced Rail Carts

**Types**
- **Powered Minecart**: Self-propelled with coal
- **Storage Minecart**: Double capacity (36 slots)
- **Passenger Minecart**: Seating for 2-4
- **Hopper Minecart**: Auto-load/unload at stations
- **TNT Minecart**: Explosive transport/delivery

---

#### 2.3.3 Boats/Water Transport

**Specifications**
```yaml
Entity ID: transport_boat_small_01
Name: Small Boat
Description: Basic water vessel for river/lake travel
Phase: Post-MVP Phase 2
Bounding Box: 2.0m x 0.8m x 4.0m
Base Weight: 150 kg
Capacity: 4 slots + 2 passengers
```

**Advanced Types**
- Rowboat (manual)
- Sailboat (wind-powered)
- Cargo barge (high capacity)
- Steam boat (engine-powered)

---

## 3. Crafting/Production Entities

### 3.1 MVP Crafting/Production

#### 3.1.1 Basic Workbench

**Specifications**
```yaml
Entity ID: crafting_workbench_basic_01
Name: Basic Workbench
Description: Fundamental crafting station for simple recipes
Category: Crafting/Production
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.5m x 1.0m x 0.8m (W x H x D)
Base Weight: 35 kg
Placement: Surface (floor)
Rotatable: Yes (4 directions)
```

**Capacity**
```yaml
Input Slots: 9 (3x3 grid)
Output Slot: 1
Tool Slot: 1 (for crafting tools)
Recipe Memory: Last 8 crafted items
```

**Crafting Requirements**
```yaml
Recipe:
  - Planks: 4 units
  - Wooden Sticks: 4 units
  - Iron Nails: 8 units
Craft Time: 20 seconds
Skill Required: None (starter recipe)
```

**Functionality**
```yaml
Recipes Supported: 50+ basic recipes
Craft Speed: 1x base speed
Power Required: None (manual)
Tool Durability: Affects craft speed/quality
Recipe Categories:
  - Basic Tools
  - Simple Components
  - Wooden Items
  - Food Preparation (basic)
```

**Interactions**
- **Open Interface**: 3x3 crafting grid + output
- **Recipe Browse**: Filterable recipe list
- **Queue Crafting**: Batch craft up to 64
- **Tool Management**: Equip/unequip crafting tools

**Physics Properties**
```yaml
Body Type: StaticBody3D
Mass: 35 kg
Fixed: Yes (when placed)
Can Carry: No (too heavy)
Collision Layers: Entity, CraftingStation, Interactable
```

**Network Sync**
```yaml
Sync Priority: Low
Sync Rate: On interaction only
State: Active crafter, current recipe
```

---

#### 3.1.2 Furnace/Smelter

**Specifications**
```yaml
Entity ID: crafting_furnace_basic_01
Name: Stone Furnace
Description: Basic furnace for smelting ores and cooking
Category: Crafting/Production
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.0m x 1.2m x 1.0m
Base Weight: 80 kg
Material: Stone bricks
```

**Capacity**
```yaml
Input Slot: 1 (ore/food)
Fuel Slot: 1 (coal/wood)
Output Slot: 1
Fuel Capacity: 64 units coal
```

**Functionality**
```yaml
Smelt Time: 10 seconds per item
Fuel Duration: 80 seconds per coal
Max Temperature: 1200°C
Recipes:
  - Iron Ore → Iron Ingot
  - Copper Ore → Copper Ingot
  - Gold Ore → Gold Ingot
  - Clay → Brick
  - Raw Food → Cooked Food
```

**States**
- **Idle**: Cold, no fuel
- **Heating**: Fuel burning, heating up
- **Active**: Operating temperature
- **Cooling**: Fuel exhausted, cooling down

**Visual Feedback**
- Chimney smoke (when active)
- Glow intensity based on temperature
- Door animation (open/close)

---

#### 3.1.3 Anvil

**Specifications**
```yaml
Entity ID: crafting_anvil_iron_01
Name: Iron Anvil
Description: Heavy metalworking station for tool and weapon forging
Category: Crafting/Production
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 0.8m x 0.6m x 0.5m
Base Weight: 150 kg
Material: Iron
Special: Cannot be carried (must be placed via construction)
```

**Capacity**
```yaml
Input Slots: 3
Output Slot: 1
Tool Slot: 1 (hammer)
```

**Functionality**
```yaml
Recipes: Metal tools, weapons, armor, mechanisms
Craft Speed: 1x
Quality Modifier: +10% (better durability on outputs)
Required Tool: Hammer (various tiers)
```

**Crafting Requirements**
```yaml
Recipe:
  - Iron Blocks: 3 units
  - Iron Ingots: 4 units
Craft Time: 120 seconds
Skill: Metalworking (Level 3)
```

---

#### 3.1.4 Carpentry Table

**Specifications**
```yaml
Entity ID: crafting_carpentry_table_01
Name: Carpentry Table
Description: Specialized workstation for woodworking
Category: Crafting/Production
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.8m x 1.0m x 1.0m
Base Weight: 45 kg
```

**Capacity**
```yaml
Input Slots: 6
Output Slot: 1
Tool Slot: 1 (saw)
```

**Functionality**
```yaml
Recipes: Advanced wood items, furniture, tools, containers
Craft Speed: 2x (vs basic workbench for wood items)
Precision: Higher quality wooden outputs
Material Efficiency: +15% (less waste)
```

---

### 3.2 Post-MVP Phase 1 Crafting/Production

#### 3.2.1 Advanced Workshops

**Types**

**Smithy Forge**
```yaml
ID: crafting_smithy_01
Description: Complete metalworking workshop
Size: 3m x 2.5m x 3m
Features:
  - Forge (heating)
  - Anvil (shaping)
  - Quenching tub
  - Tool rack
Recipes: Advanced metallurgy, alloys, mechanisms
```

**Loom**
```yaml
ID: crafting_loom_01
Description: Textile production station
Size: 1.5m x 1.8m x 1.0m
Recipes: Cloth, clothing, tapestries, rope
Automation: Can load pattern cards
```

**Alchemy Station**
```yaml
ID: crafting_alchemy_01
Description: Potion and chemical crafting
Size: 1.2m x 1.5m x 0.8m
Recipes: Potions, explosives, dyes, treatments
Hazard: Some recipes can fail/explode
```

---

#### 3.2.2 Assembly Tables

**Specifications**
```yaml
Entity ID: crafting_assembly_table_01
Name: Assembly Table
Description: Complex multi-component crafting
Phase: Post-MVP Phase 1
Size: 2.0m x 1.0m x 1.5m
```

**Functionality**
- Multi-stage assembly process
- Component verification
- Quality control mechanics
- Blueprint reading

---

#### 3.2.3 Power Tools Station

**Specifications**
```yaml
Entity ID: crafting_power_tools_01
Name: Power Tools Station
Description: Mechanically-assisted crafting (water/wind/steam powered)
Phase: Post-MVP Phase 1
Size: 2.5m x 1.5m x 2.0m
Power: External source required
```

**Tools**
- Power saw (wood)
- Drill press (holes/precision)
- Grinding wheel (sharpening)
- Lathe (turning)

---

### 3.3 Post-MVP Phase 2 Crafting/Production

#### 3.3.1 Automated Production

**Automated Workbench**
```yaml
ID: crafting_auto_workbench_01
Description: Self-crafting with recipe memory
Features:
  - Auto-feed from adjacent storage
  - Recipe queuing
  - Output to designated container
Power: Clockwork (spring) or basic mechanical
```

**Mechanical Press**
```yaml
ID: crafting_mechanical_press_01
Description: Automated forming and stamping
Recipes: Metal plates, gears, bulk items
Throughput: 10x manual speed
```

---

#### 3.3.2 Factory Machinery

**Assembly Line Components**
- Conveyor-based transport between stations
- Robotic arms (simple mechanical)
- Quality scanners
- Packaging stations

**Power Systems**
- Water wheel generators
- Windmill power
- Steam engines
- Early electrical (Phase 3)

---

## 4. Storage Entities

### 4.1 MVP Storage

#### 4.1.1 Wooden Chest

**Specifications**
```yaml
Entity ID: storage_chest_wooden_01
Name: Wooden Chest
Description: Basic lockable storage container
Category: Storage
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.0m x 0.6m x 0.5m
Base Weight: 12 kg
Material: Wood
Carryable: Yes (empty, by 2 people)
```

**Capacity**
```yaml
Inventory Slots: 27 (3x3x3)
Slot Size: Standard
Max Item Types: 27 different items
Total Volume: 0.3m³
Weight Capacity: 200 kg (of contents)
```

**Crafting Requirements**
```yaml
Recipe:
  - Planks: 8 units
  - Iron Hinge: 2 units
  - Iron Lock (optional): 1 unit
Craft Time: 30 seconds
Skill: Carpentry (Level 1)
```

**Security**
```yaml
Default: Unlocked
Lockable: Yes (with lock upgrade)
Access: Owner + permission list
Breakable: Yes (drops contents)
```

**Interactions**
- **Open**: Inventory interface
- **Lock/Unlock**: Security toggle
- **Rename**: Custom label
- **Sort**: Auto-organize contents

**Physics Properties**
```yaml
Body Type: RigidBody3D (can be pushed/moved)
Mass: 12 kg + contents
Friction: 0.6
Collision Layers: Entity, Storage, Interactable, Movable
Can Carry: Yes (when empty, 2-person lift when full)
```

---

#### 4.1.2 Storage Crate

**Specifications**
```yaml
Entity ID: storage_crate_01
Name: Storage Crate
Description: Open-top crate for bulk storage
Category: Storage
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.0m x 1.0m x 1.0m
Base Weight: 8 kg
Stackable: Yes (up to 3 high when empty)
```

**Capacity**
```yaml
Inventory Slots: 18
Slot Size: Large (2x standard)
Bulk Focus: Designed for single-item storage
Max Stack per Slot: 128 (2x normal)
```

**Key Difference from Chest**
- Open top (no lid animation)
- Larger slots for bulk items
- Stackable when empty
- No lock mechanism
- Lower security (easier to access/steal)

---

#### 4.1.3 Barrel

**Specifications**
```yaml
Entity ID: storage_barrel_01
Name: Barrel
Description: Cylindrical container for liquids and bulk goods
Category: Storage
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 0.8m x 1.0m x 0.8m (cylindrical)
Base Weight: 15 kg
Shape: Cylinder
Rolls: Yes (when pushed)
```

**Capacity**
```yaml
Inventory Slots: 12
Liquid Support: Yes (up to 50L)
Slot Size: Standard
Special: Preserves food 25% longer
```

**Crafting**
```yaml
Recipe:
  - Wooden Staves: 12 units
  - Iron Hoops: 3 units
  - Lid: 1 unit
Skill: Carpentry (Level 2)
```

**Unique Properties**
- Can roll when pushed (physics)
- Liquid storage capability
- Food preservation bonus
- Can be sealed (long-term storage)

---

### 4.2 Post-MVP Phase 1 Storage

#### 4.2.1 Warehouse Shelving

**Specifications**
```yaml
Entity ID: storage_shelving_01
Name: Warehouse Shelving
Description: Industrial storage system for high-density organization
Phase: Post-MVP Phase 1
Size: 3m x 2.5m x 0.6m
Modular: Yes (connects side-by-side)
```

**Capacity**
```yaml
Slots: 54 (6 shelves x 9 slots each)
Access Height: Requires ladder for top shelves
Organization: Labeling system included
```

---

#### 4.2.2 Storage Racks

**Types**
- **Tool Rack**: Wall-mounted, holds 8 tools
- **Weapon Rack**: Displays/armory storage
- **Drying Rack**: Food/material preservation
- **Wine Rack**: Specialized for bottles

---

#### 4.2.3 Refrigerated Storage

**Specifications**
```yaml
Entity ID: storage_icebox_01
Name: Icebox
Description: Cold storage using ice blocks
Phase: Post-MVP Phase 1
Size: 1.2m x 1.0m x 1.0m
```

**Functionality**
- Requires ice replenishment
- Extends food preservation by 300%
- Limited duration (ice melts)
- Upgrade path to mechanical refrigeration

---

### 4.3 Post-MVP Phase 2 Storage

#### 4.3.1 Automated Storage

**Smart Chest**
```yaml
ID: storage_smart_chest_01
Features:
  - Auto-sorting (configurable)
  - Inventory display (external)
  - Search functionality
  - Link to multiple chests
Power: None (mechanical logic)
```

**Buffer Storage**
- Input/output management
- Queue system for crafters
- Overflow handling
- Priority routing

---

#### 4.3.2 Conveyor Systems

**Conveyor Belt**
```yaml
Entity ID: conveyor_belt_01
Name: Conveyor Belt
Description: Automated item transport
Phase: Post-MVP Phase 2
Size: 1m x 0.3m x 1m (per segment)
Speed: 1 m/s
```

**Components**
- Straight segments
- Curves (90°)
- Incline/decline
- Junctions (sorting)
- Sensors and diverters

---

## 5. Furniture/Decorative Entities

### 5.1 MVP Furniture/Decorative

#### 5.1.1 Bed

**Specifications**
```yaml
Entity ID: furniture_bed_basic_01
Name: Basic Bed
Description: Sleeping accommodation for rest and spawn point
Category: Furniture
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 1.0m x 0.6m x 2.0m
Base Weight: 25 kg
Placement: Floor (requires 2x1 space)
```

**Functionality**
```yaml
Use: Sleep (night skip, health regen)
Spawn Point: Yes (when claimed)
Comfort: Basic (+50% rest efficiency)
Capacity: 1 person
```

**Crafting**
```yaml
Recipe:
  - Wooden Frame: 4 units
  - Mattress (straw): 2 units
  - Fabric: 4 units
Skill: Carpentry (Level 1)
```

**States**
- **Made**: Ready for use
- **Unmade**: Recently used (aesthetic only)
- **Occupied**: Player/AI sleeping

---

#### 5.1.2 Chair

**Specifications**
```yaml
Entity ID: furniture_chair_wooden_01
Name: Wooden Chair
Description: Basic seating
Category: Furniture
Phase: MVP
Size: 0.5m x 1.0m x 0.5m
Weight: 8 kg
```

**Variants**
- Stool (simpler, lighter)
- Bench (2-3 person seating)
- Armchair (higher comfort)

---

#### 5.1.3 Table

**Specifications**
```yaml
Entity ID: furniture_table_wooden_01
Name: Wooden Table
Description: Surface for crafting, eating, display
Category: Furniture
Phase: MVP
Size: 1.5m x 0.8m x 1.0m
Weight: 20 kg
```

**Functionality**
- Surface placement (items can sit on top)
- Crafting auxiliary space
- Dining functionality
- Display surface

**Variants**
- Small table (1m x 0.8m)
- Large table (2m x 1m)
- Work table (sturdy, 2x durability)

---

#### 5.1.4 Door

**Specifications**
```yaml
Entity ID: furniture_door_wooden_01
Name: Wooden Door
Description: Access control and security
Category: Furniture
Phase: MVP
Size: 1.0m x 2.0m x 0.1m (when closed)
Weight: 25 kg
```

**Functionality**
- Open/close (hinge animation)
- Lockable (with upgrade)
- Auto-close option
- Double door support

**Variants**
- Reinforced door (metal bands, stronger)
- Trapdoor (floor/ceiling)
- Gate (fence-style, outdoor)
- Portcullis (vertical sliding)

---

### 5.2 Post-MVP Furniture/Decorative

#### 5.2.1 Decorative Items

**Wall Decor**
- Paintings (various sizes)
- Tapestries
- Mounted trophies
- Shelves
- Mirrors

**Floor Decor**
- Rugs/carpets
- Potted plants
- Statues
- Fountains (small)

**Tabletop**
- Candlesticks
- Vases
- Books/stacks
- Tableware settings

---

#### 5.2.2 Lighting Fixtures

**Types**
- **Wall Sconce**: Mounted light source
- **Chandelier**: Ceiling-hung, multiple candles
- **Floor Lamp**: Standing, movable
- **Lantern Post**: Outdoor lighting
- **Brazier**: Large fire-based lighting

**Mechanics**
- Fuel consumption (candles/oil)
- Light radius and intensity
- Shadow casting
- Color temperature (warm/cool)

---

#### 5.2.3 Signs/Displays

**Sign**
```yaml
Size: 0.8m x 0.6m
Custom Text: Yes (player-defined)
Font: Blocky/legible
Paintable: Yes
```

**Notice Board**
```yaml
Size: 1.5m x 1.0m
Multiple notices: Yes
Interaction: Read/remove/post
Public/Private: Configurable
```

---

## 6. Infrastructure Entities

### 6.1 MVP Infrastructure

#### 6.1.1 Ladder

**Specifications**
```yaml
Entity ID: infrastructure_ladder_01
Name: Ladder
Description: Vertical climbing access
Category: Infrastructure
Phase: MVP
```

**Physical Properties**
```yaml
Bounding Box: 0.3m x 3.0m x 0.1m (per segment)
Base Weight: 3 kg per segment
Stackable: Yes (vertical)
Placement: Wall or free-standing
```

**Functionality**
```yaml
Climb Speed: 2 m/s
Capacity: 1 person per segment
Stability: Requires support every 3 segments
Safety: Fall damage reduction (climbing vs falling)
```

**Crafting**
```yaml
Recipe (4 segments):
  - Wooden Sticks: 8 units
  - Rope/Iron: 2 units
Craft Time: 15 seconds
Skill: None
```

**Placement Rules**
- Vertical stacking (unlimited with support)
- Wall-mounted (preferred, +stability)
- Free-standing (requires brace at base)
- Retractable variant (post-MVP)

---

#### 6.1.2 Simple Bridge Segments

**Specifications**
```yaml
Entity ID: infrastructure_bridge_wooden_01
Name: Wooden Bridge Segment
Description: Walkway for crossing gaps
Category: Infrastructure
Phase: MVP
Size: 1m x 0.2m x 3m (per segment)
Weight: 15 kg
```

**Types**
- **Plank Bridge**: Basic, 1m wide
- **Rope Bridge**: Suspended, sways
- **Support Beam**: Structural component

**Functionality**
- Span gaps up to 15m (with supports)
- Support placement: Every 5m
- Weight limit: 500 kg per segment

---

### 6.2 Post-MVP Infrastructure

#### 6.2.1 Elevated Platforms

**Scaffolding**
```yaml
Size: Modular 2m x 2m sections
Height: Adjustable 2-10m
Weight Capacity: 200 kg/m²
Features:
  - Guard rails (safety)
  - Stair access
  - Tool hooks
  - Wheels (movable variant)
```

**Balconies/Decks**
- Building attachment
- Railing variants
- Stair connections

---

#### 6.2.2 Conveyor Belts

See Section 4.3.2 for detailed specs.

**Quick Reference**
```yaml
Type: Infrastructure + Storage hybrid
Purpose: Automated item transport
Speed: 1-5 m/s (configurable)
Capacity: Items on belt (no slot limit)
Power: Required (Phase 2+)
```

---

#### 6.2.3 Pipes/Conduits

**Fluid Pipes**
```yaml
Size: 0.2m diameter, 1m segments
Capacity: 10L per segment
Flow Rate: 5L/s
Materials: Clay, Copper, Iron
Pressure Limit: Varies by material
```

**Power Conduits**
```yaml
Size: 0.15m diameter
Transmission: Mechanical power (Phase 2)
Efficiency: 95% per 10m
Max Load: 100 HP (horsepower)
```

---

## 7. Entity Specifications Template

### 7.1 Standard Specification Format

All entities in this catalog follow this template structure:

#### Header Information
```yaml
Entity ID: [category]_[name]_[variant]_[version]
Name: [Display Name]
Description: [Brief description]
Category: [Transportation|Crafting|Storage|Furniture|Infrastructure]
Phase: [MVP|Post-MVP Phase 1|Phase 2|Phase 3]
Status: [Planned|In Design|Approved|Implemented]
```

#### Physical Properties
```yaml
Bounding Box: [W x H x D in meters]
Collision Shape: [Box|Cylinder|Sphere|Mesh|Compound]
Base Weight: [kg]
Max Load: [kg] (if applicable)
Total Max Weight: [kg] (base + max load)
Center of Mass: [offset from origin]
Material: [primary material]
```

#### Capacity (Container Entities)
```yaml
Inventory Slots: [number]
Slot Size: [Standard|Large|Small]
Volume Capacity: [m³]
Weight Capacity: [kg]
Special Storage: [liquids|specific item types]
```

#### Crafting Requirements
```yaml
Workbench: [required station]
Recipe:
  - [Material]: [quantity]
  - [Material]: [quantity]
Craft Time: [seconds]
Skill Required: [Skill Name] (Level [X])
Unlock Condition: [research/achievement/phase]
```

#### Interactions
```yaml
Primary Interaction: [main action]
Secondary Interactions:
  - [action 1]: [description]
  - [action 2]: [description]
Interface Type: [none|inventory|crafting|custom UI]
Animation: [interaction animation]
```

#### Physics Properties
```yaml
Body Type: [StaticBody3D|RigidBody3D|CharacterBody3D]
Mass: [kg]
Friction: [0.0-1.0]
Bounciness: [0.0-1.0]
Angular Damping: [0.0-1.0]
Linear Damping: [0.0-1.0]
Gravity Scale: [multiplier]
Can Carry: [Yes|No|Conditional]
Carry Conditions: [requirements]
Collision Layers: [list]
Collision Mask: [list]
```

#### Network Synchronization
```yaml
Sync Priority: [Critical|High|Medium|Low]
Sync Rate: [Hz]
Interpolation: [Yes|No]
Prediction: [Client|Server|Hybrid]
Ownership: [determines authoritative control]
State Updates: [what changes trigger sync]
```

### 7.2 Specification Examples by Category

#### Transportation Example (Hand Cart - Full)
```yaml
# Header
Entity ID: transport_hand_cart_wooden_01
Name: Hand Cart
Description: A simple wooden cart with two wheels for manual transport
Category: Transportation
Phase: MVP
Status: Approved

# Physical
Bounding Box: 1.2m x 0.8m x 1.5m
Collision Shape: Compound (chassis + wheels)
Base Weight: 15 kg
Max Load: 80 kg
Total Max Weight: 95 kg
Center of Mass: 0.4m Y-offset
Material: Wood (frame), Iron (fittings)

# Capacity
Inventory Slots: 12
Slot Size: Standard
Volume Capacity: 0.8m³
Weight Capacity: 80 kg

# Crafting
Workbench: Carpentry Table
Recipe:
  - Wooden Planks: 8
  - Wooden Sticks: 4
  - Iron Nails: 16
  - Wheel (Wooden): 2
Craft Time: 45 seconds
Skill: Carpentry (Level 1)

# Interactions
Primary: Push/Pull (force-based movement)
Secondary:
  - Open Inventory (load/unload)
  - Lock/Unlock (security)
  - Attach Cart (train up to 3)
Interface: Inventory + Context Menu
Animation: Push/pull (player), Open/close lid

# Physics
Body Type: RigidBody3D
Mass: 15-95 kg (dynamic)
Friction: 0.3
Rolling Friction: 0.05
Angular Damping: 0.5
Linear Damping: 0.2
Gravity Scale: 1.0
Can Carry: Yes (when empty, 2-person when loaded)
Carry Conditions: Empty or <20kg contents
Collision Layers: Entity, Transport, Interactable, Movable
Collision Mask: Terrain, Block, Entity, Player, Transport

# Network
Sync Priority: High (when moving), Low (stationary)
Sync Rate: 20 Hz (moving), 1 Hz (stationary)
Interpolation: Yes (position/rotation)
Prediction: Client-side movement prediction
Ownership: Pushing player/agent
State Updates: Position, rotation, velocity, inventory
```

#### Crafting Station Example (Furnace)
```yaml
# Header
Entity ID: crafting_furnace_stone_01
Name: Stone Furnace
Category: Crafting/Production
Phase: MVP

# Physical
Bounding Box: 1.0m x 1.2m x 1.0m
Collision Shape: Box
Base Weight: 80 kg
Material: Stone

# Capacity
Input: 1 slot
Fuel: 1 slot
Output: 1 slot

# Crafting Recipe
Workbench: None (starter)
Materials:
  - Stone Bricks: 16
  - Clay: 4
Craft Time: 60 seconds

# Interactions
Primary: Open smelting interface
Secondary:
  - Add fuel
  - Remove output
  - Check temperature
Interface: 3-slot furnace UI + recipe list

# Physics
Body Type: StaticBody3D
Mass: 80 kg
Can Carry: No (fixed placement)

# State Machine
States:
  - IDLE: Cold, dark
  - HEATING: Warming up
  - ACTIVE: Operating, lit, smoke
  - COOLING: Cooling down
State Transitions: Fuel-based

# Visual Feedback
Animations:
  - Door open/close
  - Flame intensity (based on fuel)
  - Smoke particles (when active)
  - Glow (light emission when hot)

# Network
Sync Priority: Medium
Sync Rate: 2 Hz (state changes)
State: Current state, temperature, progress
```

### 7.3 Variant System

Entities support multiple variants through a structured naming and configuration system.

#### Variant Naming Convention
```
[base_id]_[material]_[quality]_[special]

Examples:
- storage_chest_wooden_basic_01
- storage_chest_iron_reinforced_01
- storage_chest_wooden_large_01
```

#### Variant Types

**Material Variants**
```yaml
Wooden: Standard, lighter, flammable
Iron: Stronger, heavier, rust-resistant
Steel: Premium durability
Stone: Immobile, decorative
```

**Quality Variants**
```yaml
Basic: Standard functionality
Reinforced: +50% durability, +25% capacity
Masterwork: +100% durability, special effects
```

**Size Variants**
```yaml
Small: 50% capacity, portable
Standard: Base specifications
Large: 150% capacity, immobile
```

#### Inheritance System
```yaml
Base Entity: storage_chest_wooden_basic_01

Variant: storage_chest_iron_reinforced_01
Inherits:
  - Interaction logic
  - State machine
  - Interface layout
  - Network sync rules

Overrides:
  - Material: Iron (from Wood)
  - Weight: 40 kg (from 12 kg)
  - Durability: 300% (from 100%)
  - Lock: Built-in (from upgrade)
  
Modifiers:
  - Capacity: +25%
  - Security: +50% lock strength
```

---

## 8. Art/Visual Guidelines

### 8.1 Style Consistency

#### Visual Style Definition
Societies uses a **stylized realistic** approach with the following characteristics:

**Aesthetic Principles**
- Clean, readable silhouettes
- Consistent proportional relationships
- Functional-first design with decorative elements
- Color coding by material and function
- Wear and aging for used items

**Scale Standards**
```yaml
Human Reference: 1.8m tall
Door Height: 2.0m (standard)
Block Size: 1.0m³ (reference)
Entity Scale: Proportional to real-world equivalents
Detail Level: Visible at 5m distance minimum
```

#### Material Consistency

**Wood**
- Visible grain pattern
- Color variation by type (oak, pine, etc.)
- Aging/darkening with use
- Metal reinforcements at stress points

**Metal**
- Forging marks on handcrafted items
- Rust/corrosion for outdoor exposure
- Polished surfaces on maintained items
- Heat discoloration on furnace tools

**Stone**
- Rough-hewn or cut patterns
- Mortar lines for constructed items
- Moss/weathering for exposed items
- Chisel marks on detailed work

### 8.2 LOD (Level of Detail) Requirements

#### LOD Tiers

**LOD0 (Close Range: 0-5m)**
- Full detail mesh
- 2000-5000 triangles (complex entities)
- 500-2000 triangles (simple entities)
- Full texture resolution (1K-2K)
- All animation bones
- Dynamic shadows

**LOD1 (Medium Range: 5-15m)**
- Reduced mesh complexity
- 50% triangle reduction
- 1K textures
- Simplified collision proxy
- Static shadows

**LOD2 (Far Range: 15-30m)**
- Aggressive simplification
- 25% of LOD0 triangles
- 512px textures
- Billboard/impostor option for small items
- No shadows

**LOD3 (Distant: 30m+)**
- Minimal representation
- 10% of LOD0 triangles or impostor
- 256px textures
- Shared LOD for similar items

#### LOD Transition
```yaml
Transition Method: Distance-based with hysteresis
Fade Technique: Dither or alpha (0.5m blend zone)
Switch Threshold: 5m, 15m, 30m
Pop Prevention: Smooth transition required
```

### 8.3 Collision Proxy Simplification

#### Collision Mesh Guidelines

**Complexity Limits**
```yaml
Simple Entities: 10-20 collision polygons
Medium Entities: 20-50 collision polygons
Complex Entities: 50-100 collision polygons
Maximum: 150 collision polygons
```

**Proxy Types**
```yaml
Box: Default for most entities (AABB)
Cylinder: Round objects (barrels, wheels)
Capsule: Humanoid/organic shapes
Convex Hull: Irregular but solid shapes
Compound: Multiple primitives for complex shapes
Mesh: Last resort for detailed collision needs
```

**Approximation Rules**
- Match visual silhouette at player height
- Simplify protrusions <5cm
- Combine adjacent elements
- Use compound shapes for moving parts
- Decompose concave shapes into convex parts

### 8.4 Texture Atlas Consideration

#### Atlas Strategy

**Entity Atlas Organization**
```yaml
Atlas 1 - Furniture: 2048x2048px
  - Beds, chairs, tables, storage
  - Common wood types
  
Atlas 2 - Crafting: 2048x2048px
  - Workbenches, furnaces, anvils
  - Metal and stone textures
  
Atlas 3 - Transport: 2048x2048px
  - Carts, rails, vehicles
  - Wheels, mechanical parts
  
Atlas 4 - Infrastructure: 2048x2048px
  - Ladders, bridges, pipes
  - Construction materials
```

**UV Mapping Standards**
- 1:1 texel density for MVP (1m = 512px)
- Consistent texel ratio across atlas
- Minimize UV islands
- 2px padding between atlas elements
- Share UV space for repeated elements

**Material Variations**
- Use tint masks for color variations
- Separate detail maps for wear/aging
- Normal maps for surface detail
- Emissive masks for lit elements

---

## 9. Technical Implementation Notes

### 9.1 Entity Class Hierarchy

#### Core Class Structure

```csharp
// Base entity class (all entities inherit)
public abstract class GameEntity : RigidBody3D
{
    // Core properties
    public EntityID ID { get; protected set; }
    public EntityCategory Category { get; protected set; }
    public float BaseWeight { get; set; }
    public float CurrentWeight { get; protected set; }
    
    // State
    public EntityState CurrentState { get; protected set; }
    public bool IsInteractable { get; set; } = true;
    public bool IsMovable { get; set; } = true;
    
    // Network
    public NetworkNode NetworkSync { get; protected set; }
    public int OwnerPeerID { get; set; } = -1;
    
    // Abstract methods
    public abstract void OnInteract(Player player, InteractionType type);
    public abstract void OnLoad(Dictionary data);
    public abstract Dictionary OnSave();
    
    // Common functionality
    public virtual void UpdateWeight()
    {
        CurrentWeight = BaseWeight + GetContentsWeight();
        Mass = CurrentWeight;
    }
}

// Container entities (storage, transport)
public abstract class ContainerEntity : GameEntity
{
    public Inventory Inventory { get; protected set; }
    public int MaxSlots { get; set; }
    public float MaxVolume { get; set; }
    public float MaxWeight { get; set; }
    
    public virtual bool CanAddItem(Item item)
    {
        return Inventory.HasSpace(item) && 
               (CurrentWeight + item.Weight <= MaxWeight);
    }
}

// Crafting stations
public abstract class CraftingStation : GameEntity
{
    public List<Recipe> AvailableRecipes { get; protected set; }
    public bool IsCrafting { get; protected set; }
    public float CraftProgress { get; protected set; }
    public Recipe CurrentRecipe { get; protected set; }
    
    public abstract void StartCraft(Recipe recipe, Player crafter);
    public abstract void CancelCraft();
}

// Transportation entities
public abstract class TransportEntity : ContainerEntity
{
    public float MaxSpeed { get; set; }
    public float Acceleration { get; set; }
    public bool IsBeingMoved { get; protected set; }
    public EntityBase AttachedTo { get; set; }
    public List<EntityBase> AttachedEntities { get; protected set; }
    
    public abstract void OnPush(Player player, Vector3 direction);
    public abstract void OnPull(Player player, Vector3 direction);
    public abstract bool CanAttach(TransportEntity other);
}
```

#### Category-Specific Classes

```csharp
// Furniture
public class Furniture : GameEntity
{
    public int ComfortLevel { get; set; }
    public bool IsUsable { get; set; } = true;
    public List<Player> CurrentUsers { get; protected set; }
    
    public virtual void OnUse(Player user)
    {
        if (!IsUsable) return;
        CurrentUsers.Add(user);
        // Implementation specific
    }
}

// Infrastructure
public class Infrastructure : GameEntity
{
    public bool IsStructural { get; set; }
    public float Integrity { get; set; } = 100f;
    public List<EntityBase> ConnectedTo { get; protected set; }
    
    public virtual bool CanConnect(Infrastructure other)
    {
        return Vector3.Distance(Position, other.Position) < MaxConnectionDistance;
    }
}
```

### 9.2 Common Components

#### Component Architecture

Entities use a component-based design for modularity:

```csharp
// Inventory Component
public class InventoryComponent : Node
{
    [Export] public int SlotCount = 27;
    [Export] public float MaxWeight = 1000f;
    
    private Item[] slots;
    private float currentWeight;
    
    public event Action<Item> OnItemAdded;
    public event Action<Item> OnItemRemoved;
    
    public bool TryAddItem(Item item, int slot = -1)
    {
        // Implementation
    }
    
    public Item TryRemoveItem(int slot, int quantity = 1)
    {
        // Implementation
    }
}

// Lock Component
public class LockComponent : Node
{
    [Export] public bool IsLocked = false;
    [Export] public int LockStrength = 1;
    
    public List<int> AuthorizedPlayers { get; private set; }
    
    public bool CanAccess(Player player)
    {
        return !IsLocked || AuthorizedPlayers.Contains(player.ID);
    }
    
    public void Lock(Player locker)
    {
        IsLocked = true;
        AuthorizedPlayers.Add(locker.ID);
    }
}

// Durability Component
public class DurabilityComponent : Node
{
    [Export] public float MaxDurability = 100f;
    [Export] public float CurrentDurability = 100f;
    
    public event Action OnBroken;
    
    public void TakeDamage(float damage)
    {
        CurrentDurability -= damage;
        if (CurrentDurability <= 0)
        {
            CurrentDurability = 0;
            OnBroken?.Invoke();
        }
    }
}

// Crafting Component
public class CraftingComponent : Node
{
    [Export] public float CraftSpeed = 1f;
    [Export] public List<Recipe> Recipes { get; set; }
    
    private CraftingQueue queue;
    private Timer craftTimer;
    
    public void StartCraft(Recipe recipe, Player crafter)
    {
        // Implementation
    }
}

// Physics Component (custom physics behaviors)
public class PhysicsComponent : Node
{
    [Export] public bool CanRoll = false;
    [Export] public bool CanFloat = false;
    [Export] public float Buoyancy = 0f;
    
    public void ApplyCustomPhysics()
    {
        var body = GetParent<RigidBody3D>();
        
        if (CanRoll && body.LinearVelocity.Length() > 0.1f)
        {
            // Rolling physics
        }
        
        if (CanFloat && IsInWater())
        {
            body.ApplyCentralForce(Vector3.Up * Buoyancy);
        }
    }
}
```

### 9.3 Variation System

#### Variant Implementation

```csharp
public class EntityVariantSystem : Node
{
    // Variant data structure
    public class VariantData
    {
        public string VariantID;
        public string BaseEntityID;
        public Dictionary<string, object> Overrides;
        public List<string> AddedComponents;
        public List<string> RemovedComponents;
    }
    
    // Create entity with variant
    public GameEntity CreateEntity(string baseID, string variantID = null)
    {
        var baseData = EntityDatabase.Get(baseID);
        var entity = baseData.Instantiate();
        
        if (variantID != null)
        {
            var variant = VariantDatabase.Get(variantID);
            ApplyVariant(entity, variant);
        }
        
        return entity;
    }
    
    private void ApplyVariant(GameEntity entity, VariantData variant)
    {
        // Apply property overrides
        foreach (var kvp in variant.Overrides)
        {
            entity.Set(kvp.Key, kvp.Value);
        }
        
        // Add components
        foreach (var compName in variant.AddedComponents)
        {
            var component = ComponentFactory.Create(compName);
            entity.AddChild(component);
        }
        
        // Remove components
        foreach (var compName in variant.RemovedComponents)
        {
            var component = entity.GetNode(compName);
            component?.QueueFree();
        }
    }
}
```

### 9.4 Network Sync Requirements

#### Network Architecture

```csharp
public class EntityNetworkSync : Node
{
    [Export] public SyncPriority Priority = SyncPriority.Medium;
    [Export] public float ActiveSyncRate = 20f; // Hz
    [Export] public float IdleSyncRate = 1f; // Hz
    [Export] public bool UsePrediction = true;
    
    private Timer syncTimer;
    private NetworkState lastSentState;
    private NetworkState predictedState;
    
    public override void _Ready()
    {
        syncTimer = new Timer();
        syncTimer.WaitTime = 1f / ActiveSyncRate;
        syncTimer.Timeout += OnSyncTimer;
        AddChild(syncTimer);
        syncTimer.Start();
    }
    
    private void OnSyncTimer()
    {
        var currentState = CaptureState();
        
        if (ShouldSync(currentState, lastSentState))
        {
            BroadcastState(currentState);
            lastSentState = currentState;
        }
    }
    
    private NetworkState CaptureState()
    {
        var body = GetParent<RigidBody3D>();
        return new NetworkState
        {
            Position = body.Position,
            Rotation = body.Rotation,
            LinearVelocity = body.LinearVelocity,
            AngularVelocity = body.AngularVelocity,
            CustomData = GetCustomState()
        };
    }
    
    private bool ShouldSync(NetworkState current, NetworkState last)
    {
        if (last == null) return true;
        
        // Position delta check
        if (current.Position.DistanceTo(last.Position) > 0.01f) return true;
        
        // Rotation delta check
        if (current.Rotation.DistanceTo(last.Rotation) > 0.01f) return true;
        
        // Custom data changed
        if (!current.CustomData.Equals(last.CustomData)) return true;
        
        return false;
    }
    
    private void BroadcastState(NetworkState state)
    {
        // RPC call to update clients
        Rpc(nameof(UpdateEntityState), state.Serialize());
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void UpdateEntityState(byte[] serializedState)
    {
        var state = NetworkState.Deserialize(serializedState);
        
        if (UsePrediction && IsLocalPlayerOwner())
        {
            // Reconcile prediction
            ReconcileState(state);
        }
        else
        {
            // Direct application
            ApplyState(state);
        }
    }
}

public enum SyncPriority
{
    Critical, // Immediate sync
    High,     // 20 Hz
    Medium,   // 10 Hz
    Low       // 1 Hz or event-based
}
```

#### Ownership Management

```csharp
public class EntityOwnership : Node
{
    public int CurrentOwner { get; private set; } = -1;
    public bool IsServerOwned => CurrentOwner == 1;
    public bool IsUnowned => CurrentOwner == -1;
    
    public event Action<int> OnOwnershipChanged;
    
    public void RequestOwnership(int peerID)
    {
        if (CurrentOwner == -1 || CurrentOwner == peerID)
        {
            TransferOwnership(peerID);
        }
        else
        {
            // Request from current owner
            RpcId(CurrentOwner, nameof(OnOwnershipRequested), peerID);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void OnOwnershipRequested(int requesterID)
    {
        // Owner decides whether to release
        if (CanReleaseOwnership())
        {
            Rpc(nameof(TransferOwnership), requesterID);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void TransferOwnership(int newOwner)
    {
        var oldOwner = CurrentOwner;
        CurrentOwner = newOwner;
        
        // Update physics authority
        var body = GetParent<RigidBody3D>();
        if (newOwner == Multiplayer.GetUniqueId())
        {
            body.Freeze = false; // Local control
        }
        else
        {
            body.Freeze = true; // Remote controlled
        }
        
        OnOwnershipChanged?.Invoke(newOwner);
    }
}
```

---

## 10. Development Priority Matrix

### 10.1 Critical Path Items (MVP)

These entities are essential for core gameplay and must be implemented first.

| Priority | Entity | Category | Dependencies | Est. Effort |
|----------|--------|----------|--------------|-------------|
| **P0** | Basic Workbench | Crafting | None | 2 days |
| **P0** | Furnace | Crafting | None | 2 days |
| **P0** | Wooden Chest | Storage | None | 1 day |
| **P0** | Hand Cart | Transport | None | 3 days |
| **P0** | Bed | Furniture | None | 1 day |
| **P0** | Anvil | Crafting | Furnace | 2 days |
| **P0** | Door | Furniture | None | 1 day |
| **P0** | Ladder | Infrastructure | None | 1 day |

**Critical Path Total: ~13 days**

### 10.2 High Priority Items (MVP Completion)

Essential for complete MVP experience but can be added after critical path.

| Priority | Entity | Category | Dependencies | Est. Effort |
|----------|--------|----------|--------------|-------------|
| **P1** | Carpentry Table | Crafting | Basic Workbench | 2 days |
| **P1** | Storage Crate | Storage | Wooden Chest | 1 day |
| **P1** | Barrel | Storage | Wooden Chest | 2 days |
| **P1** | Minecart | Transport | Rail System | 3 days |
| **P1** | Rail Segments | Infrastructure | Minecart | 2 days |
| **P1** | Table | Furniture | Carpentry Table | 1 day |
| **P1** | Chair | Furniture | Carpentry Table | 1 day |
| **P1** | Bridge Segments | Infrastructure | None | 2 days |

**High Priority Total: ~14 days**

### 10.3 Medium Priority (Post-MVP Phase 1)

Enhance gameplay depth and variety.

| Priority | Entity | Category | Est. Effort |
|----------|--------|----------|-------------|
| **P2** | Wheelbarrow | Transport | 2 days |
| **P2** | Wagon | Transport | 4 days |
| **P2** | Advanced Workshops | Crafting | 5 days |
| **P2** | Warehouse Shelving | Storage | 2 days |
| **P2** | Lighting Fixtures | Furniture | 3 days |
| **P2** | Scaffolding | Infrastructure | 2 days |

### 10.4 Nice-to-Have (Post-MVP Phase 2)

Advanced features for experienced players.

| Priority | Entity | Category | Est. Effort |
|----------|--------|----------|-------------|
| **P3** | Steam Vehicle | Transport | 8 days |
| **P3** | Automated Workbench | Crafting | 4 days |
| **P3** | Conveyor System | Storage/Infra | 6 days |
| **P3** | Boats | Transport | 5 days |
| **P3** | Factory Machinery | Crafting | 10 days |

### 10.5 Blocker Dependencies

Critical dependencies that must be resolved before dependent entities can be implemented.

```yaml
Physics System (RigidBody3D):
  Blocks: All transportation entities
  Status: Required for MVP
  
Inventory System:
  Blocks: All storage and transport containers
  Status: Required for MVP
  
Crafting System:
  Blocks: All crafting stations
  Status: Required for MVP
  
Network Sync (Entity State):
  Blocks: All movable entities in multiplayer
  Status: Required for MVP
  
Ownership/Authority:
  Blocks: Physics-based entities in multiplayer
  Status: Required for MVP
  
Rail System (Physics Constraints):
  Blocks: Minecart, rail segments
  Status: Required for MVP Phase 2
  
Animal AI System:
  Blocks: Wagon (animal-pulled)
  Status: Post-MVP Phase 1
  
Power/Steam System:
  Blocks: Steam vehicle, powered machinery
  Status: Post-MVP Phase 2
  
Fluid Dynamics:
  Blocks: Pipes, liquid storage
  Status: Post-MVP Phase 2
```

### 10.6 Estimation Guidelines

#### Effort Estimation Scale

| Complexity | Definition | Days Range | Examples |
|------------|------------|------------|----------|
| **Simple** | Static object, basic interaction | 0.5-1 | Chair, ladder, door |
| **Basic** | Container or simple state | 1-2 | Chest, barrel, basic workbench |
| **Moderate** | Complex interaction, physics | 2-4 | Hand cart, furnace, table |
| **Complex** | Multi-state, networking heavy | 4-8 | Minecart, wagon, anvil |
| **Advanced** | Systems integration, automation | 8-15 | Steam vehicle, conveyor system |

#### Estimation Factors

**+ Time Adders**
- New animation requirements: +25%
- Custom UI interface: +20%
- Complex physics interactions: +30%
- Multiplayer synchronization: +25%
- Special visual effects: +15%

**- Time Reducers**
- Similar to existing entity: -20%
- Reuses existing components: -15%
- Simple visual requirements: -10%

#### Buffer Recommendations

- **Art/Asset Creation**: 50% of programming time
- **Testing/Debugging**: 30% of programming time
- **Integration/Polish**: 20% of programming time

**Total Estimate Formula:**
```
Total Days = Programming Days × 2.0 (includes art, testing, polish)
```

---

## 11. Appendices

### Appendix A: Complete Entity Quick Reference

#### MVP Entities (20 total)

| Entity ID | Name | Category | Weight (kg) | Slots | Phase |
|-----------|------|----------|-------------|-------|-------|
| transport_hand_cart_01 | Hand Cart | Transportation | 15+80 | 12 | MVP |
| transport_minecart_01 | Minecart | Transportation | 45+200 | 18 | MVP |
| infrastructure_rail_01 | Rail Segment | Infrastructure | 8 | 0 | MVP |
| crafting_workbench_01 | Basic Workbench | Crafting | 35 | 9 | MVP |
| crafting_furnace_01 | Furnace | Crafting | 80 | 3 | MVP |
| crafting_anvil_01 | Anvil | Crafting | 150 | 4 | MVP |
| crafting_carpentry_01 | Carpentry Table | Crafting | 45 | 7 | MVP |
| storage_chest_01 | Wooden Chest | Storage | 12+200 | 27 | MVP |
| storage_crate_01 | Storage Crate | Storage | 8+300 | 18 | MVP |
| storage_barrel_01 | Barrel | Storage | 15+150 | 12 | MVP |
| furniture_bed_01 | Bed | Furniture | 25 | 0 | MVP |
| furniture_chair_01 | Chair | Furniture | 8 | 0 | MVP |
| furniture_table_01 | Table | Furniture | 20 | 0 | MVP |
| furniture_door_01 | Door | Furniture | 25 | 0 | MVP |
| infrastructure_ladder_01 | Ladder | Infrastructure | 3 | 0 | MVP |
| infrastructure_bridge_01 | Bridge Segment | Infrastructure | 15 | 0 | MVP |

#### Post-MVP Phase 1 Entities (25 total)

| Entity ID | Name | Category | Phase |
|-----------|------|----------|-------|
| transport_wheelbarrow_01 | Wheelbarrow | Transportation | P1 |
| transport_wagon_01 | Wagon | Transportation | P1 |
| transport_chassis_01 | Vehicle Chassis | Transportation | P1 |
| crafting_smithy_01 | Smithy Forge | Crafting | P1 |
| crafting_loom_01 | Loom | Crafting | P1 |
| crafting_alchemy_01 | Alchemy Station | Crafting | P1 |
| crafting_assembly_01 | Assembly Table | Crafting | P1 |
| crafting_power_01 | Power Tools | Crafting | P1 |
| storage_shelving_01 | Warehouse Shelving | Storage | P1 |
| storage_rack_tool_01 | Tool Rack | Storage | P1 |
| storage_icebox_01 | Icebox | Storage | P1 |
| furniture_light_wall_01 | Wall Sconce | Furniture | P1 |
| furniture_light_chandelier_01 | Chandelier | Furniture | P1 |
| furniture_sign_01 | Sign | Furniture | P1 |
| infrastructure_scaffold_01 | Scaffolding | Infrastructure | P1 |

#### Post-MVP Phase 2 Entities (20 total)

| Entity ID | Name | Category | Phase |
|-----------|------|----------|-------|
| transport_steam_01 | Steam Vehicle | Transportation | P2 |
| transport_boat_01 | Small Boat | Transportation | P2 |
| transport_rail_powered_01 | Powered Minecart | Transportation | P2 |
| crafting_auto_01 | Automated Workbench | Crafting | P2 |
| crafting_press_01 | Mechanical Press | Crafting | P2 |
| storage_smart_01 | Smart Chest | Storage | P2 |
| infrastructure_conveyor_01 | Conveyor Belt | Infrastructure | P2 |
| infrastructure_pipe_01 | Fluid Pipe | Infrastructure | P2 |
| infrastructure_power_01 | Power Conduit | Infrastructure | P2 |

### Appendix B: Material Reference

| Material | Density (kg/m³) | Durability | Notes |
|----------|-----------------|------------|-------|
| Wood (Oak) | 750 | 100% | Standard |
| Wood (Pine) | 500 | 80% | Lighter, softer |
| Wood (Hardwood) | 900 | 150% | Dense, durable |
| Iron | 7870 | 300% | Heavy, strong |
| Steel | 7850 | 500% | Premium |
| Stone | 2500 | 400% | Immobile |
| Cloth | 100 | 25% | Fragile |
| Leather | 800 | 150% | Flexible |

### Appendix C: Collision Layer Matrix

| Layer | Transportation | Crafting | Storage | Furniture | Infrastructure | Player | Terrain | Block |
|-------|----------------|----------|---------|-----------|----------------|--------|---------|-------|
| **Transportation** | Collide | - | Collide | Collide | Collide | Collide | Collide | Collide |
| **Crafting** | - | - | - | - | - | Collide | Collide | Collide |
| **Storage** | Collide | - | - | - | - | Collide | Collide | Collide |
| **Furniture** | Collide | - | - | - | - | Collide | Collide | Collide |
| **Infrastructure** | Collide | - | - | - | Collide | Collide | Collide | Collide |
| **Player** | Collide | Collide | Collide | Collide | Collide | - | Collide | Collide |
| **Terrain** | Collide | Collide | Collide | Collide | Collide | Collide | - | - |
| **Block** | Collide | Collide | Collide | Collide | Collide | Collide | - | - |

### Appendix D: Glossary

**Entity**: A non-block object in the game world with custom shape and physics.

**Bounding Box**: The axis-aligned box that contains the entity's geometry.

**Center of Mass**: The point where the entity's mass is concentrated, affecting physics stability.

**Collision Proxy**: Simplified geometry used for physics collision detection.

**LOD (Level of Detail)**: Different versions of a mesh for different viewing distances.

**MVP (Minimum Viable Product)**: The minimum set of features needed for initial release.

**Phase 1/2/3**: Post-MVP development phases with expanding features.

**RigidBody3D**: Godot physics body that responds to forces and collisions.

**Sync Rate**: Frequency of network updates (Hz = times per second).

**Variant**: A modified version of a base entity with different properties.

---

## Document Metadata

- **Author**: AI Development Team
- **Reviewers**: Game Design, Art, Engineering leads
- **Approvals Required**: Technical Lead, Art Director, Game Director
- **Next Review Date**: 2026-03-01
- **Related Documents**:
  - `session-1-technical-architecture/18-physics-collision.md`
  - `archives/00-day3-legacy.md` (archived legacy document)
  - `session-6-prototyping-roadmap/day6-prototyping-roadmap.md`

---

*End of Comprehensive Entity Catalog v1.0.0*
