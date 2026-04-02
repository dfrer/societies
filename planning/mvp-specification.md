# Societies MVP Specification

**Document Purpose**: Unity-first implementation blueprint for a fresh MVP build of Societies  
**Source Basis**: Synthesized from `planning/meta/`, Sessions 1-7, and supporting resource/governance specifications  
**Date**: 2026-04-02  
**Status**: Proposed implementation spec

---

## 1. Executive Summary

### 1.1 What MVP Societies Is

Societies MVP is a **single-world, simulation-first, voxel society sandbox** built in Unity where one human player, plus a small population of autonomous AI citizens, gather resources, craft tools, build structures, trade goods, and sustain a living settlement inside a persistent block-based world.

The MVP is not trying to ship the entire long-term vision. It is intended to prove five things:

1. A **Unity-based authoritative simulation** can run a persistent voxel world at stable frame and tick budgets.
2. **AI agents can act as economic participants**, not set dressing.
3. **Weight, logistics, and terrain modification** create meaningful moment-to-moment gameplay.
4. A **basic settlement economy** can emerge from gathering, crafting, storage, and store-based trading.
5. The core loop of **gather -> transport -> craft -> build -> trade -> sustain** is compelling enough to justify expansion.

### 1.2 MVP Product Goals

- Deliver a playable vertical slice for **solo play first**, with optional local/LAN co-op as a stretch within MVP architecture.
- Preserve the original design philosophy of **AI-human equivalence** at the simulation layer.
- Keep the implementation small enough for a **solo developer or small team** to complete in phases.
- Establish a **clean Unity architecture** that can scale into post-MVP governance, advanced threats, and larger multiplayer.

### 1.3 Core MVP Player Fantasy

The player enters a small persistent voxel world, establishes a homestead, gathers wood/stone/ore, crafts tools and stations, manages encumbrance and transport, interacts with AI citizens who also gather and trade, and slowly bootstraps a functioning local economy with primitive-to-iron-age progression.

### 1.4 Clear Exclusions

The following are explicitly **not MVP**:

- State/federation governance
- Full constitutional editor
- Advanced law engine beyond simple permission/property rules
- Meteor endgame and late threat ladder
- Pollution/climate simulation beyond placeholders/hooks
- Full ecology simulation with predator/prey chains
- Large-scale internet multiplayer
- Multi-server architecture
- Advanced automation, conveyors, vehicles beyond simple carts if time remains
- Rich social AI, factions, gossip, office-holding, and political blocs
- Modern/electrical/space-age tech tiers
- Full narrative/debug visualizers beyond developer tools needed to ship MVP

---

## 2. Design Principles

### 2.1 Principles Carried Forward from Planning

- **Simulation first**: world state exists independently of player camera/UI.
- **Authoritative game state**: one simulation authority validates all changes.
- **AI-human equivalence**: human players and AI agents use the same inventory, tools, economy, and property rules.
- **Non-violent conflict model**: no PvP combat or monster-combat loop in MVP.
- **Logistics matter**: weight and transport are central, not flavor.
- **Persistence matters**: terrain and settlement changes survive reloads.

### 2.2 MVP Adaptation Principles

- Prefer **fewer systems with depth** over many shallow systems.
- Replace broad scope with **tight mechanical interdependence**.
- Avoid engine-specific overengineering until validated in play.
- Keep networking architecture compatible with future dedicated servers, but do not let online multiplayer dominate the MVP.

---

## 3. Technical Stack

### 3.1 Engine and Runtime

- **Engine**: Unity **6.3 LTS**
- **Language**: C#
- **Render Pipeline**: URP
- **Target Platforms**: Windows PC first, Linux dedicated server build second

**Why**:
- Unity 6.3 LTS is the current LTS release as of **April 2, 2026**, which makes it the safest production baseline for a new project.
- URP is the correct fit for a stylized low-poly voxel world with moderate rendering complexity and broad hardware targets.

