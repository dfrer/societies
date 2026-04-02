# Societies Technical Constants

**Status**: SINGLE SOURCE OF TRUTH  
**Last Updated**: 2026-02-01  
**Scope**: All numerical constants for Societies game  
**Maintained By**: All planning sessions (1-7)

---

## How to Use This Document

This document contains ALL numerical constants referenced throughout the Societies planning documents. When implementing features:

1. **Reference this document first** - All numbers here supersede other documents
2. **Flag conflicts** - If you find discrepancies between this document and others, flag for resolution
3. **Propose changes** - Submit PRs to update this document when tuning gameplay
4. **Include source** - When adding new constants, cite the originating session/document

---

## 1. Performance Budgets

### Core Tick System

```csharp
// Tick rate and timing
public const int TICK_RATE = 20;                                  // Ticks per second
public const double TICK_INTERVAL_MS = 50.0;                      // Milliseconds per tick
public const double TICK_INTERVAL_SECONDS = 0.05;                 // Seconds per tick
public const int TICKS_PER_MINUTE = 1200;                         // 20 TPS × 60 seconds
public const int TICKS_PER_HOUR = 72000;                          // 20 TPS × 3600 seconds
public const int TICKS_PER_DAY = 1728000;                         // 20 TPS × 86400 seconds
// Source: Session 1 - 02-client-server-architecture.md, 04-performance-scalability.md

// Tick rate variability
public const int TICK_RATE_MIN = 10;                              // Minimum TPS under heavy load
public const int TICK_RATE_MAX = 30;                              // Maximum TPS for smooth periods
public const int TICK_RATE_TARGET = 20;                           // Normal operating TPS
// Source: Session 1 - 01-architecture-overview.md
```

### Agent Limits

```csharp
// AI agent population scaling
public const int AGENTS_MVP = 25;                                 // Minimum Viable Product target
public const int AGENTS_POST_MVP = 100;                           // Full deployment target
public const int AGENTS_ABSOLUTE_MAX = 100;                       // Hard cap regardless of server
public const int AGENTS_BUCKET_COUNT = 5;                         // Processing buckets for amortization
// Source: Session 1 - 04-performance-scalability.md, Session 2 - 01-core-ai-architecture.md

// Per-agent performance budget
public const float PER_AGENT_BUDGET_MS = 2.0f;                    // Maximum milliseconds per agent per tick
public const float PER_AGENT_AMORTIZED_BUDGET_MS = 1.5f;          // Average with bucketing
// Source: Session 1 - 02-client-server-architecture.md, Session 2 - 01-core-ai-architecture.md
```

### Player Limits

```csharp
// Concurrent player scaling
public const int PLAYERS_MVP = 8;                                 // MVP concurrent players
public const int PLAYERS_POST_MVP = 20;                           // Post-MVP small server
public const int PLAYERS_MEDIUM = 50;                             // Medium server (Eco validated)
public const int PLAYERS_LARGE = 100;                             // Large server (stretch goal)
// Source: Session 1 - 04-performance-scalability.md

// Hardware requirements by scale
public const int SERVER_CPU_MVP = 4;                              // CPU cores for MVP
public const int SERVER_RAM_MVP_GB = 8;                           // RAM for MVP (4-8 players)
public const int SERVER_CPU_LARGE = 12;                           // CPU cores for 50-100 players
public const int SERVER_RAM_LARGE_GB = 64;                        // RAM for 50-100 players
// Source: Session 1 - 04-performance-scalability.md
```

### Entity Limits

```csharp
// Total entity capacity
public const int MAX_ENTITIES_MVP = 2000;                         // MVP entity limit
public const int MAX_ENTITIES_POST_MVP = 10000;                   // Post-MVP entity limit

// Entity type breakdown (MVP scale)
public const int MAX_STATIC_ENTITIES = 3000;                      // Buildings, resources
public const int MAX_SIMPLE_DYNAMIC = 1500;                       // Plants, basic animals
public const int MAX_COMPLEX_AGENTS = 20;                         // AI agents with full AI
// Source: Session 1 - 01-architecture-overview.md, 04-performance-scalability.md
```

### Bandwidth Budgets

```csharp
// Per-player bandwidth (MVP: 20-25 agents)
public const float BANDWIDTH_PER_PLAYER_MVP_KBPS = 32.0f;         // Kilobytes per second per player
public const float BANDWIDTH_PER_PLAYER_POST_MVP_KBPS = 112.0f;   // Full scale bandwidth

// Server upload requirements
public const float SERVER_UPLOAD_MVP_KBPS = 256.0f;               // 32 KB/s × 8 players
public const float SERVER_UPLOAD_20PLAYERS_MBPS = 2.24f;          // 112 KB/s × 20 players (MB/s)
public const float SERVER_UPLOAD_50PLAYERS_MBPS = 5.6f;           // 112 KB/s × 50 players (MB/s)
public const float SERVER_UPLOAD_100PLAYERS_MBPS = 11.2f;         // 112 KB/s × 100 players (MB/s)
// Source: Session 1 - 04-performance-scalability.md

// Bandwidth components (MVP)
public const float BANDWIDTH_AGENT_POSITIONS_KBPS = 16.0f;        // 20 TPS × 20 agents × 40 bytes
public const float BANDWIDTH_STATE_UPDATES_KBPS = 0.5f;           // Batched reliable RPCs
public const float BANDWIDTH_SNAPSHOTS_KBPS = 2.5f;               // 5 KB every 2 seconds
public const float BANDWIDTH_CHAT_COMMANDS_KBPS = 2.0f;           // Text + protocol overhead
public const float BANDWIDTH_PROTOCOL_OVERHEAD_KBPS = 7.0f;       // ENet headers (~22% overhead)
// Source: Session 1 - 04-performance-scalability.md
```

### Memory Budgets

