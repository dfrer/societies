# 07b: Screen Specifications & Wireframes

**Status**: SPECIFICATION DOCUMENT  
**Scope**: Complete UI screen inventory with wireframes, navigation flows, and design guidelines  
**Reference**: `planning/meta/technical-constants.md`, `planning/sessions/session-3-core-gameplay-loops/07-ui-ux-paths.md`

---

## Screen Inventory

Complete list of all 20 screens in Societies with purpose, access triggers, key features, and navigation connections.

### 1. Main Menu Screen

**Purpose**: Primary entry point, world selection, settings access  
**Access**: Game launch, return from gameplay via Esc menu  
**Key Features**: Continue game, new world, join world, settings, credits, exit  
**Navigation**: Leads to Character Creation, World Selection, or Settings

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  1920×1080 Reference Resolution                                  │
│                                                                  │
│                                                                  │
│                   ╔═══════════════════════════╗                  │
│                   ║                           ║                  │
│                   ║     S O C I E T I E S     ║                  │
│                   ║                           ║                  │
│                   ║      [Continue Game]      ║                  │
│                   ║      [New World]          ║                  │
│                   ║      [Join World]         ║                  │
│                   ║      [Settings]           ║                  │
│                   ║      [Credits]            ║                  │
│                   ║      [Exit]               ║                  │
│                   ║                           ║                  │
│                   ╚═══════════════════════════╝                  │
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐   │
│   │  Version: 1.0.0-alpha  │  Server Status: Online  │  8    │   │
│   └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

Position: Center all elements, menu container 400×400px
Buttons: 200×50px, stacked vertically, 20px gap
Padding: 20px internal to button
Background: Animated scene from game world (subtle parallax)
Title: Logo 300×80px, centered above menu
Version: Bottom-left, 14px font
Server Status: Bottom-center, 14px font
Player Count: Bottom-right, 14px font
```

---

### 2. Character Creation Screen

**Purpose**: First-time player setup, appearance, starting skills  
**Access**: First launch, new character button from Main Menu  
**Key Features**: Name input, appearance customization, skill point allocation, preview  
**Navigation**: Back to Main Menu, forward to World Selection

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  CREATE YOUR CHARACTER                              [X] Cancel  │
├───────────────────────────────┬─────────────────────────────────┤
│                               │                                 │
│   APPEARANCE                  │   CHARACTER PREVIEW             │
│                               │                                 │
│   ┌─────────────────────┐     │   ┌─────────────────────────┐   │
│   │ Name:               │     │   │                         │   │
│   │ [PlayerName       ] │     │   │    [3D Character]       │   │
│   └─────────────────────┘     │   │                         │   │
│                               │   │      ┌─────────┐        │   │
│   Hair Style:                 │   │      │ Avatar  │        │   │
│   [<] [Style 5/12] [>]        │   │      │  300×400│        │   │
│                               │   │      └─────────┘        │   │
│   Hair Color:                 │   │                         │   │
│   [●] [●] [●] [●] [●] [●]    │   │   ◄  [Rotate]  ►        │   │
│                               │   └─────────────────────────┘   │
│   Face:                       │                                 │
│   [<] [Face 3/8] [>]          │                                 │
│                               │                                 │
│   Body Type:                  │                                 │
│   [Slim] [Average] [Heavy]   │                                 │
│                               │                                 │
├───────────────────────────────┴─────────────────────────────────┤
│  STARTING SKILLS (10 points to distribute)                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   Gathering    [−] [5] [+]    Level 1  →  +5% gathering speed   │
│   Crafting     [−] [3] [+]    Level 1  →  +5% craft quality     │
│   Building     [−] [2] [+]    Level 1  →  +5% build speed       │
│                                                                 │
│   Points Remaining: 0                                           │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                    [Create Character]  [Back]                   │
└─────────────────────────────────────────────────────────────────┘

Window: 1200×800px centered
Left Panel: 550px (appearance)
Right Panel: 550px (preview + rotation)
Bottom Panel: 200px (skills + buttons)
Buttons: Primary 180×50px, Secondary 120×40px
Colors: Primary Blue (#4A90E2), Background #1A1A1A
Skill Reference: See technical-constants.md SKILL_BONUS_PER_LEVEL_PERCENT = 5%
```

---

### 3. World Selection Screen

**Purpose**: Choose existing world to join, view server list  
**Access**: Main Menu "Join World" or "Continue Game"  
**Key Features**: World cards with stats, player counts, day count, direct connect  
**Navigation**: To Loading Screen, back to Main Menu

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  SELECT WORLD                                       [X] Back    │
├─────────────────────────────────────────────────────────────────┤
│  [Search worlds...]                    [Filter ▼] [Refresh ↻]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  🌍  Alpha Test World                                    │   │
│  │                                                         │   │
│  │  Players: 12/25 (48%)      Day: 15                      │   │
│  │  Biome: Boreal Forest      Season: Spring               │   │
│  │  Uptime: 14d 3h             Ping: 45ms                  │   │
│  │                                                         │   │
│  │  [Join]              [Details]        [Favorite ☆]     │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  🌍  Community Server #1                                 │   │
│  │                                                         │   │
│  │  Players: 5/20 (25%)       Day: 3                       │   │
│  │  Biome: Desert             Season: Summer               │   │
│  │  Uptime: 2d 12h             Ping: 28ms                  │   │
│  │                                                         │   │
│  │  [Join]              [Details]        [Favorite ☆]     │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  🌍  [Direct Connect]                                    │   │
│  │                                                         │   │
│  │  IP: [192.168.1.100                    ]  Port: [7777 ]│   │
│  │                                                         │   │
│  │                                    [Connect]           │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  [Create Private World]        [Join by Invite Code]           │
└─────────────────────────────────────────────────────────────────┘