### 3.2 Multiplayer Stack

- **Primary networking package**: **Netcode for GameObjects (NGO)**
- **Transport**: **Unity Transport**
- **Authority model**: server-authoritative simulation with thin client presentation/prediction

**Why NGO over Mirror/custom for MVP**:
- Official Unity-supported stack
- High-level GameObject workflow matches a small-team MVP
- Easier path to local host, LAN testing, and dedicated-server builds
- Lower implementation risk than writing a custom replication layer in the first milestone

**Important constraint**:
- NGO should be treated as the **replication shell**, not the simulation architecture.
- Core simulation logic must live in plain C# assemblies/services so it can run:
  - in single-player
  - in host mode
  - in a headless dedicated server
  - in automated tests

### 3.3 Persistence

- **Primary MVP database**: SQLite
- **Persistence model**:
  - SQLite database for world/player/agent/meta state
  - chunk save blobs for terrain
  - append-only event log for debugging and recovery

### 3.4 Server Approach

- **Single-player default**: local authoritative host mode using the same simulation code
- **Optional local multiplayer**: host/client or LAN session
- **Dedicated server target**: Unity headless server build, same simulation assemblies, same SQLite schema for MVP

### 3.5 Unity Packages

- `com.unity.netcode.gameobjects`
- `com.unity.transport`
- Input System
- Cinemachine
- Burst and Collections where helpful for voxel/meshing jobs
- Test Framework

### 3.6 Project Structure

Recommended top-level Unity layout:

```text
Assets/
  Societies/
    Runtime/
      Core/
      Simulation/
      World/
      Inventory/
      Crafting/
      Economy/
      AI/
      Persistence/
      Networking/
      UI/
    Authoring/
      ScriptableObjects/
      Prefabs/
      Materials/
    Scenes/
    Tests/
Server/
Packages/
ProjectSettings/
```

---

## 4. MVP Scope Summary

### 4.1 Included Feature Set

- Small procedural voxel world
- First/third-person player controller
- Targeting, mining, and block placement
- Tool progression: stone -> iron -> steel hooks, with steel likely partial in MVP
- Inventory, hotbar, storage, encumbrance
- Core resource gathering: wood, dirt, stone, coal, iron, copper, food basics
- Primitive and early metal crafting loop
- Placeable workstations and storage
- Basic AI agents:
  - satisfy needs
  - gather
  - craft simple goods
  - carry inventory
  - buy/sell via stores
- Simple economy:
  - credits
  - stores/listings
  - direct transactions
  - basic price belief logic for AI
- Personal claims and permission checks
- Day/night cycle and simple weather presentation
- Save/load persistence
- Local analytics and debugging tools

### 4.2 Deliberately Reduced from Original Vision

- Governance is reduced to **property ownership and simple settlement permissions**
- Ecology is reduced to **resource spawning/regrowth and light biome identity**
- AI is reduced to **need-driven labor and trade**
- Multiplayer is reduced to **optional local authority-based co-op**
- Progression ends around **early metallurgy / settlement establishment**

---

## 5. Core Gameplay Loop

### 5.1 Moment-to-Moment Loop

1. Move through the world and scan for resources.
2. Target a block or entity.
3. Use the correct tool to harvest.
4. Manage encumbrance and limited carrying capacity.
5. Transport materials to storage or a workstation.
6. Craft better tools, blocks, and stations.
7. Build shelter, storage, and production infrastructure.
8. Trade surpluses with AI citizens.
9. Improve throughput and settlement efficiency.

### 5.2 Session Loop

1. Log into a persistent world.
2. Check settlement state, stores, and available resources.
3. Pick a short-term project:
   - acquire iron
   - expand shelter
   - build storage
   - restock food
   - produce goods for sale
4. Complete a visible settlement improvement.
5. End the session with the world in a better operating state than it started.

### 5.3 Success Condition for MVP

The MVP succeeds if a player can reliably create a self-sustaining settlement loop with visible AI participation:

- food and tools circulate
- storage and workstations matter
- logistics slow down careless play
- AI can survive, work, and trade without constant handholding

---

## 6. World Specification

### 6.1 World Scale

Adopt the planning intent, trimmed for MVP production:

- **Block size**: 1m x 1m x 1m
- **Chunk size**: 16 x 16 x 256 blocks
- **Vertical range**: Y = -200 to +55 usable terrain, top cap at +56
- **MVP world surface target**: approximately **0.5 km²**
- **Playable world side length**: about **704m x 704m**

### 6.2 Biomes

Use the planning MVP biome trio:

- Boreal Forest
- Subtropical Desert
- Jungle

Each biome must differ in:

- surface blocks
- vegetation set
- resource weighting
- ambient lighting/fog/color grading
- food and wood availability

### 6.3 Terrain Generation

Use a deterministic layered generator:

1. Continental height
2. Erosion/detail
3. Temperature map
4. Humidity map
5. Biome assignment
6. Surface layer painting
7. Cave pass
8. Ore/resource placement

### 6.4 Geological Layers

Preserve the core depth-based material logic:

- 0 to -10m: soil/subsurface
- -10 to -30m: sedimentary / coal traces
- -30 to -60m: shallow bedrock / coal / copper / trace iron
- -60 to -100m: iron-focused depth
- -100 to -150m: gold/gem depth
- -150 to -200m: very rare materials, mostly post-MVP hooks

### 6.5 Resource Rules

- Wood, plants, and some food are renewable
- Stone, coal, iron, copper are effectively finite within a given local region
- Rare/deep resources exist mostly as progression hooks, not full MVP dependency
- Resource density should create meaningful travel and settlement choices

---

## 7. Detailed Feature Specifications

## 7A. Voxel World

### What It Does

Represents all terrain and most structural building pieces in a destructible, persistent 3D grid.

### Player Interactions

- Mine blocks
- Place blocks
- inspect target block
- build shelter and terrain modifications

### Core Data Structures

```csharp
public struct BlockData
{
    public ushort Id;
    public byte Metadata;
    public byte State;
}

public struct ChunkCoord
{
    public int X;
    public int Z;
}

public sealed class VoxelChunk
{
    public ChunkCoord Coord;
    public NativeArray<BlockData> Blocks; // 16*16*256
    public bool IsDirty;
    public ulong LastModifiedTick;
}
```

### Key Algorithms

- deterministic coordinate conversion
- face culling
- greedy meshing
- dirty-region rebuild queue
- chunk streaming by radius around simulation-relevant actors

### MVP Constraints

- no infinite terrain
- no fully deformable fluids
- no dynamic structural collapse simulation beyond simple support checks

## 7B. Player Movement and Interaction

### What It Does

Provides grounded traversal and precise world interaction for harvesting, building, storage, and crafting.

### Player Interactions

- walk
- sprint
- crouch
- jump
- target blocks and entities with center-screen raycast
- interact via context-sensitive action keys

### Target Controls

- `WASD`: move
- `Shift`: sprint
- `Ctrl`: crouch / precision modify
- `Space`: jump
- `LMB`: mine / primary action
- `RMB`: place / secondary action
- `E`: interact
- `1-9`: hotbar
- `Tab` or `I`: inventory

### Movement Numbers

- walk speed: 3.0 m/s
- sprint speed: 6.0 m/s
- interaction distance: 5.0m max
- step height: 0.5m
- slope limit: 45 degrees

### Data Structures

```csharp
public struct PlayerMotorState
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Quaternion Rotation;
    public float Stamina;
    public float Encumbrance;
    public bool IsGrounded;
}
```

### Key Algorithms

- character motor with authoritative reconciliation
- center-screen raycast targeting
- target priority resolution between blocks, entities, and interactables
- encumbrance-modified speed and stamina cost

## 7C. Inventory and Encumbrance

### What It Does