```csharp
// Server RAM allocation (8GB system)
public const int SERVER_RAM_GODOT_HEADLESS_MB = 400;              // 270-500 MB for Godot process
public const int SERVER_RAM_POSTGRESQL_MB = 1536;                 // 1-2 GB for database
public const int SERVER_RAM_OS_MB = 1536;                         // 1-2 GB for operating system
public const int SERVER_RAM_NETWORK_MB = 350;                     // 200-500 MB for network buffers
public const int SERVER_RAM_SAFETY_MARGIN_MB = 3072;              // 2-4 GB safety margin
// Source: Session 1 - 04-performance-scalability.md

// Per-entity memory
public const float MEMORY_PER_AGENT_KB = 8.5f;                    // Complex agent with AI (~8KB)
public const float MEMORY_PER_STATIC_ENTITY_KB = 1.5f;            // Static entity (building, resource)
public const int MEMORY_AGENT_STATE_BYTES = 32;                   // AgentState struct size
public const int MEMORY_PERSONALITY_TRAITS_BYTES = 15;            // 15 traits × 1 byte each
// Source: Session 1 - 02-client-server-architecture.md

// Client memory (graphical mode)
public const int CLIENT_RAM_BASE_MB = 100;                        // Base Godot engine
public const int CLIENT_RAM_RENDERING_MB = 350;                   // Vulkan rendering (200-500 MB)
public const int CLIENT_RAM_TEXTURES_MB = 750;                    // Textures/meshes (500 MB - 1 GB)
public const int CLIENT_RAM_ENTITY_CACHE_MB = 50;                 // Entity state cache (100 agents × 500 KB)
public const int CLIENT_RAM_UI_MB = 100;                          // UI/Overlays
public const int CLIENT_RAM_TOTAL_MAX_MB = 3000;                  // ~1-3 GB total client RAM
// Source: Session 1 - 02-client-server-architecture.md
```

### CPU Budgeting

```csharp
// CPU utilization targets
public const float CPU_DEFAULT_PERCENT = 0.25f;                   // 25% default utilization
public const float CPU_MAX_PERCENT = 0.75f;                       // 75% maximum recommended
public const float CPU_CRITICAL_PERCENT = 0.90f;                  // 90% = emergency reduction

// Per-tick budget allocation (50ms tick)
public const double CPU_AI_BUDGET_MS = 20.0;                      // 40% - Agent AI (highest priority)
public const double CPU_PHYSICS_BUDGET_MS = 10.0;                 // 20% - Physics & movement
public const double CPU_GAMEPLAY_BUDGET_MS = 15.0;                // 30% - Crafting, building, trading
public const double CPU_SIMULATION_BUDGET_MS = 5.0;               // 10% - Ecosystem, weather, economy
// Source: Session 1 - 02-client-server-architecture.md
```

### Database Performance

```csharp
// PostgreSQL query budgets
public const float DB_QUERY_TIME_BUDGET_MS = 1.0f;                // Maximum query time budget
public const float DB_QUERY_TYPICAL_MS = 0.5f;                    // Typical query time with GIN indexes
public const float DB_BATCH_INTERVAL_SECONDS = 5.0f;              // Batch writes every 5 seconds
public const int DB_CONNECTION_POOL_MIN = 10;                     // Minimum connection pool size
public const int DB_CONNECTION_POOL_MAX = 100;                    // Maximum connection pool size
// Source: Session 1 - 03-data-persistence.md

// Event log storage
public const int EVENT_LOG_RETENTION_DAYS_FULL = 30;              // Full event log retention
public const int EVENT_LOG_RETENTION_DAYS_HOURLY = 90;            // Hourly snapshots retention
public const int EVENT_LOG_SNAPSHOT_INTERVAL_MINUTES = 15;        // Full snapshot every 15 minutes
public const int EVENT_LOG_SNAPSHOT_INTERVAL_TICKS = 18000;       // 15 min × 20 TPS
// Source: Session 1 - 03-data-persistence.md
```

---

## 2. Timing Constants

### Game Time Scale

```csharp
// Real-time to game-time conversion
public const float GAME_TIME_SCALE = 1.0f;                        // 1 real second = 1 game second (default)
public const float TIME_ACCELERATION_2X = 2.0f;                   // 2× speed when offline
public const float TIME_ACCELERATION_5X = 5.0f;                   // 5× speed option
public const float TIME_ACCELERATION_10X = 10.0f;                 // 10× speed maximum
// Source: Session 1 - 02-client-server-architecture.md

// Day/season/year length
public const float DAY_LENGTH_REAL_MINUTES = 60.0f;               // 1 game day = 1 real hour
public const float DAY_LENGTH_GAME_HOURS = 24.0f;                 // Standard 24-hour day
public const int SEASON_LENGTH_DAYS = 7;                          // 7 days per season
public const int YEAR_LENGTH_DAYS = 28;                           // 4 seasons × 7 days
public const float SEASON_LENGTH_REAL_HOURS = 7.0f;               // 7 real hours per season
public const float YEAR_LENGTH_REAL_HOURS = 28.0f;                // 28 real hours per year
// Source: Session 1 - 01-architecture-overview.md (biome system)
```

### Tick Conversion

```csharp
// Tick-to-real-time conversions
public const int TICKS_PER_SECOND = 20;
public const int TICKS_PER_MINUTE = 1200;                         // 20 × 60
public const int TICKS_PER_HOUR = 72000;                          // 20 × 3600
public const int TICKS_PER_DAY = 1728000;                         // 20 × 86400
public const float SECONDS_PER_TICK = 0.05f;                      // 1/20
public const float MINUTES_PER_TICK = 0.000833f;                  // 1/1200

// Game-day to tick conversion (1 real hour = 1 game day)
public const int TICKS_PER_GAME_DAY = 72000;                      // Same as real hour
public const int TICKS_PER_GAME_HOUR = 3000;                      // 72000 / 24
// Source: Derived from TICK_RATE
```

---

## 3. World Generation

### World Size

```csharp
// World dimensions
public const float WORLD_SIZE_MVP_KM2 = 0.5f;                     // MVP world size (0.5 km²)
public const float WORLD_SIZE_POST_MVP_KM2 = 4.0f;                // Post-MVP world size (4 km²)
public const float WORLD_SIZE_MAX_KM2 = 4.0f;                     // Maximum planned world size
public const float WORLD_SIZE_MIN_KM2 = 0.5f;                     // Minimum world size

// Derived dimensions (assuming square worlds)
public const float WORLD_LENGTH_MVP_METERS = 707.0f;              // √0.5 km² ≈ 707m per side
public const float WORLD_LENGTH_POST_MVP_METERS = 2000.0f;        // √4 km² = 2000m per side
// Source: Session 1 - 04-performance-scalability.md, Session 3 - 01-gameplay-systems-architecture.md
```