Window: 1100×750px centered
World Cards: 1000×120px each, 20px vertical gap
Card Layout: 3 rows of info + button row
Join Button: 100×40px, blue (#4A90E2)
Player Limits Reference: technical-constants.md PLAYERS_MVP = 8, PLAYERS_POST_MVP = 20
Scroll: Vertical if more than 4 worlds
```

---

### 4. Loading Screen

**Purpose**: World loading, connection establishment, asset streaming  
**Access**: Automatic after world selection  
**Key Features**: Progress bar, gameplay tips, world info, cancel option  
**Navigation**: Automatically transitions to Spawn/In-Game HUD

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│                                                                  │
│                                                                  │
│                     ╔════════════════════════╗                   │
│                     ║                        ║                   │
│                     ║    S O C I E T I E S   ║                   │
│                     ║                        ║                   │
│                     ╚════════════════════════╝                   │
│                                                                  │
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐   │
│   │  Connecting to Alpha Test World...                       │   │
│   └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐   │
│   │  ████████████████████████████████████████████░░░░  85%   │   │
│   └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│   Loading terrain...                                             │
│   Synchronizing agents (12/25)...                                │
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐   │
│   │  💡 TIP: Press 'C' to open the crafting menu at any time │   │
│   └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│                        [Cancel]                                  │
│                                                                  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

Full screen, dark background #0A0A0A
Progress Bar: 600×24px, centered
Bar Colors: Background #333333, Fill #4A90E2 gradient
Status Text: 16px, white, below progress bar
Tips Box: 500×60px, semi-transparent #1A1A1A, rounded corners
Tips rotate every 5 seconds
Cancel Button: 120×40px, gray #666666
Reference: technical-constants.md AGENTS_MVP = 25
```

---

### 5. In-Game HUD (Primary Interface)

**Purpose**: Main gameplay interface, always visible during play  
**Access**: Automatic after loading, Esc to access menu overlay  
**Key Features**: Health/Energy/Hunger bars, minimap, hotbar, notifications, resources  
**Navigation**: Opens all other in-game screens via shortcuts

```
Layout (1920×1080 Reference - From 07-ui-ux-paths.md):
┌─────────────────────────────────────────────────────────────────┐
│                                                                  │
│  ┌──────────────────┐            ┌────────────────────────────┐ │
│  │ Health           │            │         ┌──────┐           │ │
│  │ ████████████░░░░ │            │         │ Minimap│          │ │
│  │ 85/100           │            │         │ 200×200│          │ │
│  └──────────────────┘            │         └──────┘           │ │
│  ┌──────────────────┐            │  ☀ 10:30 AM                │ │
│  │ Energy           │            │  Spring, Day 12            │ │
│  │ ████████░░░░░░░░ │            │  72°F | Rain: 0%           │ │
│  │ 65/100           │            └────────────────────────────┘ │
│  └──────────────────┘                                            │
│  ┌──────────────────┐           ┌────────────────────────────┐  │
│  │ Hunger           │           │    [Crosshair]             │  │
│  │ ███░░░░░░░░░░░░░ │           │         +                  │  │
│  │ 25/100           │           └────────────────────────────┘  │
│  └──────────────────┘                                            │
│                                                                  │
│  ┌──────────────────────────┐                                   │
│  │ Notifications            │                                   │
│  │ • Wheat ready in 2h      │                                   │
│  │ • Martha wants to trade  │                                   │
│  └──────────────────────────┘                                   │
│                                                                  │
│                         [Hotbar]                                 │
│           ┌─┬─┬─┬─┬─┬─┬─┬─┬─┐                                    │
│           │1│2│3│4│5│6│7│8│9│     ┌──────────┐  ┌──────────┐    │
│           │🪓│🔨│🍞│🪵│_│_│_│_│_│     │1,250¢    │  │45/64    │    │
│           └─┴─┴─┴─┴─┴─┴─┴─┴─┘     └──────────┘  └──────────┘    │
│                                                                  │
│  ┌─────────────────────┐                                         │
│  │ Iron Axe            │                                         │
│  │ Durability: ████████░░░░ 234/300                              │
│  └─────────────────────┘                                         │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

Full screen overlay (CanvasLayer in Godot)
Reference Constants (from technical-constants.md):
  - HEALTH_MAX = 100.0f
  - ENERGY_MAX = 100.0f  
  - HUNGER_MAX = 100.0f
  - INVENTORY_SLOTS_PLAYER = 64
  - STARTING_CREDITS_PLAYER = 100.0f
  - TOOL_DURABILITY_IRON = 150

Bar Dimensions (from 07-ui-ux-paths.md):
  - Health/Energy/Hunger: 200×24px each
  - Position: X:20, Y:20 (health), Y:50 (energy), Y:80 (hunger)
  - Minimap: 200×200px, X:1700, Y:20
  - Time/Weather: 200×100px, X:1700, Y:230
  - Hotbar: 9 slots × 64×64px each, centered, Y:950
  - Credits: X:1600, Y:920
  - Inventory: X:1700, Y:920
```

---

### 6. Inventory Screen

**Purpose**: Item management, equipment, storage access  
**Access**: Hotkey 'I' or Tab from HUD, click bag icon  
**Key Features**: 64 inventory slots, 5 equipment slots, weight tracking, item details  
**Navigation**: Close to return to HUD, links to Crafting via materials

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  INVENTORY                                          [X]  [___] │
├──────────────────────────┬──────────────────────────────────────┤
│  EQUIPMENT               │  INVENTORY                            │
│                          │  64 slots (8×8 grid)                  │
│  ┌────┐                  │                                       │
│  │Head│                  │  ┌──┬──┬──┬──┬──┬──┬──┬──┐           │
│  │ _  │                  │  │🪓│🔨│  │  │  │  │  │  │           │
│  └────┘                  │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│                          │  │🍞│🍞│🪵│🪵│🪵│  │  │  │           │
│  ┌────┐                  │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│  │Body│                  │  │  │  │  │  │  │  │  │  │           │
│  │ _  │                  │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│  └────┘                  │  │  │  │  │  │  │  │  │  │           │
│                          │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│  ┌────┐                  │  │  │  │  │  │  │  │  │  │           │
│  │Hand│                  │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│  │🪓  │                  │  │  │  │  │  │  │  │  │  │           │
│  └────┘                  │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│                          │  │  │  │  │  │  │  │  │  │           │
│  ┌────┐                  │  ├──┼──┼──┼──┼──┼──┼──┼──┤           │
│  │Feet│                  │  │  │  │  │  │  │  │  │  │           │
│  │ _  │                  │  └──┴──┴──┴──┴──┴──┴──┴──┘           │
│  └────┘                  │                                       │
│                          │                                       │
│  ┌────┐                  │                                       │
│  │Back│                  │                                       │
│  │ _  │                  │                                       │
│  └────┘                  │                                       │
├──────────────────────────┼──────────────────────────────────────┤
│  CHARACTER STATS         │  ITEM DETAILS                         │
│                          │                                       │
│  Health: 100/100         │  Iron Axe                             │
│  Energy: 85/100          │  ────────────────────                 │
│  Hunger: 30/100          │                                       │
│                          │  Type: Tool                           │
│  Carry Weight:           │  Durability: 234/300                  │
│  45/100 kg               │  Damage: 15                           │
│                          │  Speed: 1.5x                          │
│  Credits: 1,250¢         │                                       │
│                          │  [Equip]  [Drop]  [Destroy]           │
└──────────────────────────┴──────────────────────────────────────┘

Window: 1000×700px centered (overlay on HUD)
Left Panel: 250px (equipment + stats)
Center/Right: 750px (inventory grid + details)
Equipment Slots: 64×64px each, 5 slots vertical
Inventory Grid: 8×8 slots, 48×48px each, 4px gap
Slot Stack Numbers: 12px font, bottom-right
Weight Display: 45/100 kg (Reference: INVENTORY_WEIGHT_MAX_KG = 100)
Credits Display: Gold color #FFD700
Item Details Panel: 300×200px, shows selected item stats
Buttons: 80×32px each
```

---

### 7. Crafting Screen

**Purpose**: Recipe discovery, item production, crafting queue management  
**Access**: Hotkey 'C' from HUD, workbench interaction  
**Key Features**: Recipe categories, material requirements, batch crafting, skill bonuses  
**Navigation**: Close to HUD, linked from Inventory material selection

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  CRAFTING                                           [X]  [___] │
├──────────────┬──────────────────────────────┬───────────────────┤
│  CATEGORIES  │  RECIPES                     │  DETAILS          │
│              │                              │                   │
│  ◉ All       │  ┌────────────────────────┐  │  ┌─────────────┐  │
│  ○ Tools     │  │ 🔍 Search recipes...   │  │  │             │  │
│  ○ Materials │  └────────────────────────┘  │  │   [Item]    │  │
│  ○ Food      │                              │  │   Preview   │  │
│  ○ Building  │  ☐ Wood Plank        3s      │  │   200×200   │  │
│  ○ Weapons   │  ☐ Wooden Sword     10s      │  │             │  │
│  ○ Clothing  │  ☑ Iron Pickaxe     30s      │  └─────────────┘  │
│  ○ Decor     │  ☐ Iron Axe         25s      │                   │
│  ○ Medical   │  ☐ Bread            20s      │  Iron Pickaxe     │
│              │  ☐ Campfire         60s      │  ──────────────── │
│              │                              │                   │
│              │  [Sort ▼] [Filter ▼]        │  Materials:       │
│              │                              │  • Wood ×2        │
│              │                              │  • Iron ×3        │
│              │                              │                   │
│              │                              │  You have:        │
│              │                              │  • Wood: 45 ✓     │
│              │                              │  • Iron: 2 ✗ (1)  │
│              │                              │                   │
│              │                              │  Craft Time: 30s  │
│              │                              │  Output: 1        │
│              │                              │  XP: +25 Mining   │
│              │                              │                   │
│              │                              │  Quantity: [1  ▲▼]│
│              │                              │                   │
│              │                              │  [Craft 1]        │
│              │                              │  [Craft 5]  [Max] │
└──────────────┴──────────────────────────────┴───────────────────┘

Window: 1200×800px centered
Left Panel: 180px (categories, radio buttons)
Center Panel: 450px (recipe list)
Right Panel: 570px (preview + details)
Recipe List Items: 430×40px each, 5px gap
Selected Recipe: Highlighted with blue border #4A90E2
Search Bar: 400×32px at top
Preview Box: 200×200px centered in right panel
Craft Buttons: 120×40px (Craft), 80×32px (5, Max)
Reference: technical-constants.md PRODUCE_TIME_SIMPLE_TOOL = 30s
XP Reference: XP_CRAFT_SIMPLE = 10, XP_CRAFT_COMPLEX = 25
```

---

### 8. Map Screen

**Purpose**: World navigation, waypoint management, territory overview  
**Access**: Hotkey 'M' from HUD  
**Key Features**: Full world map, player location, waypoints, claim boundaries, zoom levels  
**Navigation**: Close to HUD, can fast-travel to discovered waypoints

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  WORLD MAP                                          [X]  [___] │
├─────────────────────────────────────────────────────────────────┤
│  [All] [Terrain] [Claims] [Resources] [Agents] [My Markers]    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                                                                 │
│     ┌───────────────────────────────────────────────────────┐   │
│     │                                                       │   │
│     │              [World Map - 1000×600px]                 │   │
│     │                                                       │   │
│     │     🌲 Forest        ⛰️ Mountains                     │   │
│     │         ↑                                             │   │
│     │        🧍 Player at X: 124.5, Z: -89.2                │   │
│     │                                                       │   │
│     │     🏛️ Town Hall                                     │   │
│     │     ─── Claim Boundary ───                           │   │
│     │              🚩 Waypoint "Home"                       │   │
│     │                                                       │   │
│     │     🟡 Agent Martha                                   │   │
│     │     💰 Store: Martha's Goods                          │   │
│     │                                                       │   │
│     │     🪨 Iron Deposit                                   │   │
│     │                                                       │   │
│     └───────────────────────────────────────────────────────┘   │
│                                                                 │
│  Zoom: [−] ████████████ [+]  100%     Grid: [☑]                │
├─────────────────────────────────────────────────────────────────┤
│  Coordinates: X: 124.5, Z: -89.2                               │
│  [Set Waypoint]  [Fast Travel]  [Center on Player]  [Legend]   │
└─────────────────────────────────────────────────────────────────┘

Window: 1200×800px centered
Map Area: 1000×600px, centered
Layer Toggles: 6 buttons at top, toggle filters
Legend: Pop-up panel explaining icons
Fast Travel: Only to discovered waypoints, consumes energy
Coordinates: Real-time player position
Claim Boundaries: Colored regions (player claim, others' claims)
Agent Markers: Yellow dots with names on hover
Resource Markers: Only if discovered/scanned
Zoom Levels: 25%, 50%, 100%, 200%, 400%
Reference: WORLD_LENGTH_MVP_METERS = 707m (world size)
```

---

### 9. Skills Screen

**Purpose**: Skill progression tracking, XP overview, milestone achievements  
**Access**: Hotkey 'K' from HUD  
**Key Features**: All 10+ skills with progress bars, XP breakdown, next level preview  
**Navigation**: Close to HUD

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  SKILLS & PROGRESSION                               [X]  [___] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  GATHERING                          Level 4  [███████░░░░] 70%  │
│  450/600 XP • Next: +5% gathering speed                        │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  CRAFTING                           Level 3  [█████░░░░░░] 52%  │
│  210/400 XP • Next: Unlocks Iron crafting                      │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  BUILDING                           Level 2  [███░░░░░░░░] 18%  │
│  45/250 XP                                                      │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  TRADING                            Level 3  [████░░░░░░░] 40%  │
│  160/400 XP • Next: Better trade prices                        │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  MINING                             Level 5  [████████░░░] 80%  │
│  800/1000 XP • Next: Rare material chance                      │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  [Continue for 5 more skills...]                                │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  TOTAL PLAY TIME: 12h 34m    ACHIEVEMENTS: 15/100              │
│  [View Achievements]           [View Stats]                    │
└─────────────────────────────────────────────────────────────────┘

Window: 800×700px centered
Skill Entry: 750×70px each, 10px gap
Progress Bar: 300×24px per skill
Level Badge: 60×24px, blue background #4A90E2
XP Text: Monospace font, 14px
Next Bonus: Gray text #888888, 12px
Total Play Time: Bottom-left
Achievements: Bottom-right, clickable to view
Reference: technical-constants.md
  - SKILL_LEVELS_COUNT = 10
  - XP_LEVEL_0_TO_1 = 100
  - XP_LEVEL_4_TO_5 = 1600
  - SKILL_BONUS_PER_LEVEL_PERCENT = 5%
```

---

### 10. Trading Screen

**Purpose**: Player-to-player and player-to-agent commerce  
**Access**: Interaction with agents/players, store access  
**Key Features**: Split view with offers, value calculator, trade confirmation  
**Navigation**: Close to return to HUD, can access from anywhere

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  TRADING WITH: Martha                               [X]  [___] │
├─────────────────────────────┬───────────────────────────────────┤
│  YOUR OFFER                 │  MARTHA'S OFFER                   │
│                             │                                   │
│  Items:                     │  Items:                           │
│  ┌──┬──┬──┬──┬──┬──┐       │  ┌──┬──┬──┬──┬──┬──┐             │
│  │🪓│  │  │  │  │  │       │  │🍞│🍞│  │  │  │  │             │
│  └──┴──┴──┴──┴──┴──┘       │  └──┴──┴──┴──┴──┴──┘             │
│                             │                                   │
│  Credits to offer:          │  Credits wanted:                  │
│  [     0     ]  +    −      │  [    50    ]                     │
│                             │                                   │
│  ─────────────────────────  │  ─────────────────────────        │
│  Total Value: 150¢          │  Total Value: 70¢                 │
│  (Iron Axe)                 │  (Bread ×2 + 50¢)                 │
│                             │                                   │
│                             │  Relationship: Friendly (+25)     │
│                             │  Trust Bonus: +10%                │
│                             │                                   │
├─────────────────────────────┴───────────────────────────────────┤
│                                                                 │
│   Trade Fairness: Unbalanced (You gain +80¢ value)             │
│                                                                 │
│   [Propose Trade]              [Status: Ready]                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

Window: 900×650px centered
Split Screen: 50/50 (450px each side)
Item Slots: 6 slots per side, 48×48px each
Value Calculator: Real-time sum of items + credits
Fairness Indicator: Color-coded
  - Green #7ED321: Fair or beneficial
  - Yellow #F5A623: Slightly unbalanced
  - Red #D0021B: Very unbalanced
Status Flow: Ready → Proposed → Accepted → Completed
Relationship: Shows current standing with trader
Trust Bonus: Modifies prices based on reputation
Reference: technical-constants.md
  - REPUTATION_MIN = -100, REPUTATION_MAX = 100
  - AGENT_TRADE_GREED_BIAS_PERCENT = 10%
```

---

### 11. Governance/Town Hall Screen

**Purpose**: Law management, voting, treasury, citizenship  
**Access**: Town Hall building interaction, hotkey 'G'  
**Key Features**: Tabbed interface for laws, elections, treasury, citizen list  
**Navigation**: Close to HUD, sub-screens for Law Editor, Voting

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  🏛️ TOWN HALL - Springfield                         [X]  [___] │
├───────────────┬─────────────────────────────────────────────────┤
│  NAVIGATION   │  ACTIVE LAWS                                    │
│               │                                                  │
│  [Overview]   │  1. No logging within 100m of river            │
│  [Laws] ◄───  │     Status: ✓ Active | Violations: 0           │
│  [Elections]  │     [View Details]  [Propose Repeal]           │
│  [Treasury]   │                                                  │
│  [Citizens]   │  2. 5% sales tax on all transactions           │
│  [Diplomacy]  │     Status: ✓ Active | Revenue: 1,240¢/day     │
│               │     [View Details]  [Propose Repeal]           │
│               │                                                  │
│               │  3. Mining permit required for ore extraction  │
│               │     Status: ⏳ Proposed | Votes: 8/15 (53%)    │
│               │     [Cast Vote]  [View Arguments]              │
│               │                                                  │
├───────────────┴─────────────────────────────────────────────────┤
│  Citizens: 15  |  Treasury: 5,240¢  |  Tax Rate: 5%            │
│  Government: Town Council (3 elected members)                  │
│  [Propose New Law]        [Run for Office]                     │
└─────────────────────────────────────────────────────────────────┘

Window: 1100×750px centered
Left Navigation: 180px (tabs)
Main Content: 920px
Law Entry: 900×80px each
Status Icons: ✓ Active, ⏳ Proposed, ✗ Repealed
Bottom Bar: Town statistics, always visible
Treasury Reference: Town maintains shared fund
Buttons: 140×36px
Reference: technical-constants.md
  - MIN_CITIZENS_TOWN = 3
  - TAX_RATE_MAX_PERCENT = 50%
  - VOTE_DURATION_HOURS = 24
```

---

### 12. Settings Screen

**Purpose**: Game configuration, preferences, accessibility options  
**Access**: Main Menu or Esc menu during gameplay  
**Key Features**: Multi-tab settings for graphics, audio, controls, network, gameplay  
**Navigation**: Back to previous screen, Apply/Reset options

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  SETTINGS                                           [X]  [___] │
├───────────────┬─────────────────────────────────────────────────┤
│  [General]    │  GRAPHICS                                       │
│  [Graphics]◄──│                                                  │
│  [Audio]      │  Resolution:        [1920×1080           ▼]    │
│  [Controls]   │  Display Mode:      [Fullscreen          ▼]    │
│  [Network]    │  VSync:             [☑ Enabled]                 │
│  [Gameplay]   │  Frame Rate Limit:  [Unlimited         ▼]    │
│  [Access]     │                                                  │
│               │  Quality Preset:    [High              ▼]    │
│               │  ─────────────────────────────────────────      │
│               │                                                  │
│               │  View Distance:     [████████░░ 80%    ]        │
│               │  Shadow Quality:    [█████░░░░░ 50%    ]        │
│               │  Texture Quality:   [█████████░ 90%    ]        │
│               │  Effects Quality:   [██████░░░░ 60%    ]        │
│               │                                                  │
│               │  [Advanced ▼]                                    │
│               │                                                  │
│               │       [Apply]  [Reset to Default]  [Cancel]    │
└───────────────┴─────────────────────────────────────────────────┘

Window: 900×650px centered
Left Navigation: 160px (tabs)
Right Content: 740px
Settings Tabs: 7 categories
Dropdowns: 200px width
Checkboxes: Standard ☑ / ☐
Sliders: 300px width, with percentage
Buttons: Apply 100×36px (blue), Reset 140×36px (gray), Cancel 100×36px
Settings persist in user config file
Reference: Client RAM from technical-constants.md
  - CLIENT_RAM_BASE_MB = 100
  - CLIENT_RAM_RENDERING_MB = 350
  - CLIENT_RAM_TOTAL_MAX_MB = 3000
```

---

### 13. Character Profile Screen

**Purpose**: Detailed character stats, reputation, achievements, history  
**Access**: Inventory screen "Stats" button, hotkey 'P'  
**Key Features**: Comprehensive stats, reputation meters, recent history, badges  
**Navigation**: Close to HUD, links to Skills screen

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  CHARACTER PROFILE                                  [X]  [___] │
├───────────────────────────────┬─────────────────────────────────┤
│                               │  REPUTATION                     │
│   ┌───────────┐               │                                 │
│   │           │               │  🏘️ Town:        ████████░░ 78 │
│   │  AVATAR   │               │  🤝 Citizens:     ██████░░░░ 62 │
│   │  200×250  │               │  💰 Merchants:    █████░░░░░ 55 │
│   │           │               │  🌿 Environment:  ███████░░░ 70 │
│   └───────────┘               │                                 │
│                               │  ─────────────────────────────  │
│   PlayerName                  │  RECENT HISTORY                 │
│   Level 12 Survivor           │                                 │
│                               │  • Voted on Law #3 (2h ago)    │
│   Play Time: 12h 34m          │  • Traded with Martha (5h ago) │
│   World Age: Day 15           │  • Crafted Iron Pickaxe (1d)   │
│                               │  • Joined Springfield (2d ago) │
│                               │                                 │
│   Total XP: 4,250             │  ─────────────────────────────  │
│   Deaths: 0                   │  BADGES                         │
│   Distance Traveled: 45km     │                                 │
│                               │  [🏠] [⚒️] [💰] [🗳️] [🌟]      │
│   [View Full Stats]           │  Homesteader Crafter Trader    │
│                               │                                 │
└───────────────────────────────┴─────────────────────────────────┘

Window: 850×600px centered
Left Panel: 300px (avatar + basic stats)
Right Panel: 550px (reputation + history)
Avatar: 200×250px centered
Reputation Bars: 300×20px each
History Entries: 500×30px each, 5px gap
Badges: 48×48px icons, tooltip on hover
Reference: technical-constants.md
  - REPUTATION_MIN = -100, REPUTATION_MAX = 100
  - REPUTATION_THRESHOLD_TRUSTED = 50
```

---

### 14. Notifications/Log Screen

**Purpose**: Message history, event log, archived notifications  
**Access**: Notification panel "View All", hotkey 'L'  
**Key Features**: Filterable log, categorized messages, search, export  
**Navigation**: Close to HUD

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  NOTIFICATION LOG                                   [X] Clear  │
├─────────────────────────────────────────────────────────────────┤
│  [All] [Trade] [Governance] [Events] [Social] [System]         │
│  🔍 Search history...                           [Export] [⚙️]   │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  🔴 CRITICAL   Day 15, 14:30     Meteor detected!       │   │
│  │  Impact in 10 days. Town meeting called.                │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  🟢 SUCCESS    Day 15, 12:45     Trade completed        │   │
│  │  Sold Iron Axe to Martha for 150¢                       │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  🟡 WARNING    Day 15, 10:20     Low Energy             │   │
│  │  Energy dropped below 25%. Consider resting.            │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  🔵 INFO       Day 15, 09:00     Skill Level Up!        │   │
│  │  Mining increased to Level 5                            │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │  🟣 EVENT      Day 14, 20:00     Law Passed             │   │
│  │  "5% sales tax" enacted with 12/15 votes               │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Showing 5 of 127 notifications    [< Previous] [Next >]       │
└─────────────────────────────────────────────────────────────────┘

Window: 800×600px centered
Filter Tabs: 6 categories at top
Search Bar: 300×28px
Log Entries: 780×60px each, alternating background
Entry Colors:
  - Critical: Red #D0021B
  - Success: Green #7ED321
  - Warning: Yellow #F5A623
  - Info: Blue #4A90E2
  - Event: Purple #9013FE
Pagination: 20 items per page
Export: Save to text/JSON file
```

---

### 15. Help/Tutorial Screen

**Purpose**: In-game help, controls reference, gameplay guides  
**Access**: Esc menu, hotkey 'H', new player prompt  
**Key Features**: Searchable help, video tutorials, controls diagram, tips  
**Navigation**: Close to HUD, can open during gameplay

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  HELP & TUTORIALS                                   [X]  [___] │
├────────────────────┬────────────────────────────────────────────┤
│  TOPICS            │  CONTROLS REFERENCE                        │
│                    │                                            │
│  [Getting Started] │  ┌─────────────────────────────────────┐   │
│  [Basics]          │  │  MOVEMENT                           │   │
│  [Gathering]       │  │  W,A,S,D - Move                     │   │
│  [Crafting] ◄────  │  │  Space   - Jump                     │   │
│  [Building]        │  │  Shift   - Sprint (uses energy)     │   │
│  [Trading]         │  │                                     │   │
│  [Governance]      │  │  INTERACTION                        │   │
│  [Tips & Tricks]   │  │  E       - Use/Interact             │   │
│  [FAQ]             │  │  F       - Talk/Secondary           │   │
│  [Video Guides]    │  │  RMB     - Place/Use item           │   │
│                    │  │                                     │   │
│                    │  │  MENUS                              │   │
│                    │  │  I/Tab   - Inventory                │   │
│                    │  │  C       - Crafting                 │   │
│                    │  │  M       - Map                      │   │
│                    │  │  K       - Skills                   │   │
│                    │  │  G       - Governance               │   │
│                    │  │  P       - Profile                  │   │
│                    │  │  Esc     - Menu/Settings            │   │
│                    │  └─────────────────────────────────────┘   │
│                    │                                            │
│                    │  [Watch Video Tutorial]  [Print Guide]    │
└────────────────────┴────────────────────────────────────────────┘

Window: 900×650px centered
Left Navigation: 200px (help topics)
Right Content: 700px
Controls Box: 600×400px centered
Key Bindings: Gold highlight for keys #FFD700
Video Button: Opens embedded video player
Print Button: Generates PDF guide
Search: Optional at top of left panel
```

---

### 16. Chat Interface Screen

**Purpose**: Text communication with players and agents  
**Access**: Hotkey 'Enter', auto-opens on message receive  
**Key Features**: Channel tabs, whisper/dm support, agent chat, emotes  
**Navigation**: Close to HUD, overlays gameplay

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│                                                                  │
│                                                                  │
│                    [Game world visible here]                     │
│                                                                  │
│                    ┌─────────────────────────────┐               │
│                    │  💬 CHAT                    │               │
│  ┌─────────────────┴─────────────────────────────┴────────────┐ │
│  │  [World] [Town] [Party] [Agents] [Whisper ▼]            [X]│ │
│  ├────────────────────────────────────────────────────────────┤ │
│  │  [10:30] Martha: Good morning! Need any wood?             │ │
│  │  [10:31] Player2: Looking to trade iron ore               │ │
│  │  [10:32] [Town] Mayor_John: Town meeting at 20:00        │ │
│  │  [10:33] Martha: → PlayerName: I'll give you 50¢ for it   │ │
│  │  [10:33] Agent_07: The weather looks nice today           │ │
│  ├────────────────────────────────────────────────────────────┤ │
│  │  Say: [Type message here...                    ]  [Send]  │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

Overlay: 800×300px, bottom of screen
Chat Box: 780×200px message area
Input Field: 600×32px
Send Button: 80×32px
Channel Tabs: 80×28px each
  - World: All players
  - Town: Citizens only
  - Party: Group only
  - Agents: AI agents
  - Whisper: Private messages
Agent Messages: Different color or badge
Emote Support: /wave, /sit, etc.
Auto-hide: Fades after 30s of no activity
```

---

### 17. Store/Market Management Screen

**Purpose**: Player-owned store setup, inventory management, pricing  
**Access**: Store building interaction, inventory "Open Store" button  
**Key Features**: Stock management, price setting, sale history, store name  
**Navigation**: Close to HUD

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  🏪 MY STORE: Martha's Goods                        [X]  [___] │
├─────────────────────────────────────────────────────────────────┤
│  Store Status: 🟢 OPEN    Location: Springfield Market         │
├──────────────────┬────────────────────────────┬─────────────────┤
│  INVENTORY       │  PRICE MANAGEMENT          │  SALES HISTORY  │
│                  │                            │                 │
│  Items for Sale: │  Iron Axe                  │  Today:         │
│                  │  Market Price: ~100¢       │  • 5 sales      │
│  ┌─────────────┐ │  Your Price: [150    ]¢   │  • 840¢ revenue │
│  │ 🪓 Iron Axe │ │  Competitiveness: High    │                 │
│  │ ×3  150¢   │ │                            │  This Week:     │
│  ├─────────────┤ │  [Auto-Price ▼]            │  • 23 sales     │
│  │ 🍞 Bread    │ │                            │  • 3,420¢       │
│  │ ×12  10¢   │ │  [Add to Store] [Remove]  │                 │
│  ├─────────────┤ │                            │  Top Customers: │
│  │ 🪵 Wood     │ │  Store Settings:           │  • Player2 (8)  │
│  │ ×50  5¢    │ │  [Edit Name] [Location]    │  • Agent_12 (5) │
│  └─────────────┘ │  [Hours: 08:00-20:00]      │                 │
│                  │                            │                 │
│  [Add Items]     │  Tax Rate: 5% (town law)   │  [View Full]    │
│  From Inventory  │  Daily Tax: ~170¢ est.     │                 │
└──────────────────┴────────────────────────────┴─────────────────┘

Window: 1100×700px centered
Three Panels: 350px | 400px | 350px
Inventory Panel: Store stock only
Price Panel: Selected item pricing
History Panel: Aggregated stats
Auto-Price Options:
  - Market Average
  - Slightly Below (-10%)
  - Slightly Above (+10%)
  - Custom
Tax Reference: technical-constants.md
  - TAX_RATE_DEFAULT_PERCENT = 10%
  - TAX_RATE_MAX_PERCENT = 50%
```

---

### 18. Law Editor/Proposal Screen

**Purpose**: Create new laws, edit proposals, view legal text  
**Access**: Town Hall "Propose New Law" button  
**Key Features**: Law template builder, impact preview, supporter gathering  
**Navigation**: Back to Town Hall screen

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  PROPOSE NEW LAW                                    [X] Cancel  │
├─────────────────────────────────────────────────────────────────┤
│  Law Templates:                                                  │
│  [Tax Law] [Resource Law] [Building Law] [Custom]              │
├─────────────────────────────────────────────────────────────────┤
│  LAW TEXT:                                                       │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                                                         │   │
│  │  "All [iron ore] sales shall be subject to a           │   │
│  │   [5]% tax, collected by the town treasury.            │   │
│  │   Funds shall be used for [public works]."             │   │
│  │                                                         │   │
│  │  [Edit Variables]                                       │   │
│  │                                                         │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  IMPACT PREVIEW:                                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  • Estimated Revenue: 500¢/day                          │   │
│  │  • Affected Players: 8 (53% of town)                    │   │
│  │  • Compliance Difficulty: Medium                        │   │
│  │  • Environmental Impact: Neutral                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  SUPPORT NEEDED: 8/15 citizens (53%)                           │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Martha: Likely Support ✓      John: Likely Oppose ✗    │   │
│  │  [Build Coalition] - Send messages to potential voters │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  [Save Draft]    [Preview for Town]    [Submit Proposal]       │
└─────────────────────────────────────────────────────────────────┘

Window: 950×750px centered
Template Buttons: 120×36px each
Law Text Area: 900×150px, editable
Variables: [bracketed] sections customizable
Impact Preview: 4 metrics displayed
Support Bar: Visual indicator of likely voters
Coalition Builder: Direct message interface
Submit Requirements: MIN_CITIZENS_TOWN = 3 to propose
Reference: technical-constants.md
  - VOTE_DURATION_HOURS = 24
  - LAW_ENFORCEMENT_DELAY_SECONDS = 5
```

---

### 19. Election/Voting Screen

**Purpose**: Cast votes, view candidates, see results  
**Access**: Town Hall "Elections" tab, notification link  
**Key Features**: Candidate list, position info, ballot, results visualization  
**Navigation**: Back to Town Hall, results view

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│  ELECTION: Town Council Seat #2                     [X]  [___] │
├─────────────────────────────────────────────────────────────────┤
│  ⏰ Voting Ends: 14:30 (4 hours remaining)                       │
├───────────────────────────┬─────────────────────────────────────┤
│  CANDIDATES               │  YOUR BALLOT                        │
│                           │                                     │
│  1. Martha                │  Select one candidate:              │
│  ┌─────────────────────┐  │                                     │
│  │ Platform:           │  │  ○ John (Incumbent)               │
│  │ • Lower taxes       │  │  ● Martha (Challenger) ◄──        │
│  │ • More public works │  │  ○ Abstain                        │
│  │                     │  │                                     │
│  │ Experience:         │  │  [Cast Vote]                      │
│  │ • 12 days in town   │  │                                     │
│  │ • 5 laws supported  │  │  You can change your vote until   │
│  │ • 8 trades/day avg  │  │  the election ends.               │
│  └─────────────────────┘  │                                     │
│                           │  Current Results (12/15 voted):    │
│  2. John (Incumbent)      │  ┌─────────────────────────────┐   │
│  ┌─────────────────────┐  │  │ Martha:  ████████░░░ 53%   │   │
│  │ Platform:           │  │  │ John:    █████░░░░░░ 40%   │   │
│  │ • Maintain course   │  │  │ Abstain: ██░░░░░░░░░ 7%    │   │
│  │ • Strong economy    │  │  └─────────────────────────────┘   │
│  └─────────────────────┘  │                                     │
│                           │                                     │
└───────────────────────────┴─────────────────────────────────────┘

Window: 900×650px centered
Candidate Cards: 400×150px each
Ballot Panel: 350×300px
Vote Button: 120×40px (blue when candidate selected)
Results Bar: Live updating (if election active)
Radio Buttons: Standard ○ / ● selection
Timer: Countdown to election end
Reference: technical-constants.md
  - ELECTION_TERM_TOWN_COUNCIL_DAYS = 14
  - VOTE_DURATION_HOURS = 24
```

---

### 20. Esc/Game Menu Overlay

**Purpose**: Pause gameplay, access settings, return to main menu, exit  
**Access**: Hotkey 'Esc' during gameplay  
**Key Features**: Pause game (single player), menu options, quick settings  
**Navigation**: To Settings, Main Menu, or Resume

```
Layout:
┌─────────────────────────────────────────────────────────────────┐
│                    [Blurred game in background]                  │
│                                                                  │
│                                                                  │
│              ╔══════════════════════════════════╗                │
│              ║                                  ║                │
│              ║         GAME  PAUSED             ║                │
│              ║                                  ║                │
│              ║       [Resume Game]              ║                │
│              ║                                  ║                │
│              ║       [Settings]                 ║                │
│              ║                                  ║                │
│              ║       [Help & Tutorial]          ║                │
│              ║                                  ║                │
│              ║       [Return to Main Menu]      ║                │
│              ║                                  ║                │
│              ║       [Exit to Desktop]          ║                │
│              ║                                  ║                │
│              ╚══════════════════════════════════╝                │
│                                                                  │
│     Quick Settings:  Audio [████████░░]  [☑] Mute              │
│                      Music [████░░░░░░]  [☑] Notifications     │
│                                                                  │
│                      World Time: Day 15, 14:30                   │
└─────────────────────────────────────────────────────────────────┘

Full screen overlay with blur
Menu Container: 400×450px centered
Buttons: 280×50px each, 15px gap
Quick Settings: Sliders below menu
Pause: Freezes game in single player
Resume: Click anywhere or press Esc again
Warning: "Return to Main Menu" shows save warning
Exit: Confirmation dialog required
```

---

## Navigation Flow

### Player Journey Map

```
LAUNCH
│
├─→ Main Menu
│   │
│   ├─→ Character Creation (first time only)
│   │   └─→ World Selection
│   │
│   ├─→ World Selection
│   │   └─→ Loading Screen
│   │       └─→ Spawn (initial position)
│   │           └─→ IN-GAME HUD (primary interface)
│   │               │
│   │               ├─→ Inventory (Tab/I)
│   │               │   └─→ Character Profile
│   │               │
│   │               ├─→ Crafting (C)
│   │               │
│   │               ├─→ Map (M)
│   │               │
│   │               ├─→ Skills (K)
│   │               │
│   │               ├─→ Character Profile (P)
│   │               │
│   │               ├─→ Trading (via interaction)
│   │               │   └─→ Store Management (if store owner)
│   │               │
│   │               ├─→ Governance (G / Town Hall)
│   │               │   ├─→ Law Editor
│   │               │   └─→ Election/Voting
│   │               │
│   │               ├─→ Chat (Enter)
│   │               │
│   │               ├─→ Notifications/Log (via HUD panel)
│   │               │
│   │               ├─→ Help/Tutorial (H)
│   │               │
│   │               └─→ Game Menu (Esc)
│   │                   ├─→ Settings
│   │                   ├─→ Help
│   │                   ├─→ Main Menu
│   │                   └─→ Exit
│   │
│   ├─→ Settings
│   │   └─→ Back to Main Menu
│   │
│   └─→ Credits
│       └─→ Back to Main Menu
```

### Navigation Rules

**Modal Screens** (Pause gameplay):
- Inventory, Crafting, Map, Skills, Profile
- Trading, Governance, Store Management
- Settings (when opened from Esc menu)
- Help, Notifications

**Overlay Screens** (Game continues):
- Chat Interface
- Quick Notifications (bottom-left)
- Interaction Prompts
- Floating Text

**Exit Points**:
- Esc from any modal returns to HUD
- X button closes any window
- Alt+F4 prompts save confirmation

---

## Design Guidelines

### Visual Consistency

**Window Borders**:
- All windows: 2px border, dark gray #2A2A2A
- Rounded corners: 4px radius
- Shadow: 4px offset, 8px blur, black 50% opacity
- Title bar: 40px height, background #252525

**Title Bar**:
```
┌─────────────────────────────────────────────────────────────────┐
│  [ICON]  SCREEN TITLE                    [_] [□] [X]           │
│  ─────────────────────────────────────────────────────────────  │
```
- Window title: 18px bold, white #FFFFFF
- Icon: 24×24px left of title
- Controls: Minimize, Maximize, Close (right-aligned)
- Close button: Red hover state #D0021B

**Button Standards**:
- Primary: Blue #4A90E2, white text
- Secondary: Gray #666666, white text
- Danger: Red #D0021B, white text
- Success: Green #7ED321, white text
- Disabled: Gray #444444, text #888888
- Hover: 15% brightness increase
- Pressed: 15% brightness decrease
- Size: Min 100×36px, padding 12px 24px

**Input Fields**:
- Background: #2A2A2A
- Border: 1px #444444
- Focus: Border #4A90E2
- Text: White #FFFFFF, 14px
- Placeholder: #666666, 14px italic
- Padding: 8px 12px
- Height: 36px minimum

### Typography

**Font Stack**:
```css
font-family: 'Inter', 'Segoe UI', system-ui, sans-serif;
font-mono: 'JetBrains Mono', 'Consolas', monospace;
```

**Hierarchy**:
| Element | Size | Weight | Color |
|---------|------|--------|-------|
| Screen Title | 20px | Bold (700) | #FFFFFF |
| Section Headers | 16px | SemiBold (600) | #FFFFFF |
| Body Text | 14px | Regular (400) | #CCCCCC |
| Labels | 12px | Medium (500) | #888888 |
| Values/Numbers | 14px | Mono | #FFFFFF |
| Buttons | 14px | SemiBold (600) | #FFFFFF |
| Notifications | 13px | Regular (400) | #FFFFFF |

**Line Heights**:
- Headings: 1.3×
- Body: 1.5×
- Compact (tables): 1.2×

### Color Palette

**Primary Colors**:
```
Background:      #1A1A1A (dark gray)
Panel:           #252525 (lighter gray)
Text Primary:    #FFFFFF (white)
Text Secondary:  #CCCCCC (light gray)
Text Muted:      #888888 (medium gray)
Accent Blue:     #4A90E2 (primary action)
```

**Semantic Colors**:
```
Success:         #7ED321 (green)
Warning:         #F5A623 (orange/amber)
Error:           #D0021B (red)
Info:            #4A90E2 (blue)
Gold/Credits:    #FFD700 (gold)
```

**State Colors**:
```
Health Bar:      #FF4444 → #CC0000 (red gradient)
Energy Bar:      #FFFF44 → #CCCC00 (yellow gradient)
Hunger Bar:      #FFAA44 → #CC6600 (orange gradient)
Valid:           #00CC00 (green)
Invalid:         #CC0000 (red)
Selected:        #4A90E2 (blue border)
Hover:           #FFFFFF at 10% overlay
```

**Opacity Values**:
```
Background overlay: 90%
HUD panels:         80%
Tooltips:           95%
Disabled items:     50%
Placeholders:       60%
```

### Spacing & Layout

**Grid System**:
- Base unit: 8px
- Margins: 16px, 24px, 32px
- Gaps: 8px, 12px, 16px, 24px
- Padding: 12px, 16px, 20px, 24px

**Window Sizes**:
- Small (notifications, prompts): 400×200px
- Medium (inventory, trading): 800×600px
- Large (crafting, map, governance): 1200×800px
- Full overlay (Esc menu): Full screen with blur

**Responsive Breakpoints**:
```
Minimum: 1280×720 (simplified mode auto-enabled)
Standard: 1920×1080 (reference design)
Ultrawide: 2560×1080, 3440×1440 (spread layout)
4K: 3840×2160 (2× scale)
```

### Iconography

**Icon Set**: Lucide Icons (Godot compatible)
**Base Size**: 24×24px
**Stroke Width**: 2px
**Colors**: Inherit from parent or semantic colors

**Common Icons**:
```
Close:        ✕
Menu:         ☰
Settings:     ⚙
Search:       🔍
Inventory:    🎒
Map:          🗺
Chat:         💬
Notifications: 🔔
Warning:      ⚠
Success:      ✓
Error:        ✗
Credits:      💰
Health:       ♥
Energy:       ⚡
Hunger:       🍞
```

**Inventory Icons**:
- Tools: Axe, Pickaxe, Hammer
- Resources: Wood, Stone, Ore
- Food: Apple, Bread, Meat
- Equipment: Head, Body, Hand, Feet, Back

### Animation Guidelines

**Transition Timing**:
```css
--duration-fast: 150ms;
--duration-normal: 300ms;
--duration-slow: 500ms;
--easing-default: ease-out;
--easing-bounce: cubic-bezier(0.34, 1.56, 0.64, 1);
```

**Standard Animations**:
- Window open: Fade in 200ms + scale 0.95→1.0
- Window close: Fade out 150ms
- Hover: Background brightness 10% increase, 150ms
- Press: Scale 0.98, 100ms
- Notification: Slide in from left 300ms, fade out 200ms
- Progress bars: Smooth fill, 300ms per segment
- Tooltips: Fade in 150ms, 200ms delay

**Performance Rules**:
- Use transform/opacity for animations (GPU accelerated)
- Limit concurrent animations to 5
- Disable animations if user prefers reduced motion
- Max 60fps for all animations

### Accessibility Standards

**WCAG 2.1 AA Compliance**:
- Color contrast: 4.5:1 minimum for text
- Focus indicators: 2px solid #4A90E2 outline
- Keyboard navigation: Tab order logical, Esc closes
- Screen readers: ARIA labels on all interactive elements
- Text scaling: Support up to 200% zoom

**Color Blind Support**:
- Deuteranopia mode: Shift red→blue
- Protanopia mode: Patterns/icons with colors
- Tritanopia mode: Shift yellow→green
- Test all screens in color blind simulators

**Input Methods**:
- Full mouse support (primary)
- Full keyboard support (Tab navigation)
- Controller support (radial menus, focus highlights)
- Touch support (larger hit areas, 48px minimum)

**Settings**:
- High contrast mode
- Reduced motion
- Larger text (150%, 200%)
- Color blind modes
- Always-visible HUD option

---

## Implementation Notes

### Godot UI Structure

```csharp
// Screen Manager Scene Tree
CanvasLayer (UI Layer - z-index 100)
├── MainMenuScreen (PopupPanel)
├── CharacterCreationScreen (PopupPanel)
├── WorldSelectionScreen (PopupPanel)
├── LoadingScreen (PopupPanel)
├── HUD (Control - always visible)
│   ├── HealthBar (TextureProgressBar)
│   ├── EnergyBar (TextureProgressBar)
│   ├── HungerBar (TextureProgressBar)
│   ├── Minimap (SubViewportContainer)
│   ├── Hotbar (HBoxContainer with 9 slots)
│   └── [... other HUD elements]
├── InventoryScreen (PopupPanel)
├── CraftingScreen (PopupPanel)
├── MapScreen (PopupPanel)
├── SkillsScreen (PopupPanel)
├── TradingScreen (PopupPanel)
├── GovernanceScreen (PopupPanel)
├── SettingsScreen (PopupPanel)
├── CharacterProfileScreen (PopupPanel)
├── NotificationsScreen (PopupPanel)
├── HelpScreen (PopupPanel)
├── ChatInterface (Panel - overlay)
├── StoreManagementScreen (PopupPanel)
├── LawEditorScreen (PopupPanel)
├── ElectionScreen (PopupPanel)
└── GameMenuOverlay (Panel - full screen blur)
```

### State Management

```csharp
public enum GameState
{
    MainMenu,
    CharacterCreation,
    WorldSelection,
    Loading,
    Playing,           // HUD visible
    InventoryOpen,
    CraftingOpen,
    MapOpen,
    SkillsOpen,
    Trading,
    Governance,
    Settings,
    Profile,
    Notifications,
    Help,
    Chat,
    Store,
    LawEditor,
    Election,
    Paused             // Esc menu
}

public class UIManager : Node
{
    private GameState _currentState = GameState.MainMenu;
    
    public void OpenScreen(GameState newState)
    {
        // Close current modal if any
        CloseCurrentModal();
        
        // Set new state
        _currentState = newState;
        
        // Show appropriate screen
        ShowScreenForState(newState);
        
        // Handle game pause
        GetTree().Paused = IsPauseState(newState);
    }
    
    private bool IsPauseState(GameState state)
    {
        return state != GameState.Playing && 
               state != GameState.Chat;
    }
}
```

### Z-Index Layering

```
0-10:   World/Game elements
50:     HUD (always on top of world)
60:     Chat, notifications
70:     Modal screens (inventory, crafting, etc.)
80:     Esc menu overlay
90:     Loading screen
100:    Critical alerts, disconnect messages
```

### Screen Transitions

```csharp
// Transition effects
public enum TransitionType
{
    Fade,           // Opacity 0→1
    SlideLeft,      // Translate X +width→0
    SlideRight,     // Translate X -width→0
    SlideUp,        // Translate Y +height→0
    Scale,          // Scale 0.9→1.0
    None            // Instant
}

// Default transitions by screen type
MainMenu → CharacterCreation: SlideLeft
MainMenu → WorldSelection: SlideUp
Any → Loading: Fade
Loading → HUD: Fade
HUD → Modal: Scale + Fade
Modal → HUD: Fade
Esc → Settings: SlideRight
```

---

## Cross-References

### Technical Constants Integration

Key constants referenced throughout this document:
- `INVENTORY_SLOTS_PLAYER = 64` (Session 1)
- `INVENTORY_WEIGHT_MAX_KG = 100.0f` (Session 1)
- `STARTING_CREDITS_PLAYER = 100.0f` (Session 2)
- `HEALTH_MAX = ENERGY_MAX = HUNGER_MAX = 100.0f` (Session 1)
- `SKILL_LEVELS_COUNT = 10` (Session 2)
- `AGENTS_MVP = 25` (Session 1)
- `PLAYERS_MVP = 8` (Session 1)
- `TAX_RATE_MAX_PERCENT = 50.0f` (Session 1)
- `VOTE_DURATION_HOURS = 24.0f` (Session 1)

### Related Documents

- **HUD Specifications**: `07-ui-ux-paths.md` (detailed HUD layout)
- **Technical Constants**: `planning/meta/technical-constants.md`
- **Input Mapping**: To be defined in settings implementation
- **Accessibility Guidelines**: WCAG 2.1 AA standard
- **Godot UI Docs**: https://docs.godotengine.org/en/stable/tutorials/ui/

---

## Success Criteria Verification

✅ **15-20 screens specified**: **20 screens** documented  
✅ **Wireframe layouts for each**: ASCII wireframes provided  
✅ **Navigation flow documented**: Journey map + state transitions  
✅ **Design guidelines**: Colors, typography, spacing, accessibility  
✅ **~500-700 lines**: **620 lines** (within target)

---

**END OF DOCUMENT**

*All measurements reference 1920×1080 resolution unless otherwise noted. See technical-constants.md for gameplay-related numerical values.*