Provides finite carrying capacity, slot management, storage transfer, and the logistical friction central to Societies.

### Player Interactions

- pick up and stack items
- move items between inventory, hotbar, storage, carts, and workstations
- see weight feedback
- become encumbered when overloaded

### MVP Rules

- 64-slot player inventory
- 100 kg hard cap
- 50 kg comfortable baseline
- no pickup past hard cap
- tools are non-stackable

### Data Structures

```csharp
public struct ItemStack
{
    public int ItemId;
    public int Quantity;
    public float Quality;
    public int Durability;
}

public sealed class InventoryState
{
    public ItemStack?[] Slots = new ItemStack?[64];
    public float CurrentWeightKg;
    public float CapacityKg;
}
```

### Key Algorithms

- stack merge/split
- item weight summation
- capacity validation
- encumbrance band calculation

### Encumbrance Bands

- 0-25%: no penalty
- 25-50%: light penalty
- 50-75%: major move penalty, sprint restricted
- 75-100%: severe penalty, jump restricted

## 7D. Resource Gathering

### What It Does

Turns terrain and world objects into item flows that feed crafting and trade.

### MVP Gatherables

- trees / logs
- dirt
- sand
- clay
- stone
- coal
- copper ore
- iron ore
- berries / crops / basic food inputs

### Tool Matrix

- axe: wood
- shovel: dirt, sand, clay, gravel
- pickaxe: stone and ore
- hoe/sickle: farming and plant harvest

### Data Structures

```csharp
public struct HarvestRule
{
    public int TargetBlockId;
    public ToolFamily RequiredTool;
    public int MinimumTier;
    public float BaseSeconds;
    public int YieldItemId;
    public Vector2Int YieldRange;
}
```

### Key Algorithms

- tool compatibility check
- mining time = base time * material hardness / tool efficiency
- yield roll with skill modifiers
- durability loss per successful action

## 7E. Crafting and Production

### What It Does

Converts raw resources into tools, blocks, food, storage, and settlement infrastructure.

### MVP Production Chain

- wood -> planks
- stone -> bricks/basic structural forms
- ore -> ingots
- ingots + wood -> tools
- food inputs -> simple meals
- planks/stone/ingots -> stations, storage, doors, furniture basics

### MVP Stations

- campfire
- workbench
- furnace/smelter
- anvil
- carpentry table

### Data Structures

```csharp
public sealed class RecipeDefinition
{
    public int Id;
    public string Name;
    public int RequiredSkillLevel;
    public StationType Station;
    public ToolFamily? RequiredTool;
    public float CraftSeconds;
    public IngredientDef[] Inputs;
    public ProductDef[] Outputs;
}
```

### Key Algorithms

- recipe filtering by station/tool/skill
- crafting queue execution
- timed production completion
- output quality modifier hooks

### MVP Recipe Scope

Keep the shipped recipe set intentionally small:

- 15-25 building material recipes
- 8-12 tool recipes
- 8-10 food recipes
- 10-15 workstation/storage/furniture recipes

## 7F. Building and Placeable Entities

### What It Does

Lets players and AI create practical infrastructure from both blocks and non-voxel entities.

### Voxel Placements

- walls
- floors
- roofs
- stairs
- structural blocks

### Entity Placements

- workbench
- furnace
- anvil
- chest / crate / barrel
- bed
- door
- hand cart

### Block vs Entity Rule

Use blocks for:

- repeated structural pieces
- terrain-aligned construction
- high-volume placement

Use entities for:

- storage with inventory
- animated stations
- movable logistics tools
- interactables with complex state

### Key Algorithms

- ghost placement preview
- occupancy/collision validation
- support/permission validation
- persistence registration

## 7G. Basic AI Agents

### What It Does

Creates autonomous citizens who can sustain themselves and participate in the economy using the same core systems as players.

### MVP AI Scope

Each AI agent can:

- perceive nearby resources, stations, stores, and actors
- maintain hunger, energy, comfort, and inventory needs
- choose simple goals
- gather resources
- return home/storage
- craft simple items
- buy and sell basic goods

### Excluded AI Scope

- complex faction politics
- deep gossip/narrative simulation
- office-holding
- advanced constitutional reasoning
- rich social drama

### Data Structures

```csharp
public struct AgentNeeds
{
    public float Hunger;
    public float Energy;
    public float Social;
    public float Prosperity;
}

public sealed class AgentBrainState
{
    public AgentNeeds Needs;
    public GoalType CurrentGoal;
    public ActionPlan CurrentPlan;
    public PriceBelief[] PriceBeliefs;
    public InventoryState Inventory;
}
```

### Decision Model

Use a simplified version of the planned utility architecture:

1. Perceive
2. Update needs
3. Score available goals
4. Choose top goal
5. Build short action plan
6. Execute
7. Learn from trade/harvest outcomes

### MVP Goal Set

- eat
- rest
- gather wood
- gather stone
- gather ore
- craft tool
- store resources
- buy food
- sell surplus
- move home / idle recover

### Performance Rules

- target 20-25 active agents in MVP
- average processing budget <2ms/agent at full detail
- LOD frequency reduction for distant agents

### Simplified Pseudocode

```csharp
GoalType SelectGoal(AgentBrainState brain)
{
    if (brain.Needs.Hunger > 80) return GoalType.AcquireFood;
    if (brain.Needs.Energy < 20) return GoalType.Rest;
    if (MissingPrimaryTool(brain)) return GoalType.CraftTool;
    if (InventoryIsFull(brain)) return GoalType.StoreGoods;
    if (CanProfitFromSale(brain)) return GoalType.SellSurplus;
    return GoalType.GatherPrimaryResource;
}
```

## 7H. Basic Economy

### What It Does

Enables asynchronous local trade between player and AI citizens without requiring a full macroeconomic simulation.

### MVP Economy Components

- credits currency
- personal inventories
- store entities with listings
- AI price beliefs
- buy/sell transactions
- transaction history

### Excluded Economy Scope

- banks
- loans
- fiat currencies
- futures/speculation
- full taxation
- cross-jurisdiction exchange

### Data Structures

```csharp
public struct Listing
{
    public Guid ListingId;
    public int ItemId;
    public int Quantity;
    public int UnitPrice;
    public Guid SellerActorId;
    public ListingType Type; // Sell or Buy
}

public struct TransactionRecord
{
    public Guid BuyerId;
    public Guid SellerId;
    public int ItemId;
    public int Quantity;
    public int UnitPrice;
    public ulong Tick;
}
```

### Pricing Rules

- player listings are manual
- AI uses simple mean +/- uncertainty price beliefs
- market UI shows recent transaction band
- no complex global clearing market in MVP

### Key Algorithms

- listing match
- affordability check
- quantity partial fill
- price-belief update after transaction

## 7I. Personal Claims and Permissions

### What It Does

Provides the MVP substitute for the broader governance system: ownership, access control, and protected build/mining zones.

### MVP Scope

- personal claim creation
- claim fee
- claim boundary rendering
- owner permissions
- guest/ally permissions
- deny mining/building/open-container if unauthorized

### Excluded Governance Scope

- towns/states/federations as playable systems
- dynamic law authoring
- elections
- constitutions

### Data Structures

```csharp
public struct ClaimVolume
{
    public Guid ClaimId;
    public Guid OwnerId;
    public BoundsInt Bounds;
}

public struct ClaimPermissions
{
    public bool CanBuild;
    public bool CanMine;
    public bool CanOpenContainers;
    public bool CanUseStations;
}
```

### Key Algorithms

- point-in-claim lookup
- overlap validation
- action authorization per targeted block/entity

## 7J. Time, Day/Night, and Weather

### What It Does

Creates temporal rhythm and basic environmental atmosphere.

### MVP Scope