### Chunk System

```csharp
// Spatial partitioning
public const int CHUNK_SIZE_METERS = 100;                         // 100m × 100m chunks
public const int CHUNK_SIZE_MAX_VIEW_METERS = 200;                // Maximum view distance

// Chunk counts for world sizes
public const int CHUNKS_MVP_TOTAL = 50;                           // ~50 chunks for 0.5 km²
public const int CHUNKS_POST_MVP_TOTAL = 400;                     // ~400 chunks for 4 km²
public const int CHUNKS_MAX_ACTIVE = 9;                           // 3×3 chunks around player
// Source: Session 1 - 02-client-server-architecture.md, 04-performance-scalability.md
```

### Biome Distribution

```csharp
// MVP biomes (3 required)
public const int BIOME_COUNT_MVP = 3;                             // Boreal Forest, Desert, Jungle
public const int BIOME_COUNT_POST_MVP = 6;                        // +Grassland, Tundra, Temperate Forest

// Biome elevation ranges (meters)
public const float BIOME_ELEVATION_MIN = 0.0f;
public const float BIOME_ELEVATION_MAX = 1500.0f;                 // Maximum elevation

// Sub-biome thresholds - Boreal Forest
public const float BOREAL_ELEVATION_TAIGA_MAX = 200.0f;           // Lowland Taiga: 0-200m
public const float BOREAL_ELEVATION_MID_MAX = 500.0f;             // Mid-Elevation: 200-500m
public const float BOREAL_ELEVATION_MONTANE_MAX = 800.0f;         // Montane: 500-800m
public const float BOREAL_ELEVATION_ALPINE_MIN = 800.0f;          // Alpine Tundra: 800m+

// Sub-biome thresholds - Desert
public const float DESERT_ELEVATION_SALT_MAX = 100.0f;            // Salt Flats: 0-100m
public const float DESERT_ELEVATION_DUNES_MAX = 300.0f;           // Sand Dunes: 100-300m
public const float DESERT_ELEVATION_ROCKY_MAX = 500.0f;           // Rocky Desert: 300-500m
public const float DESERT_ELEVATION_MOUNTAIN_MIN = 500.0f;        // Desert Mountains: 500m+

// Sub-biome thresholds - Jungle
public const float JUNGLE_ELEVATION_RIVER_MAX = 150.0f;           // Riverine: 0-150m
public const float JUNGLE_ELEVATION_RAINFOREST_MAX = 400.0f;      // Rainforest: 150-400m
public const float JUNGLE_ELEVATION_CLOUD_MAX = 700.0f;           // Cloud Forest: 400-700m
public const float JUNGLE_ELEVATION_PEAKS_MIN = 700.0f;           // Jungle Peaks: 700m+
// Source: Session 1 - 01-architecture-overview.md

// Temperature and precipitation modifiers
public const float TEMPERATURE_LAPSE_RATE_PER_1000M = -6.5f;      // -6.5°C per 1000m elevation
public const float TEMPERATURE_SEASONAL_VARIATION = 15.0f;        // ±15°C seasonal amplitude
public const float PRECIPITATION_JUNGLE_BASE_MM = 3000.0f;        // 2000-4000mm/year base
public const float PRECIPITATION_DESERT_BASE_MM = 150.0f;         // 50-250mm/year base
public const float PRECIPITATION_BOREAL_BASE_MM = 550.0f;         // 300-800mm/year base
// Source: Session 1 - 01-architecture-overview.md
```

---

## 4. Economy & Resources

### Starting Resources

```csharp
// Starting credits
public const float STARTING_CREDITS_PLAYER = 100.0f;              // Player starting money
public const float STARTING_CREDITS_AGENT = 100.0f;               // AI agent starting money
public const float STARTING_CREDITS_MIN = 50.0f;                  // Minimum to avoid immediate poverty

// Currency
public const string CURRENCY_SYMBOL = "Cr";                       // Credits symbol
public const float CURRENCY_MAX_BALANCE = 1000000.0f;             // 1 million credits max (soft cap)
// Source: Session 2 - 02-economic-behavior.md
```

### Inventory System

```csharp
// Inventory limits
public const int INVENTORY_SLOTS_PLAYER = 64;                     // Player inventory slots
public const int INVENTORY_SLOTS_AGENT = 64;                      // AI agent inventory slots
public const float INVENTORY_WEIGHT_MAX_KG = 100.0f;              // Maximum carry weight
public const float INVENTORY_WEIGHT_PLAYER_BASE_KG = 50.0f;       // Base player capacity

// Stack sizes by resource type
public const int STACK_SIZE_WOOD = 100;                           // Wood stack size
public const int STACK_SIZE_STONE = 50;                           // Stone stack size
public const int STACK_SIZE_ORE = 50;                             // Ore stack size
public const int STACK_SIZE_FOOD = 20;                            // Food stack size
public const int STACK_SIZE_TOOLS = 1;                            // Tools don't stack
public const int STACK_SIZE_MATERIALS = 100;                      // Crafting materials
// Source: Session 3 - 01-gameplay-systems-architecture.md
```

### Price Ranges

```csharp
// Day 1 economy (early scarcity)
public const float PRICE_DAY1_FOOD_MIN = 5.0f;                    // Food: 5-15 credits
public const float PRICE_DAY1_FOOD_MAX = 15.0f;
public const float PRICE_DAY1_WOOD_MIN = 3.0f;                    // Wood: 3-8 credits
public const float PRICE_DAY1_WOOD_MAX = 8.0f;
public const float PRICE_DAY1_STONE_MIN = 5.0f;                   // Stone: 5-12 credits
public const float PRICE_DAY1_STONE_MAX = 12.0f;
public const float PRICE_DAY1_TOOLS_MIN = 50.0f;                  // Tools: 50-150 credits
public const float PRICE_DAY1_TOOLS_MAX = 150.0f;

// Day 7 economy (established)
public const float PRICE_DAY7_FOOD_MIN = 3.0f;                    // Food: 3-8 credits
public const float PRICE_DAY7_FOOD_MAX = 8.0f;
public const float PRICE_DAY7_WOOD_MIN = 2.0f;                    // Wood: 2-5 credits
public const float PRICE_DAY7_WOOD_MAX = 5.0f;
public const float PRICE_DAY7_STONE_MIN = 3.0f;                   // Stone: 3-7 credits
public const float PRICE_DAY7_STONE_MAX = 7.0f;
public const float PRICE_DAY7_TOOLS_MIN = 30.0f;                  // Tools: 30-80 credits
public const float PRICE_DAY7_TOOLS_MAX = 80.0f;

// Day 30 economy (mature)
public const float PRICE_DAY30_FOOD_MIN = 1.0f;                   // Food: 1-4 credits
public const float PRICE_DAY30_FOOD_MAX = 4.0f;
public const float PRICE_DAY30_WOOD_MIN = 1.0f;                   // Wood: 1-3 credits
public const float PRICE_DAY30_WOOD_MAX = 3.0f;
public const float PRICE_DAY30_STONE_MIN = 1.0f;                  // Stone: 1-3 credits
public const float PRICE_DAY30_STONE_MAX = 3.0f;
public const float PRICE_DAY30_TOOLS_MIN = 15.0f;                 // Tools: 15-40 credits
public const float PRICE_DAY30_TOOLS_MAX = 40.0f;
// Source: Derived from economic models in Session 2
```

---

## 5. Player Stats

### Health & Energy

```csharp
// Stat maximums
public const float HEALTH_MAX = 100.0f;                           // Maximum health points
public const float ENERGY_MAX = 100.0f;                           // Maximum energy points
public const float HUNGER_MAX = 100.0f;                           // Maximum hunger (0 = full, 100 = starving)
public const float STAMINA_MAX = 100.0f;                          // Maximum stamina

// Stat regeneration rates (per game hour = 3000 ticks)
public const float HEALTH_REGEN_PER_HOUR = 5.0f;                  // Health regeneration
public const float ENERGY_REGEN_REST_PER_HOUR = 20.0f;            // Energy regen while resting
public const float ENERGY_REGEN_ACTIVE_PER_HOUR = 5.0f;           // Energy regen while active
public const float STAMINA_REGEN_REST_PER_HOUR = 30.0f;           // Stamina regen while resting
public const float STAMINA_REGEN_WALK_PER_HOUR = 10.0f;           // Stamina regen while walking
// Source: Session 3 - 01-gameplay-systems-architecture.md

// Stat decay rates (per game hour)
public const float HUNGER_DECAY_PER_HOUR = 5.0f;                  // Hunger increases 5 per hour
public const float ENERGY_DECAY_ACTIVE_PER_HOUR = 10.0f;          // Energy drain while working
public const float STAMINA_DRAIN_SPRINT_PER_SECOND = 2.0f;        // Stamina drain while sprinting
// Source: Session 2 - 01-core-ai-architecture.md
```

### Movement

```csharp
// Movement speeds (meters per second)
public const float MOVEMENT_SPEED_WALK = 3.0f;                    // Walking speed (3 m/s)
public const float MOVEMENT_SPEED_SPRINT = 6.0f;                  // Sprinting speed (6 m/s)
public const float MOVEMENT_SPEED_CARRYING = 2.0f;                // Speed while carrying heavy load

// Sprint mechanics
public const float SPRINT_STAMINA_COST_PER_SECOND = 2.0f;         // Stamina cost per second
public const float SPRINT_MIN_STAMINA_TO_START = 20.0f;           // Minimum stamina to begin sprint
public const float SPRINT_RECOVERY_TIME_SECONDS = 2.0f;           // Time before stamina starts recovering
// Source: Session 3 - 01-gameplay-systems-architecture.md
```

---

## 6. Skills System

### Skill Levels

```csharp
// Skill system constants
public const int SKILL_LEVELS_COUNT = 10;                         // Levels 0-9 (or 1-10)
public const int SKILL_LEVEL_MAX = 10;                            // Maximum skill level
public const float SKILL_BONUS_PER_LEVEL_PERCENT = 5.0f;          // 5% efficiency bonus per level

// XP requirements per level (cumulative)
public const int XP_LEVEL_0_TO_1 = 100;                           // XP to reach level 1
public const int XP_LEVEL_1_TO_2 = 200;                           // XP to reach level 2
public const int XP_LEVEL_2_TO_3 = 400;                           // XP to reach level 3
public const int XP_LEVEL_3_TO_4 = 800;                           // XP to reach level 4
public const int XP_LEVEL_4_TO_5 = 1600;                          // XP to reach level 5
public const int XP_LEVEL_5_TO_6 = 3200;                          // XP to reach level 6
public const int XP_LEVEL_6_TO_7 = 6400;                          // XP to reach level 7
public const int XP_LEVEL_7_TO_8 = 12800;                         // XP to reach level 8
public const int XP_LEVEL_8_TO_9 = 25600;                         // XP to reach level 9
public const int XP_LEVEL_9_TO_10 = 51200;                        // XP to reach level 10

// Total XP for max level: 102,300 XP
// Source: Session 2 - 01-core-ai-architecture.md, Session 3 - 05-progression-feel.md
```

### XP Rewards

```csharp
// XP rewards by action type
public const int XP_GATHER_BASIC = 5;                             // Basic resource gathering
public const int XP_GATHER_ADVANCED = 15;                         // Advanced resource gathering
public const int XP_CRAFT_SIMPLE = 10;                            // Simple crafting
public const int XP_CRAFT_COMPLEX = 25;                           // Complex crafting
public const int XP_BUILD_SMALL = 20;                             // Small construction
public const int XP_BUILD_LARGE = 50;                             // Large construction
public const int XP_TRADE_SUCCESSFUL = 3;                         // Successful trade
public const int XP_GOVERNANCE_PARTICIPATION = 10;                // Voting, law-making
public const int XP_SKILL_PRACTICE_BASE = 2;                      // Base XP for practicing skill
// Source: Session 2 - 01-core-ai-architecture.md
```

---

## 7. AI Agent Constants

### Memory System