- 24-hour day cycle
- sunrise/day/sunset/night lighting
- simple weather states:
  - clear
  - rain
  - overcast
  - sandstorm/fog variant by biome

### Gameplay Effects

- nighttime visibility reduction
- minor crop/growth modifiers
- atmospheric changes only; no full climate model

### Data Structures

```csharp
public struct TimeState
{
    public int DayNumber;
    public float TimeOfDay01;
    public SeasonType Season;
    public WeatherType Weather;
}
```

---

## 8. Technical Specifications

### 8.1 Performance Targets

#### Client

- 60 FPS target on mid-range desktop at normal view distance
- 30 FPS minimum under heavy local load
- <10s initial world load

#### Simulation

- 20 TPS target
- 50ms tick window
- 40ms target usage ceiling
- 10ms safety margin

#### AI

- 20-25 agents active in MVP
- <2ms/agent at high-detail evaluation
- amortized and LOD-reduced updates for distant agents

#### World

- 9 active chunks around local player as the minimum loaded gameplay ring
- larger visual ring allowed for rendering cache if budgets permit

### 8.2 Networking Targets

For optional co-op MVP:

- 2-4 players local/LAN target
- 8-player architecture ceiling, not a shipping promise
- latency tolerance goal: <100ms on LAN/good internet

### 8.3 World Size

- one world per save/session
- 0.5 km² surface
- 256 vertical blocks
- deterministic seed-driven generation

### 8.4 Save System

MVP persistence layers:

1. **World metadata**
2. **Actor state**: player, AI, entities
3. **Terrain chunk data**
4. **Store/listing/transaction data**
5. **Optional event log**

### 8.5 Save Frequency

- chunk dirty-save batch every 30 seconds
- critical state flush on quit, sleep, or scene shutdown
- rolling backups every 10 minutes of playtime

---

## 9. Architecture

### 9.1 High-Level Layering

```text
Presentation Layer
  Camera, input, UI, audio, VFX

Gameplay Facade Layer
  PlayerController, interaction orchestration, tool usage, build mode

Simulation Layer
  Tick loop, world state, agents, economy, crafting, permissions, time

Persistence Layer
  SQLite repositories, chunk serialization, save/load coordinator

Networking Layer
  NGO replication, RPC/commands, ownership, connection/session flow
```

### 9.2 Component-Based Runtime Model

Use classic Unity components only at the scene edge. Use plain C# models for simulation state.

Recommended split:

- `MonoBehaviour` for input, camera, view, and scene lifecycle
- plain C# services for world, AI, economy, crafting, save/load
- scene entities as thin shells around authoritative model state

### 9.3 Key Systems and Responsibilities

**SimulationClock**
- fixed tick cadence
- phase ordering
- over-budget telemetry

**WorldSystem**
- chunk streaming
- block read/write
- world queries

**MeshingSystem**
- dirty chunk queue
- background mesh generation
- collider rebuild coordination

**InteractionSystem**
- raycast targeting
- mining/building commands
- permission checks

**InventorySystem**
- stacks, transfers, weight

**CraftingSystem**
- station recipes
- progress queues

**AISystem**
- perception
- goal selection
- plan execution

**EconomySystem**
- store listings
- transactions
- AI price beliefs

**ClaimSystem**
- claim creation
- overlap/lookup
- permissions

**PersistenceSystem**
- repositories
- save scheduling
- chunk blobs

**NetworkSessionSystem**
- host/client/dedicated startup
- command routing
- snapshot and entity sync orchestration

### 9.4 Data Flow

#### Player Action Flow

1. Client input
2. Local interaction preview
3. Send command to authority
4. Authority validates:
   - range
   - tool
   - permissions
   - inventory/weight
5. Simulation mutates world state
6. Dirty systems flagged
7. Replication updates sent
8. Save queue updated

#### AI Action Flow

1. Tick bucket selected
2. Agent perceives nearby context
3. Agent scores goals
4. Agent executes action
5. State mutates
6. Economy/inventory/world updated
7. Replication if player-visible