```csharp
// Memory slot counts
public const int AGENT_MEMORY_SLOTS_SHORT_TERM = 5;               // Short-term memory slots
public const int AGENT_MEMORY_SLOTS_LONG_TERM = 5;                // Long-term memory slots
public const int AGENT_MEMORY_SLOTS_CORE = 10;                    // Core memory slots (unlimited in practice)

// Memory slot sizes
public const int AGENT_MEMORY_BYTES_PER_SLOT = 64;                // 64 bytes per memory slot
public const int AGENT_MEMORY_TOTAL_BYTES = 640;                  // (5+5) × 64 bytes

// Memory timing
public const float MEMORY_SHORT_TERM_DURATION_HOURS = 24.0f;      // STM lasts 24 game hours
public const float MEMORY_LONG_TERM_DURATION_DAYS = 30.0f;        // LTM lasts 30 game days
public const int MEMORY_CONSOLIDATION_INTERVAL_TICKS = 10;        // Every 10 ticks

// Memory emotional valence
public const sbyte MEMORY_VALENCE_MIN = -100;                     // Most negative memory
public const sbyte MEMORY_VALENCE_MAX = 100;                      // Most positive memory
public const byte MEMORY_IMPORTANCE_MIN = 0;                      // Trivial memory
public const byte MEMORY_IMPORTANCE_MAX = 255;                    // Life-changing memory
// Source: Session 2 - 01-core-ai-architecture.md, 04-population-personality.md
```

### Agent State

```csharp
// Agent state size
public const int AGENT_STATE_SIZE_BYTES = 32;                     // AgentState struct
public const int AGENT_TOTAL_MEMORY_BYTES = 8192;                 // ~8KB per agent total

// Agent perception
public const float AGENT_PERCEPTION_RADIUS_METERS = 50.0f;        // Perception radius
public const float AGENT_INTERACTION_RADIUS_METERS = 10.0f;       // Can interact within 10m
public const int AGENT_SPAWN_CHECK_INTERVAL_TICKS = 300;          // Check spawn every 300 ticks (15s)

// Agent tick processing
public const float AGENT_TICK_BUDGET_AMORTIZED_MS = 1.5f;         // Average budget with amortization
public const int AGENT_GOAL_EVALUATION_INTERVAL_TICKS = 5;        // Evaluate goals every 5 ticks
public const int AGENT_LEARNING_INTERVAL_TICKS = 10;              // Learn every 10 ticks

// LOD (Level of Detail) processing
public const float AGENT_LOD_HIGH_DISTANCE_METERS = 20.0f;        // Full AI every tick
public const float AGENT_LOD_MEDIUM_DISTANCE_METERS = 100.0f;     // Reduced AI every 5 ticks
public const float AGENT_LOD_LOW_DISTANCE_METERS = 500.0f;        // Minimal AI every 20 ticks

// LOD frequencies
public const int AGENT_LOD_HIGH_FREQUENCY_TICKS = 1;              // Every tick
public const int AGENT_LOD_MEDIUM_FREQUENCY_TICKS = 5;            // Every 5 ticks
public const int AGENT_LOD_LOW_FREQUENCY_TICKS = 20;              // Every 20 ticks
public const int AGENT_LOD_DORMANT_FREQUENCY_TICKS = 100;         // Every 100 ticks
// Source: Session 2 - 01-core-ai-architecture.md
```

### Personality System

```csharp
// Personality trait count and ranges
public const int AGENT_PERSONALITY_FACET_COUNT = 19;              // 19 personality facets
public const byte AGENT_PERSONALITY_MIN = 0;                      // Minimum trait value
public const byte AGENT_PERSONALITY_MAX = 100;                    // Maximum trait value
public const byte AGENT_PERSONALITY_NEUTRAL = 50;                 // Neutral/middle value

// Core 5 traits (direct gameplay impact)
public const int TRAIT_GREGARIOUSNESS = 0;                        // Social need
public const int TRAIT_WORK_ETHIC = 1;                            // Productivity drive
public const int TRAIT_VIOLENCE = 2;                              // Conflict tendency (unused - non-violent)
public const int TRAIT_GREED = 3;                                 // Economic drive
public const int TRAIT_EMOTIONAL_STABILITY = 4;                   // Stress resilience

// Big Five traits (OCEAN)
public const int TRAIT_OPENNESS = 5;                              // Creativity/curiosity
public const int TRAIT_CONSCIENTIOUSNESS = 6;                     // Organization/discipline
public const int TRAIT_EXTRAVERSION = 7;                          // Social energy
public const int TRAIT_AGREEABLENESS = 8;                         // Cooperation/trust
public const int TRAIT_NEUROTICISM = 9;                           // Anxiety/sensitivity

// Secondary 9 traits
public const int TRAIT_BRAVERY = 10;
public const int TRAIT_ALTRUISM = 11;
public const int TRAIT_EXCITEMENT_SEEKING = 12;
public const int TRAIT_TRADITION = 13;
public const int TRAIT_PROGRESSIVISM = 14;
// ... (19 total traits)
// Source: Session 2 - 01-core-ai-architecture.md, 06-ai-skills-reference.md
```

### Need Decay Rates

```csharp
// Need decay rates (per game hour)
public const float AGENT_NEED_DECAY_HUNGER_PER_HOUR = 5.0f;       // Hunger increases
public const float AGENT_NEED_DECAY_ENERGY_PER_HOUR = 5.0f;       // Energy decreases
public const float AGENT_NEED_DECAY_SOCIAL_PER_HOUR = 3.0f;       // Social need increases
public const float AGENT_NEED_DECAY_COMFORT_PER_HOUR = 2.0f;      // Comfort need decreases

// Need thresholds (0-100 scale)
public const float AGENT_NEED_CRITICAL_THRESHOLD = 80.0f;         // Critical need level
public const float AGENT_NEED_HIGH_THRESHOLD = 60.0f;             // High need level
public const float AGENT_NEED_MODERATE_THRESHOLD = 40.0f;         // Moderate need level
public const float AGENT_NEED_LOW_THRESHOLD = 20.0f;              // Low need level

// Survival thresholds
public const float AGENT_HUNGER_CRITICAL = 80.0f;                 // Must eat immediately
public const float AGENT_ENERGY_CRITICAL = 20.0f;                 // Must rest immediately
// Source: Session 2 - 01-core-ai-architecture.md
```

### Economic Behavior

```csharp
// Price beliefs
public const int AGENT_PRICE_BELIEFS_MAX = 32;                    // Max price beliefs per agent
public const float AGENT_PRICE_BELIEF_DECAY_PER_HOUR = 0.02f;     // 2% uncertainty growth per hour
public const float AGENT_PRICE_BELIEF_MIN_UNCERTAINTY = 0.10f;    // Minimum 10% uncertainty
public const float AGENT_PRICE_BELIEF_MAX_UNCERTAINTY = 3.00f;    // Maximum 300% uncertainty
public const byte AGENT_PRICE_BELIEF_OBSERVATION_CAP = 100;       // Max observations tracked

// Trading behavior
public const float AGENT_TRADE_GREED_BIAS_PERCENT = 10.0f;        // ±10% price bias from greed
public const float AGENT_TRADE_CONFIDENCE_THRESHOLD = 0.4f;       // Minimum confidence to trade
// Source: Session 2 - 02-economic-behavior.md
```

---

## 8. Building & Claims

### Claim Sizes

```csharp
// Land claim sizes (meters squared)
public const float CLAIM_SIZE_HOMESTEAD_M2 = 10000.0f;            // 100m × 100m homestead
public const float CLAIM_SIZE_TOWN_M2 = 100000.0f;                // 316m × 316m town
public const float CLAIM_SIZE_CITY_M2 = 1000000.0f;               // 1000m × 1000m city

// Claim dimensions (approximate, assuming square)
public const float CLAIM_LENGTH_HOMESTEAD_METERS = 100.0f;
public const float CLAIM_LENGTH_TOWN_METERS = 316.0f;
public const float CLAIM_LENGTH_CITY_METERS = 1000.0f;
// Source: Session 1 - comprehensive-breakdown.md, Session 3
```

### Building Limits

```csharp
// Building constraints
public const float BUILDING_MAX_HEIGHT_METERS = 50.0f;            // Maximum building height
public const int BUILDING_DECAY_DAYS = 30;                        // Building decay after 30 days
public const int BUILDING_MAINTENANCE_INTERVAL_DAYS = 7;          // Weekly maintenance check

// Material durability multipliers
public const float BUILDING_DURABILITY_WOOD = 1.0f;               // Wood baseline
public const float BUILDING_DURABILITY_STONE = 3.0f;              // Stone 3× more durable
public const float BUILDING_DURABILITY_IRON = 5.0f;               // Iron 5× more durable
public const float BUILDING_DURABILITY_STEEL = 10.0f;             // Steel 10× more durable
// Source: Session 1 - comprehensive-breakdown.md
```

---

## 9. Tools & Equipment

### Tool Durability

```csharp
// Tool uses before breaking
public const int TOOL_DURABILITY_STONE = 50;                      // Stone tool uses
public const int TOOL_DURABILITY_IRON = 150;                      // Iron tool uses
public const int TOOL_DURABILITY_STEEL = 500;                     // Steel tool uses

// Tool repair
public const float TOOL_REPAIR_COST_PERCENT = 50.0f;              // 50% of material cost to repair
public const int TOOL_REPAIR_DURABILITY_RESTORED = 80;            // Repair restores 80% durability

// Tool efficiency multipliers
public const float TOOL_EFFICIENCY_STONE = 1.0f;                  // Baseline efficiency
public const float TOOL_EFFICIENCY_IRON = 1.5f;                   // 50% faster with iron
public const float TOOL_EFFICIENCY_STEEL = 2.0f;                  // 100% faster with steel
// Source: Session 1 - comprehensive-breakdown.md
```

---

## 10. Governance

### Population Requirements

```csharp
// Minimum citizens for governance tiers
public const int MIN_CITIZENS_TOWN = 3;                           // Minimum for town formation
public const int MIN_CITIZENS_STATE = 50;                         // Minimum for state formation
public const int MIN_CITIZENS_FEDERATION = 100;                   // Minimum for federation formation

// Town growth thresholds
public const int TOWN_POPULATION_SMALL_MAX = 10;                  // 3-10 citizens
public const int TOWN_POPULATION_MEDIUM_MAX = 30;                 // 11-30 citizens
public const int TOWN_POPULATION_LARGE_MAX = 75;                  // 31-75 citizens
public const int TOWN_POPULATION_CITY_MIN = 76;                   // 76+ citizens = city
// Source: Session 1 - comprehensive-breakdown.md
```

### Voting & Laws

```csharp
// Voting timing
public const float VOTE_DURATION_HOURS = 24.0f;                   // Standard vote duration
public const float VOTE_DURATION_URGENT_HOURS = 4.0f;             // Urgent vote duration
public const float LAW_ENFORCEMENT_DELAY_SECONDS = 5.0f;          // Delay before law takes effect

// Tax limits
public const float TAX_RATE_MIN_PERCENT = 0.0f;                   // 0% minimum tax
public const float TAX_RATE_MAX_PERCENT = 50.0f;                  // 50% maximum tax
public const float TAX_RATE_DEFAULT_PERCENT = 10.0f;              // 10% default tax

// Election terms
public const int ELECTION_TERM_TOWN_COUNCIL_DAYS = 14;            // 2 week council terms
public const int ELECTION_TERM_MAYOR_DAYS = 30;                   // 1 month mayor terms
public const int ELECTION_TERM_STATE_OFFICIAL_DAYS = 60;          // 2 month state terms
// Source: Session 1 - comprehensive-breakdown.md, Session 2
```

---

## 11. Crafting & Production

### Production Timing

```csharp
// Base production times (seconds at skill level 0)
public const float PRODUCE_TIME_SIMPLE_TOOL = 30.0f;              // 30 seconds for simple tool
public const float PRODUCE_TIME_COMPLEX_TOOL = 120.0f;            // 2 minutes for complex tool
public const float PRODUCE_TIME_BASIC_BUILDING = 300.0f;          // 5 minutes for basic structure
public const float PRODUCE_TIME_ADVANCED_BUILDING = 1800.0f;      // 30 minutes for advanced structure
public const float PRODUCE_TIME_CRAFT_ITEM = 15.0f;               // 15 seconds per craft
public const float PRODUCE_TIME_PROCESS_RESOURCE = 5.0f;          // 5 seconds per resource

// Skill impact on production time
public const float PRODUCTION_TIME_SKILL_MULTIPLIER_PER_LEVEL = 0.95f; // 5% faster per level
// At level 10: 0.95^10 = 0.60 (40% faster)
// Source: Session 1 - comprehensive-breakdown.md
```

### Quality Thresholds