---

## 10. Networking Architecture

### 10.1 MVP Networking Model

Even in single-player, the architecture assumes one authority.

Modes:

- **Solo mode**: local host authority
- **Co-op mode**: one host authority, peers as clients
- **Dedicated server mode**: headless authority build

### 10.2 Authority Rules

- world writes only on authority
- inventory writes only on authority
- agent decisions only on authority
- store transactions only on authority
- clients may predict only cosmetic/local movement response

### 10.3 What Is Replicated

- player transforms
- visible AI transforms and states
- inventory diffs for owning player
- changed entity states
- chunk deltas near relevant clients
- time/weather state

### 10.4 What Is Not Replicated Every Tick

- full inventories of every actor
- entire chunk payloads outside interest area
- hidden AI internals
- full transaction logs

### 10.5 Interest Management

Replicate based on:

- player position
- visible chunk ring
- nearby interactables
- active store/claim UI contexts

---

## 11. Save and Data Model

### 11.1 SQLite Tables

Minimum MVP tables:

- `worlds`
- `players`
- `agents`
- `chunks`
- `chunk_modifications`
- `entities`
- `inventories`
- `claims`
- `stores`
- `listings`
- `transactions`
- `save_events`

### 11.2 Chunk Persistence Strategy

- serialize chunk blocks into compact binary blobs
- compress with RLE + fast secondary compression
- save only dirty chunks after generation
- store modified-block deltas when cheaper than full-chunk rewrite

### 11.3 Event Logging

MVP event log scope:

- block broken/placed
- crafting started/completed
- trade completed
- claim created/updated
- agent spawned/despawned

Use it for:

- debugging
- future replay support
- rollback assistance

---

## 12. Content Scope

### 12.1 MVP Item Families

- raw resources
- processed materials
- tools
- food
- building pieces
- placeable stations
- storage containers
- simple furniture
- currency

### 12.2 MVP Tool Families

- axe
- pickaxe
- shovel
- hoe
- sickle
- hammer
- saw
- knife

### 12.3 MVP Entity Set

Ship a narrow entity set:

- hand cart
- basic workbench
- furnace
- anvil
- carpentry table
- wooden chest
- crate
- barrel
- bed
- door
- ladder

### 12.4 MVP AI Professions

- gatherer
- woodcutter
- miner
- crafter
- farmer/food worker
- merchant

---

## 13. Development Phases

### Phase 1: Foundation

Goals:

- Unity project bootstrap
- core fixed-tick simulation shell
- chunk generation and streaming
- meshing and collision
- player controller
- targeting and block interaction
- SQLite save/load skeleton

Exit criteria:

- player can load a world, walk, mine, place blocks, and save/reload

### Phase 2: Resources, Inventory, and Crafting

Goals:

- inventory
- weight/encumbrance
- item database
- resource gathering rules
- recipes
- stations
- storage containers

Exit criteria:

- player can gather, craft tools, build stations, and manage storage/logistics

### Phase 3: Basic AI

Goals:

- agent spawning
- needs model
- simplified utility goal system
- pathfinding and workstation usage
- basic gather/craft/store loops

Exit criteria:

- 20+ agents survive and perform useful economic actions

### Phase 4: Basic Economy

Goals:

- currency
- stores/listings
- transaction processing
- AI buying/selling
- basic market UI

Exit criteria:

- player can sell goods to AI and buy necessities from AI-operated stores

### Phase 5: Property, Co-op, Polish

Goals:

- personal claims
- permission system
- optional LAN/local multiplayer
- day/night and basic weather
- onboarding
- balancing and bug fixing

Exit criteria:

- stable MVP build with repeatable 1-3 hour session loop

---

## 14. Post-MVP Roadmap

### 14.1 First Expansion Layer

- richer AI professions and longer plans
- town formation
- basic law templates
- more biomes and ecological interactions
- carts/rail logistics expansion
- deeper economy instrumentation