```csharp
// Item quality levels
public const int QUALITY_POOR_MIN = 0;
public const int QUALITY_POOR_MAX = 25;                           // 0-25% = Poor quality
public const int QUALITY_NORMAL_MIN = 26;
public const int QUALITY_NORMAL_MAX = 50;                         // 26-50% = Normal
public const int QUALITY_GOOD_MIN = 51;
public const int QUALITY_GOOD_MAX = 75;                           // 51-75% = Good
public const int QUALITY_EXCELLENT_MIN = 76;
public const int QUALITY_EXCELLENT_MAX = 95;                      // 76-95% = Excellent
public const int QUALITY_MASTERWORK_MIN = 96;
public const int QUALITY_MASTERWORK_MAX = 100;                    // 96-100% = Masterwork

// Quality modifiers
public const float QUALITY_MULTIPLIER_POOR = 0.70f;               // 70% effectiveness
public const float QUALITY_MULTIPLIER_NORMAL = 1.00f;             // 100% effectiveness
public const float QUALITY_MULTIPLIER_GOOD = 1.15f;               // 115% effectiveness
public const float QUALITY_MULTIPLIER_EXCELLENT = 1.30f;          // 130% effectiveness
public const float QUALITY_MULTIPLIER_MASTERWORK = 1.50f;         // 150% effectiveness
// Source: Session 1 - comprehensive-breakdown.md
```

---

## 12. Social & Reputation

### Reputation System

```csharp
// Reputation ranges
public const float REPUTATION_MIN = -100.0f;                      // Minimum reputation
public const float REPUTATION_MAX = 100.0f;                       // Maximum reputation
public const float REPUTATION_NEUTRAL = 0.0f;                     // Neutral reputation

// Reputation change values
public const float REPUTATION_CHANGE_MINOR = 5.0f;                // Minor interaction
public const float REPUTATION_CHANGE_MODERATE = 15.0f;            // Moderate interaction
public const float REPUTATION_CHANGE_MAJOR = 30.0f;               // Major interaction
public const float REPUTATION_CHANGE_SIGNIFICANT = 50.0f;         // Significant event

// Reputation impact thresholds
public const float REPUTATION_THRESHOLD_TRUSTED = 50.0f;          // Trusted citizen
public const float REPUTATION_THRESHOLD_RESPECTED = 70.0f;        // Respected leader
public const float REPUTATION_THRESHOLD_SUSPICIOUS = -20.0f;      // Suspicious citizen
public const float REPUTATION_THRESHOLD_OUTCAST = -50.0f;         // Social outcast
// Source: Session 2 - 01-core-ai-architecture.md
```

### Relationship System

```csharp
// Relationship strength ranges
public const float RELATIONSHIP_MIN = -100.0f;                    // Mortal enemy
public const float RELATIONSHIP_MAX = 100.0f;                     // Best friend/soulmate
public const float RELATIONSHIP_NEUTRAL = 0.0f;                   // Stranger

// Relationship categories
public const float RELATIONSHIP_HOSTILE_MAX = -50.0f;             // -100 to -50 = Hostile
public const float RELATIONSHIP_NEGATIVE_MAX = -10.0f;            // -50 to -10 = Negative
public const float RELATIONSHIP_ACQUAINTANCE_MAX = 30.0f;         // -10 to 30 = Acquaintance
public const float RELATIONSHIP_FRIEND_MAX = 70.0f;               // 30 to 70 = Friend
public const float RELATIONSHIP_CLOSE_FRIEND_MIN = 70.0f;         // 70+ = Close friend

// Max relationships per agent
public const int AGENT_MAX_FRIENDS = 16;                          // Maximum friends
public const int AGENT_MAX_ENEMIES = 8;                           // Maximum enemies
public const int AGENT_MAX_RELATIONSHIPS = 24;                    // Total relationship slots
// Source: Session 2 - 01-core-ai-architecture.md
```

### Faction System

```csharp
// Faction cohesion
public const float FACTION_COHESION_MIN = 0.0f;                   // Completely fractured
public const float FACTION_COHESION_MAX = 1.0f;                   // Perfectly unified
public const float FACTION_COHESION_FRAGMENTED_MAX = 0.3f;        // 0-0.3 = Fragmented
public const float FACTION_COHESION_MODERATE_MAX = 0.7f;          // 0.3-0.7 = Moderate
public const float FACTION_COHESION_UNIFIED_MIN = 0.7f;           // 0.7+ = Unified

// Faction formation thresholds
public const int FACTION_FORMATION_MIN_AGENTS = 3;                // Min 3 agents to form faction
public const float FACTION_FORMATION_SIMILARITY_THRESHOLD = 0.6f; // 60% similarity required
public const float FACTION_FORMATION_NETWORK_DENSITY = 0.6f;      // 60% communication density
// Source: Session 2 - 03-political-social-behavior.md
```

---

## 13. Progression & Threats

### Timeline Milestones

```csharp
// Meteor timeline
public const int DAY_METEOR_DETECTION = 20;                       // Meteor detected on day 20
public const int DAY_METEOR_IMPACT = 30;                          // Meteor impact on day 30
public const int DAYS_METEOR_PREP_TIME = 10;                      // 10 days to prepare

// Progression phases (real days)
public const int PHASE_DAY1_START = 1;                            // Day 1: Survival
public const int PHASE_DAY7_COMPETENT = 7;                        // Day 7: Competent
public const int PHASE_DAY14_SKILLED = 14;                        // Day 14: Skilled
public const int PHASE_DAY21_EXPERT = 21;                         // Day 21: Expert
public const int PHASE_DAY30_MASTER = 30;                         // Day 30: Master
// Source: Session 3 - 05-progression-feel.md
```

### Pollution Thresholds

```csharp
// Pollution levels (ppm or arbitrary units)
public const float POLLUTION_LOW_MAX = 100.0f;                    // 0-100 = Low pollution
public const float POLLUTION_MODERATE_MAX = 300.0f;               // 100-300 = Moderate
public const float POLLUTION_HIGH_MAX = 600.0f;                   // 300-600 = High
public const float POLLUTION_CRITICAL_MIN = 600.0f;               // 600+ = Critical

// Pollution effects thresholds
public const float POLLUTION_EFFECT_PLANT_GROWTH_REDUCTION = 150.0f;  // Reduced plant growth
public const float POLLUTION_EFFECT_HEALTH_IMPACT = 250.0f;           // Health impacts begin
public const float POLLUTION_EFFECT_SPECIES_DECLINE = 400.0f;         // Species begin declining
public const float POLLUTION_EFFECT_ECOSYSTEM_COLLAPSE = 800.0f;      // Ecosystem collapse risk
// Source: Session 1 - comprehensive-breakdown.md
```

### Ecosystem Health

```csharp
// Ecosystem health ranges (0-100%)
public const float ECOSYSTEM_HEALTH_THRIVING_MIN = 80.0f;         // 80-100% = Thriving
public const float ECOSYSTEM_HEALTH_STABLE_MIN = 60.0f;           // 60-80% = Stable
public const float ECOSYSTEM_HEALTH_STRESSED_MIN = 40.0f;         // 40-60% = Stressed
public const float ECOSYSTEM_HEALTH_DEGRADED_MIN = 20.0f;         // 20-40% = Degraded
public const float ECOSYSTEM_HEALTH_COLLAPSED_MAX = 20.0f;        // 0-20% = Collapsed

// Species population thresholds
public const float SPECIES_EXTINCTION_THRESHOLD = 2;              // < 2 individuals = functionally extinct
public const float SPECIES_CRITICAL_THRESHOLD = 10;               // < 10 = critically endangered
public const float SPECIES_ENDANGERED_THRESHOLD = 100;            // < 100 = endangered
public const float SPECIES_HEALTHY_MINIMUM = 500;                 // > 500 = healthy population
// Source: Session 1 - comprehensive-breakdown.md
```

### Climate Change

```csharp
// Climate change thresholds
public const float CLIMATE_CO2_BASELINE_PPM = 280.0f;             // Pre-industrial baseline
public const float CLIMATE_CO2_MODERATE_RISE = 400.0f;            // 400 ppm = moderate warming
public const float CLIMATE_CO2_HIGH_RISE = 550.0f;                // 550 ppm = high warming
public const float CLIMATE_CO2_CRITICAL_RISE = 800.0f;            // 800 ppm = critical

// Temperature change effects
public const float CLIMATE_TEMP_CHANGE_MODERATE = 1.5f;           // +1.5°C = moderate effects
public const float CLIMATE_TEMP_CHANGE_HIGH = 3.0f;               // +3°C = high effects
public const float CLIMATE_TEMP_CHANGE_CRITICAL = 5.0f;           // +5°C = critical effects

// Sea level rise
public const float SEA_LEVEL_RISE_MODERATE_CM = 50.0f;            // 50cm = moderate flooding
public const float SEA_LEVEL_RISE_HIGH_CM = 100.0f;               // 1m = significant flooding
public const float SEA_LEVEL_RISE_CRITICAL_CM = 200.0f;           // 2m = critical flooding
// Source: Session 1 - comprehensive-breakdown.md
```

---

## 14. Network & Synchronization

### Network Timing

```csharp
// Jitter buffer
public const float NETWORK_JITTER_BUFFER_MS = 100.0f;             // 100ms jitter buffer

// Latency targets
public const float NETWORK_LATENCY_TARGET_MS = 50.0f;             // Target latency
public const float NETWORK_LATENCY_MAX_ACCEPTABLE_MS = 150.0f;    // Maximum acceptable latency

// Sync frequencies
public const int NETWORK_SYNC_POSITION_EVERY_TICKS = 1;           // Position every tick (20 TPS)
public const int NETWORK_SYNC_FULL_STATE_EVERY_TICKS = 10;        // Full state every 10 ticks
public const int NETWORK_SNAPSHOT_INTERVAL_SECONDS = 2;           // Snapshot every 2 seconds
// Source: Session 1 - 02-client-server-architecture.md
```

### Compression

```csharp
// Delta compression
public const float DELTA_COMPRESSION_BANDWIDTH_REDUCTION = 0.60f; // 60% bandwidth reduction
public const float DIRTY_TRACKING_BANDWIDTH_REDUCTION = 0.70f;    // 70% bandwidth reduction

// Megapacket batching
public const int MEGAPACKET_BATCH_SIZE_TICKS = 6;                 // Batch 6 ticks
public const float MEGAPACKET_OVERHEAD_REDUCTION = 0.90f;         // 90% overhead reduction
// Source: Session 1 - 02-client-server-architecture.md
```

---

## Conflicts & Resolution Log

### Documented Conflicts

| Constant | Conflict Location | Values Found | Resolution | Date |
|----------|------------------|--------------|------------|------|
| AGENTS_MVP | Session 1 vs Session 2 | 20 vs 25 | **Resolved**: Use 25 (Session 1:04-performance-scalability.md is authoritative) | 2026-02-01 |
| WORLD_SIZE_MVP | 01-architecture vs 04-performance | 0.5 km² vs "0.5 km² blocks" | **No conflict**: Both specify 0.5 km² | 2026-02-01 |
| MEMORY_PER_AGENT | 02-client-server vs 01-core-ai | ~500 KB vs ~8.5 KB | **Resolved**: ~8.5 KB is complete agent state, ~500 KB is entity state cache estimate | 2026-02-01 |

### Pending Resolution

None as of 2026-02-01.

---

## Change Log

| Date | Change | Author | Reason |
|------|--------|--------|--------|
| 2026-02-01 | Initial document creation | Session 1-3 Review | Single source of truth for all constants |

---

## Usage Examples

### C# Example
```csharp
// Using constants in code
if (agent.tickBudgetMs > TechnicalConstants.PER_AGENT_BUDGET_MS)
{
    telemetry.RecordOverBudget(agent);
}

// Check agent limits
if (world.agentCount >= TechnicalConstants.AGENTS_MVP)
{
    populationManager.EnableAmortization();
}
```

### Database Example
```sql
-- Using constants in queries
SELECT * FROM agents 
WHERE reputation > 50  -- REPUTATION_THRESHOLD_TRUSTED
LIMIT 25;              -- AGENTS_MVP
```

### Configuration Example
```json
{
  "tickRate": 20,                    // TICK_RATE
  "maxAgents": 25,                   // AGENTS_MVP
  "worldSizeKm2": 0.5,               // WORLD_SIZE_MVP_KM2
  "bandwidthPerPlayerKbps": 32       // BANDWIDTH_PER_PLAYER_MVP_KBPS
}
```

---

**END OF DOCUMENT**

*This document is the authoritative source for all numerical constants in Societies. When in doubt, reference this document.*