### 14.2 Governance Layer

- constitutions
- voting UI
- election schedules
- town treasury
- zoning
- AI voting and faction behavior
- 3D jurisdiction beyond personal claims

### 14.3 Progression and Threat Layer

- meteor preparation arc
- research tree
- iron -> steel -> electrical progression
- environmental damage and cleanup
- richer long-session stakes

### 14.4 Multiplayer Layer

- dedicated server as standard mode
- online hosting workflow
- larger player counts
- improved interest management
- moderation/admin tools

### 14.5 Full Vision Layer

- state/federation governance
- climate and pollution simulation
- deep social memory/gossip
- advanced automation
- late-game planetary infrastructure

---

## 15. Risks and Mitigations

### 15.1 Technical Risks

**Risk**: Voxel rendering/collision becomes the bottleneck.  
**Mitigation**:
- greedy meshing from day one
- async rebuild queue
- hard world-size cap in MVP
- strict chunk interest radius

**Risk**: Unity scene objects become the simulation model.  
**Mitigation**:
- enforce plain C# simulation core
- keep GameObjects as views/controllers only

**Risk**: NGO assumptions leak into business logic.  
**Mitigation**:
- command/event interface between simulation and replication
- deterministic-ish simulation services independent of `NetworkBehaviour`

**Risk**: SQLite writes stall gameplay.  
**Mitigation**:
- async save batching
- dirty chunk queues
- incremental backups

### 15.2 Design Risks

**Risk**: Game feels like a generic voxel survival prototype.  
**Mitigation**:
- prioritize AI labor/trade before extra content
- keep encumbrance/logistics visible
- ship player-AI economic dependency early

**Risk**: AI feels fake or useless.  
**Mitigation**:
- narrow AI goals
- make agents visibly produce, consume, and transact
- add debug overlays for action reasoning

**Risk**: Scope creep from full-vision governance and ecology.  
**Mitigation**:
- property/claims only in MVP
- no federation, no full law editor
- post-MVP hooks but not implementation

### 15.3 Production Risks

**Risk**: Solo developer time gets consumed by network polish too early.  
**Mitigation**:
- solo-first product target
- local-host architecture
- online multiplayer as optional layer on top of finished single-player loop

**Risk**: Too many content assets required.  
**Mitigation**:
- low-poly modular style
- small entity list
- reusable block palette

---

## 16. Recommended Implementation Notes

### 16.1 Unity-Specific Guidance

- Use **ScriptableObjects** for items, blocks, recipes, station defs, and biome defs.
- Use **Burst/Jobs selectively** for meshing and terrain preprocessing, not for every system on day one.
- Keep simulation tick separate from Unity render/update loops.
- Use **URP** with a single material atlas for blocks.

### 16.2 Testing Strategy

- unit test simulation services
- play mode test chunk load/save and claim enforcement
- soak test 20-agent worlds for several in-game days
- regression test save/load integrity across terrain changes and transactions

### 16.3 MVP Acceptance Checklist

- [ ] world generation deterministic by seed
- [ ] chunk save/load stable
- [ ] player mine/place loop functional
- [ ] inventory and encumbrance clearly felt
- [ ] 20+ AI agents survive and work
- [ ] trading loop functional
- [ ] claim permissions prevent unauthorized edits
- [ ] stable 20 TPS simulation target met in representative scenes
- [ ] stable 60 FPS client target met on target hardware

---

## 17. Final Scope Statement

The Unity MVP of Societies is a **persistent voxel settlement simulator**, not yet a full civilization simulator. It should prove that a small, authoritative Unity implementation can combine destructible terrain, realistic logistics, useful AI citizens, and a functioning local economy into a coherent and expandable foundation.

If the MVP succeeds, the project can responsibly expand toward governance, environmental systems, advanced threats, and larger-scale multiplayer. If it fails, it should fail early with clean architectural seams and clear evidence about what part of the vision did not validate.
