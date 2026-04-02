# Movement and Interaction Mechanics Specification

**Status**: SPECIFICATION COMPLETE  
**Session**: 3 - Core Gameplay Loops  
**Document**: 01c  
**Last Updated**: 2026-02-01  

---

## Overview

This document specifies all player movement mechanics, camera systems, and interaction systems for Societies. All numerical values reference `planning/meta/technical-constants.md` as the authoritative source.

---

## 1. Movement System

### 1.1 Basic Movement Modes

#### Walking (Default)
```yaml
Speed: 3.0 units/second  # MOVEMENT_SPEED_WALK from technical-constants.md
Stamina Cost: 0 per second
Control: Precise, full turning rate (360°/second)
Acceleration: 8.0 units/second²
Deceleration: 10.0 units/second²
Use Case: Default travel, precise positioning, resource gathering
```

#### Sprinting (Hold Shift)
```yaml
Speed: 6.0 units/second  # MOVEMENT_SPEED_SPRINT from technical-constants.md
Stamina Cost: 2.0 units/second  # SPRINT_STAMINA_COST_PER_SECOND
Minimum Stamina Required: 20.0 units  # SPRINT_MIN_STAMINA_TO_START
Control: Slightly reduced turning rate (270°/second)
Acceleration: 6.0 units/second² (slower to reach max speed)
Deceleration: 12.0 units/second² (faster to stop)
Use Case: Long distance travel, fleeing danger, urgent deliveries

Sprint Recovery:
  - Stamina must recover to 20.0 units before sprint can resume
  - Recovery delay: 2.0 seconds after stopping sprint before regen begins
  - Visual indicator: Stamina bar flashes when below minimum
```

#### Crouching (Hold Ctrl)
```yaml
Speed: 1.5 units/second (50% of walking speed)
Stamina Cost: 0 per second
Control: Very precise, reduced momentum
Height: Reduced to 60% of standing height (1.08 units vs 1.8 units)
Collision: Can fit through 1.0 meter gaps
Step Height: Reduced to 0.25 units (half of normal)
Use Case: Stealth approach, precise building placement, fitting through tight spaces

Stealth Mechanics:
  - Footstep audio volume: -50%
  - Detection radius by AI agents: -40%
  - Movement trail visibility: Reduced
```

#### Jumping (Space)
```yaml
Height: 1.2 units (peak of jump arc)
Stamina Cost: 15.0 units per jump
Cooldown: 0.5 seconds between jumps
Forward Momentum: Maintains 80% of current horizontal velocity
Vertical Velocity: 4.9 units/second initial (calculated for 1.2m height with gravity)
Gravity: 9.8 units/second² (standard earth gravity)
Air Control: 30% of ground control (limited steering while airborne)
Use Case: Clearing obstacles, reaching elevated areas, escape routes

Jump Timing:
  - Coyote time: 0.15 seconds (can jump briefly after leaving ground)
  - Pre-jump buffer: 0.1 seconds (input registered before landing)
  - Queue jump: Pressing jump during cooldown queues next jump
```

### 1.2 Carrying & Encumbrance

#### Weight Thresholds
```yaml
Light Load (0-25 kg):
  Speed: 100% (3.0 m/s walk, 6.0 m/s sprint)
  Stamina drain: Normal

Medium Load (25-50 kg):  # INVENTORY_WEIGHT_PLAYER_BASE_KG
  Speed: 80% (2.4 m/s walk, 4.8 m/s sprint)
  Stamina drain: +25%
  Visual: Slight character animation change

Heavy Load (50-75 kg):
  Speed: 60% (1.8 m/s walk, sprint disabled)
  Stamina drain: +50%
  Visual: Heavy breathing animation, slower movement
  Cannot: Sprint, jump higher than 0.5 units

Over-Encumbered (75-100 kg):  # INVENTORY_WEIGHT_MAX_KG
  Speed: 30% (0.9 m/s walk, no sprint)
  Stamina drain: +100%
  Cannot: Jump, sprint, interact with some objects
  Visual: Dramatically slowed animation
```

### 1.3 Stamina System

#### Stamina Range & States
```yaml
Maximum: 100.0 units  # STAMINA_MAX
Minimum: 0.0 units

Stamina States:
  Excellent (100-80):
    Indicator: Bright green
    Effects: Full movement capability
    
  Good (79-50):
    Indicator: Green
    Effects: Normal movement
    
  Reduced (49-30):
    Indicator: Yellow
    Effects: -5% movement speed, slight screen vignette
    Warning: "Getting tired" message at 40
    
  Depleted (29-10):
    Indicator: Orange
    Effects: -15% movement speed, moderate vignette, heavy breathing audio
    Cannot: Sprint below 20 units
    Warning: "Exhausted" message at 20
    
  Collapsed (9-0):
    Indicator: Red
    Effects: -30% movement speed, heavy vignette, forced heavy breathing
    Cannot: Sprint, jump
    Forced: Character enters "exhausted" animation when hitting 0
    Recovery: Must rest to 10 units before normal movement resumes
```

#### Stamina Regeneration
```yaml
Regeneration Rates (per second, converted from per-hour rates):
  Standing Still: 0.0083 units/second  # STAMINA_REGEN_REST_PER_HOUR / 3600
  Walking: 0.0028 units/second  # STAMINA_REGEN_WALK_PER_HOUR / 3600
  Sprinting: -2.0 units/second  # SPRINT_STAMINA_COST_PER_SECOND
  Jumping: -15.0 units (instant cost)
  Heavy Labor (mining, building): -0.083 units/second  # 5 units/minute
  Combat: -0.167 units/second  # 10 units/minute

Boosted Recovery:
  Sitting on furniture: 0.0167 units/second  # +10 units/second equivalent
  Lying in bed: 0.025 units/second  # +15 units/second equivalent
  
Instant Recovery:
  Energy food items: +20 to +40 units instantly
  Energy potions: +50 units instantly with 30-second cooldown
```

### 1.4 Physics & Collision

#### Player Capsule
```yaml
Height (Standing): 1.8 units
Height (Crouching): 1.08 units (60% of standing)
Radius: 0.4 units
Mass: 70 kg
Center of Mass: 0.9 units from ground (midpoint)

Collision Shape:
  Type: Capsule
  Physics Material: PlayerFriction (friction: 0.8, bounciness: 0.0)
  Continuous Collision: Enabled (prevents tunneling at high speeds)
```

#### Collision Layers
```yaml
Layer 1 - Terrain:
  Ground, rocks, natural formations
  Collides with: Player, AI agents, objects
  
Layer 2 - Buildings:
  Player-built structures, walls, floors
  Collides with: Player, AI agents, objects
  
Layer 3 - Objects:
  Furniture, containers, crafting stations
  Collides with: Player, AI agents
  
Layer 4 - Players:
  Other player characters
  Collides with: Terrain, buildings, objects (not other players - noclip)
  
Layer 5 - AI Agents:
  Non-player characters
  Collides with: Terrain, buildings, objects, other AI agents
  
Layer 6 - Resources:
  Trees, rocks, harvestable resources
  Collides with: Player, AI agents
  Interaction: Raycast detection for harvesting
  
Layer 7 - Triggers:
  Zones, detection areas, quest triggers
  Collides with: Player, AI agents (non-physical)
```

#### Terrain Interaction
```yaml
Slope Limit:
  Max Walkable Slope: 45 degrees
  Slippery Slope: > 45 degrees (player slides uncontrollably)
  Slide Speed: 3.0 m/s on slippery slopes
  Slide Control: 10% (minimal steering while sliding)
  
Step Height:
  Automatic Step Up: 0.5 units
  Step Speed: Instant (no slowdown for steps ≤ 0.5m)
  Higher Obstacles: Require jump
  
Surface Types:
  Dirt/Ground: 100% traction, normal movement
  Grass: 95% traction, -2% speed
  Stone: 100% traction, +5% traction for stopping
  Sand: 80% traction, -10% speed
  Snow: 60% traction, -20% speed, visible footprints
  Ice: 30% traction, -5% speed, momentum preserved
  Wood: 90% traction, -3% speed
  Metal: 85% traction, -5% speed, audible footsteps
```

### 1.5 Advanced Movement

#### Swimming
```yaml
Speed: 1.5 m/s (horizontal), 1.0 m/s (vertical)
Stamina Cost: 1.0 unit/second
Buoyancy: Neutral (maintains depth without input)
Diving: Hold Crouch to descend faster
Surfacing: Auto-surface at 10% stamina (forced)
Breath: 30 seconds underwater before drowning

Water Types:
  Fresh Water: Normal swimming
  Salt Water: Same as fresh (no difference in MVP)
  Polluted Water: -20% speed, health drain if submerged > 10 seconds
```

#### Falling
```yaml
Gravity: 9.8 m/s²
Terminal Velocity: 30 m/s (air resistance kicks in)
Fall Damage:
  Safe Height: < 3.0 meters (no damage)
  Minor Damage: 3.0-6.0 meters (5-15 HP)
  Major Damage: 6.0-10.0 meters (20-50 HP)
  Lethal: > 10.0 meters (instant death or 90+ HP damage)
  
Landing Recovery:
  Hard Landing: 0.5 second recovery animation
  Roll Landing: Hold Crouch on landing to reduce damage by 50%
```

---

## 2. Camera System

### 2.1 Third-Person Camera (Default)

#### Position & Orientation
```yaml
Base Distance: 4.0 units behind player
Height Offset: 1.5 units above player head
Look-at Offset: 0.5 units above player center (focus point)

Distance Range:
  Minimum: 2.0 units (closest zoom)
  Maximum: 8.0 units (furthest zoom)
  Default: 4.0 units
  
Zoom Sensitivity: 0.5 units per scroll tick
Zoom Smoothing: 0.1 seconds interpolation time
```

#### Controls
```yaml
Mouse X (Horizontal): 
  Rotation around player
  Sensitivity: 2.0 degrees per mouse unit
  Smoothing: 0.05 seconds
  
Mouse Y (Vertical):
  Pitch adjustment
  Sensitivity: 1.5 degrees per mouse unit
  Smoothing: 0.05 seconds
  
Scroll Wheel:
  Zoom in/out
  Speed: 0.5 units per tick
  Limits: 2.0 to 8.0 units
  
Middle Click:
  Snap camera to behind player
  Animation: 0.3 second smooth transition
```

#### Constraints
```yaml
Pitch Limits:
  Minimum (Looking Up): -60 degrees from horizontal
  Maximum (Looking Down): +30 degrees from horizontal
  
Rotation Limits:
  Full 360 degrees horizontal rotation
  No artificial limits on yaw
  
Collision Avoidance:
  Raycast from player to camera position
  If collision detected: Camera moves to collision point - 0.3m buffer
  Smooth return: Camera returns to preferred distance when path clears
  
Occlusion Handling:
  When camera would be inside object: Fade object to 30% opacity
  Exception: Terrain never fades (camera moves instead)
  Fade Speed: 0.2 seconds in, 0.5 seconds out
```

#### Camera Modes
```yaml
Exploration Mode (Default):
  Distance: 5.0-8.0 units
  Field of View: 70 degrees
  Use Case: General travel, exploration
  
Combat/Work Mode:
  Distance: 3.0-5.0 units
  Field of View: 65 degrees
  Activation: When weapon/tool equipped or combat detected
  Use Case: Precision work, combat, building
  
Photo Mode (B Key):
  Distance: 2.0-20.0 units (free)
  Field of View: 40-120 degrees adjustable
  Constraints: None (can clip through walls)
  UI: Hidden (clean screenshot mode)
  Time: Pauses single-player, continues in multiplayer
```

### 2.2 First-Person Camera (Optional)

#### Configuration
```yaml
Position: Player head height (1.7 units above ground)
Offset: 0.1 units forward from head center
Field of View: 90 degrees horizontal
Aspect Ratio: Adjustable (default 16:9)

Head Bob:
  Enabled: Toggle in settings
  Walk Amplitude: 0.05 units vertical
  Sprint Amplitude: 0.08 units vertical
  Frequency: 2 cycles per second at walk speed
```

#### Visibility Rules
```yaml
Player Body:
  Visible: Hands, arms, tools/weapons
  Invisible: Head, torso, legs (clipping prevention)
  
Tool/Weapon Models:
  Always visible in lower-right corner
  Animation: Full first-person animation set
  
Shadow:
  Player still casts shadow (calculated from third-person position)
```

### 2.3 Camera Transitions

#### Mode Switching
```yaml
Third-Person to First-Person:
  Transition Time: 0.3 seconds
  Animation: Camera moves smoothly into head position
  Field of View: Animates from 70° to 90°
  
First-Person to Third-Person:
  Transition Time: 0.3 seconds
  Animation: Camera pulls back to default distance
  Field of View: Animates from 90° to 70°
```

#### Dynamic Adjustments
```yaml
Velocity-Based FOV:
  Base FOV: 70 degrees (third-person), 90 degrees (first-person)
  Sprint Bonus: +5 degrees FOV at full sprint speed
  Smoothing: 0.5 seconds to reach new FOV
  
Damage Shake:
  Amplitude: 0.2 units
  Duration: 0.3 seconds
  Frequency: 15 Hz
  Direction: Random within 10 degree cone
```

---

## 3. Interaction System

### 3.1 Interaction Range & Detection

#### Detection Parameters
```yaml
Base Interaction Range: 2.0 meters
Extended Range (Tools): 3.0 meters
Angle: 60 degree cone in front of player
Vertical Angle: 45 degrees (up and down)

Raycast Configuration:
  Origin: Camera center (crosshair position)
  Length: 50.0 meters max (performance limit)
  Layers: Objects, Resources, AI Agents, Buildings
  
Sphere Cast (Backup):
  Used when raycast misses but player is near interactables
  Radius: 1.5 meters around player
  Priority: Closest to camera center
```

#### Target Highlighting
```yaml
Default State:
  No outline, normal rendering
  
Hovered State (within range, looking at):
  Outline: 2 pixel width
  Color: Cyan (#00FFFF)
  Opacity: 80%
  Animation: 0.1 second fade in
  
In-Range State (nearby but not looking directly):
  Outline: 1 pixel width
  Color: White (#FFFFFF)
  Opacity: 40%
  
Out-of-Range State:
  Outline: None
  Crosshair: Red X when targeting but too far
```

#### Crosshair States
```yaml
Default:
  Type: Small dot (4 pixels)
  Color: White (#FFFFFF)
  
Interactable Available:
  Type: Hand icon
  Color: Green (#00FF00)
  Animation: Gentle pulse (1 second cycle)
  
Interactable in Range:
  Type: Hand icon with range indicator
  Color: Cyan (#00FFFF)
  
Tool/Weapon Active:
  Type: Target reticle
  Color: Orange (#FFA500)
  
Cannot Interact:
  Type: Red circle with slash
  Color: Red (#FF0000)
```

### 3.2 Interaction Priority System

#### Priority Hierarchy
```yaml
Priority 1 - Equipped Tool/Weapon:
  Trigger: Left Mouse Button (Hold)
  Override: Always takes precedence
  Examples: Attacking, harvesting, building placement

Priority 2 - Containers:
  Trigger: E Key (Press)
  Examples: Chests, storage boxes, crates
  Range: Standard 2.0m

Priority 3 - Crafting Stations:
  Trigger: E Key (Press)
  Examples: Workbench, furnace, anvil
  Range: Standard 2.0m

Priority 4 - NPCs/AI Agents:
  Trigger: F Key (Press) or E Key (Alternative)
  Examples: Trading, dialogue, hiring
  Range: 1.5-3.0m (social distance)

Priority 5 - Resources:
  Trigger: Left Mouse Button (Hold) with tool equipped
  Examples: Trees, rocks, ore nodes
  Range: Tool reach (2.0-3.0m)

Priority 6 - Doors/Openables:
  Trigger: E Key (Press)
  Examples: Doors, gates, windows
  Range: Standard 2.0m

Priority 7 - Furniture:
  Trigger: E Key (Press) or F Key (Alternative)
  Examples: Chairs (sit), beds (sleep), tables
  Range: Standard 2.0m

Priority 8 - World Objects:
  Trigger: E Key (Press)
  Examples: Signs, readable objects, switches
  Range: Standard 2.0m
```

#### Context Sensitivity
```yaml
Auto-Detect:
  System determines appropriate action based on:
    - Currently equipped item
    - Target object type
    - Player state (sitting, standing, etc.)
    - Range to target
    
Examples:
  Holding Axe + Looking at Tree = Harvest
  Holding Building Plan + Right Click = Placement Mode
  Empty Hands + Looking at NPC = Talk
  Empty Hands + Looking at Chair = Sit
```

### 3.3 Interaction Types by Target

#### 3.3.1 Resources (Trees, Rocks, Ore)

```yaml
Input: Hold Left Mouse Button
Duration: Continuous (until resource depleted or player stops)
Tool Required: Yes (appropriate type)

Gathering Mechanics:
  Resource Health: Variable by type and size
    Small Tree: 50 HP
    Medium Tree: 100 HP
    Large Tree: 200 HP
    Small Rock: 30 HP
    Large Rock: 80 HP
    Ore Node: 150 HP
    
  Damage Per Hit:
    Base: Tool damage value
    Skill Bonus: +5% per skill level
    Tool Quality: × efficiency multiplier
    
  Hit Timing:
    Animation: 1.0 second swing cycle
    Active Hit Window: 0.3 seconds (middle of swing)
    Stamina Cost: 2.0 units per swing
    
  Yield Calculation:
    Base Yield: Defined per resource type
    Skill Bonus: +10% per skill level (rounded down)
    Tool Bonus: +20% with steel tools
    Depletion Bonus: +50% on final hit

Feedback Systems:
  Visual:
    - Tool swing animation (synced to hit window)
    - Resource shake on hit
    - Resource health bar (appears on first hit)
    - Material chips/debris on impact
    
  Audio:
    - Tool swing sound (start of animation)
    - Impact sound (wood, stone, metal variations)
    - Depletion sound (resource destroyed)
    
  Particles:
    - Impact dust/sparks
    - Wood chips for trees
    - Stone fragments for rocks
    - Glow particles for ore
    
  UI:
    - Progress bar (resource HP / max HP)
    - Yield preview (estimated resources)
    - Stamina drain indicator
```

#### 3.3.2 Containers (Chests, Storage)

```yaml
Input: Press E (single press)
Duration: Instant open
Animation: 0.2 second lid/door opening

Container Types:
  Small Chest:
    Slots: 15
    Volume: 0.5m³
    
  Standard Chest:
    Slots: 30
    Volume: 1.0m³
    
  Large Chest:
    Slots: 60
    Volume: 2.0m³
    
  Warehouse Container:
    Slots: 300
    Volume: 20.0m³

Inventory Interface:
  Layout:
    Left Panel: Player inventory (8×8 grid)
    Right Panel: Container inventory (variable grid)
    
  Transfer Methods:
    - Drag and drop (single item)
    - Shift+Click (move stack)
    - Double Click (move single item)
    - "Take All" button (container to player)
    - "Store All" button (player to container)
    
  Auto-Features:
    - Auto-stack: Matching items combine automatically
    - Quick sort: Button to sort by type
    - Search: Filter items by name
    - Category tabs: Resources, Tools, Food, Misc

Special Behaviors:
  Locked Containers:
    - Require key or lockpick skill
    - Visual: Padlock icon
    
  Trapped Containers:
    - Damage on open (5-20 HP)
    - Disarm with trap detection skill
```

#### 3.3.3 Crafting Stations

```yaml
Input: Press E (single press)
Duration: Instant UI open
Animation: 0.3 second station activation

Station Types:
  Workbench:
    Recipes: Basic tools, simple items
    Craft Time Multiplier: 1.0× (baseline)
    
  Furnace:
    Recipes: Smelting ore, cooking food
    Fuel Required: Yes (wood, coal)
    Batch Size: Up to 10 items
    
  Anvil:
    Recipes: Metal tools, weapons
    Craft Time Multiplier: 0.8× (faster)
    Requires: Hammer in inventory
    
  Advanced Workbench:
    Recipes: Complex mechanical items
    Craft Time Multiplier: 0.7×
    Power Required: Yes (electricity in late game)

Crafting Interface:
  Layout:
    Left Panel: Recipe list (filterable, searchable)
    Center Panel: Recipe details
    Right Panel: Player inventory
    
  Recipe Display:
    - Item name and icon
    - Material requirements (with current inventory count)
    - Output quantity
    - Craft time
    - Success chance (if applicable)
    - Quality output range
    
  Crafting Controls:
    - Craft 1×: Single item
    - Craft 5×: Five items (if materials available)
    - Craft 10×: Ten items
    - Craft Max: Maximum possible with current materials
    - Continuous Craft: Hold button for repeated crafting

Crafting Process:
  Real-Time Crafting:
    - Progress bar fills over time
    - Can be interrupted (lose 50% of materials)
    - Station animation during craft
    - Audio: Crafting-specific sounds
    
  Instant Crafting:
    - For simple items (< 5 seconds)
    - No progress bar, immediate completion
    
  Batch Crafting:
    - Queue up to 20 items
    - Process sequentially
    - Can cancel remaining queue
```

#### 3.3.4 NPCs/AI Agents

```yaml
Input: Press F (primary) or E (alternative)
Duration: Opens dialogue UI
Animation: NPC turns to face player, gesture

Interaction Range:
  Too Close: < 1.0 meter (NPC backs away uncomfortably)
  Comfortable: 1.5-3.0 meters (optimal interaction)
  Maximum: 4.0 meters (beyond this, interaction disabled)
  
  Visual Feedback:
    - Too close: NPC steps back animation
    - Too far: "Move closer" tooltip
    - Optimal: NPC faces player, engages

Dialogue Options:
  Greeting:
    - Context-aware (time of day, recent events)
    - Personality-affected (friendly, neutral, hostile)
    
  Trade:
    - Available if NPC is merchant or has items
    - Opens trade interface
    - Prices affected by reputation
    
  Talk:
    - General conversation
    - Lore, rumors, news
    - Relationship building
    
  Request Service:
    - Crafting (if NPC has skill)
    - Gathering (if NPC is available)
    - Guide/Tour (if NPC knows area)
    
  Hire:
    - Available if NPC is unemployed
    - Negotiate wage (daily rate)
    - Assign job type
    - Set working hours
    
  Gossip:
    - Learn about other NPCs
    - Discover secrets
    - Affects relationships

Social Mechanics:
  Reputation Impact:
    - Successful interaction: +1 to +5 reputation
    - Failed interaction: -1 to -3 reputation
    - Ignored greeting: -1 reputation
    
  Time Limits:
    - NPCs have limited patience
    - 3-5 dialogue exchanges before NPC wants to move on
    - High social skill extends limit
```

#### 3.3.5 Building/Construction

```yaml
Input: Right Click (with building tool equipped)
Toggle Mode: B Key (enter/exit build mode)

Build Mode Activation:
  1. Equip building tool or hammer
  2. Press B or Right Click to enter build mode
  3. Select building piece from radial menu
  4. Ghost preview appears

Placement Mechanics:
  Ghost Preview:
    Valid Position:
      - Color: Transparent blue
      - Opacity: 50%
      - Animation: Gentle pulse
      
    Invalid Position:
      - Color: Transparent red
      - Opacity: 50%
      - Reason displayed: "No ground support", "Collision detected", etc.
      
  Grid Snapping:
    Default: 0.5 meter increments
    Fine Mode: 0.1 meter increments (Hold Alt)
    Free Mode: No snapping (Hold Shift)
    
  Rotation:
    Mouse Scroll: 15 degree increments
    Q/E Keys: 90 degree snap rotation
    R Key: Reset to default rotation
    
  Elevation:
    Up/Down Arrows: Raise/lower by 0.5m
    Page Up/Down: Raise/lower by 2.0m
    
Placement Constraints:
  Valid Ground:
    - Must be on walkable terrain
    - Max slope: 30 degrees for buildings
    - Min clearance: 0.5m from other objects
    
  Line of Sight:
    - Player must see placement location
    - Cannot place through walls (unless in free camera)
    
  Collision:
    - Cannot intersect existing buildings
    - Cannot intersect terrain (except foundations)
    - Must respect claim boundaries
    
  Materials:
    - Required materials displayed in UI
    - Place button disabled if insufficient
    - Material types affect building durability

Building Process:
  Step 1: Position ghost (valid = blue, invalid = red)
  Step 2: Rotate as needed
  Step 3: Press Left Click to place
  Step 4: Animation: Character builds (3-30 seconds based on size)
  Step 5: Building appears, materials consumed

Post-Placement:
  Edit Mode:
    - Hold E on building to enter edit
    - Move, rotate, or delete
    - 50% material refund on delete
    
  Repair:
    - Damaged buildings show health bar
    - Hold E + select repair
    - Costs 25% of original materials
```

---

## 4. Controls Mapping

### 4.1 Default Keyboard Layout

#### Movement Keys
```yaml
W: Move forward
S: Move backward
A: Move left (strafe)
D: Move right (strafe)

Space: Jump
  Tap: Standard jump
  Hold: Prepare for higher jump (if mechanic available)

Shift (Hold): Sprint
  Double-Tap: Toggle sprint mode (accessibility option)
  
Ctrl (Hold): Crouch
  Toggle: Ctrl (accessibility option)
  
C: Toggle crouch (alternative to Ctrl)
```

#### Mouse Controls
```yaml
Mouse Movement: Look/camera control
  Sensitivity: Adjustable (default 1.0)
  Invert Y: Toggle in settings
  Acceleration: Off by default

Left Mouse Button:
  Primary Action: Use tool / Attack / Harvest
  Hold: Continuous action (mining, attacking)
  Build Mode: Place building piece

Right Mouse Button:
  Secondary Action: Alternate tool use / Block
  Hold: Enter build mode (with building tool)
  Build Mode: Cancel/exit build mode

Scroll Wheel:
  Camera: Zoom in/out (third-person)
  Build Mode: Rotate building piece
  Inventory: Scroll through items

Middle Click:
  Camera: Snap to behind player
  Hold: Free look (camera orbits while player faces forward)
```

#### Action Keys
```yaml
E: Primary Interact
  Context sensitive based on target
  Hold for extended interactions (long press = different action)

F: Secondary Interact / Talk
  Dedicated NPC interaction
  Alternative interact for some objects

Q: Drop Item
  Drops held item or selected hotbar item
  Hold: Drop stack

R: Reload (if applicable)
  Also: Rotate building counter-clockwise in build mode

T: Tool radial menu (quick tool switch)
  Hold: Slow time, select with mouse

G: Gesture/Emote menu
  Hold: Radial emote selector
  Quick tap: Last used emote

V: Voice chat (push to talk)
  Hold: Transmit voice
  Settings: Toggle voice activation
```

#### Interface Keys
```yaml
Tab: Inventory
  Toggle full inventory screen
  
C: Character sheet
  Shows stats, skills, equipment
  
M: Map
  Toggle world map
  Scroll: Zoom map
  Right drag: Pan map

J: Journal/Quest Log
  Current objectives
  Completed quests
  Notes

L: Society/Law Interface
  Laws, elections, governance

Enter: Chat
  Open chat input
  / commands for slash commands

Esc: Menu / Cancel
  Open game menu
  Close current UI
  Cancel current action
```

#### Hotbar
```yaml
Number Keys 1-9: Hotbar slots
  Instant select
  
0: Hotbar slot 10
  Alternative: - key

-: Hotbar slot 11 (if available)

=: Hotbar slot 12 (if available)

Mouse Wheel: Scroll through hotbar
  Wraps around from 9 to 1
  
Shift + Number: Move selected inventory item to hotbar slot
```

#### Modifier Combinations
```yaml
Shift + Click (Inventory): Move entire stack
Ctrl + Click (Inventory): Move single item from stack
Alt + Click (Inventory): Drop item

Shift + E (Building): Repair mode
Ctrl + E (Building): Delete/Recycle mode

Tab + Number: Quick loadout switch (if multiple sets configured)
```

### 4.2 Controller Mapping (Xbox/PlayStation)

#### Movement & Camera
```yaml
Left Stick: Move
  Press: Toggle sprint (or hold to sprint based on settings)
  
Right Stick: Look/Camera
  Sensitivity: Adjustable (default 2.0)
  Press: Snap camera behind player
```

#### Face Buttons
```yaml
A / X (Xbox/PS): Jump
  Hold in water: Dive/Swim up

B / Circle: Crouch
  Hold: Prone (if implemented)
  
X / Square: Primary Interact (E key equivalent)
  Hold: Context menu for complex interactions
  
Y / Triangle: Inventory
  Hold: Quick select radial menu
```

#### Triggers & Bumpers
```yaml
Left Trigger (LT / L2): Use Tool (Hold for continuous)
  Aim mode for ranged weapons
  
Right Trigger (RT / R2): Secondary Action / Attack
  Heavy attack for melee
  
Left Bumper (LB / L1): Previous hotbar slot
  Hold: Hotbar radial menu
  
Right Bumper (RB / R1): Next hotbar slot
  Hold: Emote radial menu
```

#### D-Pad
```yaml
Up: Map
  Hold: Journal/Quest log
  
Down: Crafting menu
  Hold: Building menu
  
Left: Previous quick slot / Tool category
  
Right: Next quick slot / Tool category
```

#### Menu & System
```yaml
Start / Options: Game menu
  
Back / Touchpad: Scoreboard/Player list
  Press: Voice chat (if touchpad available)
  
Left Stick Click + Right Stick Click: Screenshot mode
```

### 4.3 Control Customization

#### Rebinding System
```yaml
Available Actions: All 50+ game actions
Rebinding Method: Press key to assign
Conflict Detection: Automatic warning for duplicate binds
Profile System: Up to 3 custom profiles
Reset: One-click reset to defaults

Categories:
  - Movement (8 actions)
  - Combat/Tools (12 actions)
  - Interaction (10 actions)
  - Interface (15 actions)
  - Communication (6 actions)
```

---

## 5. Accessibility Options

### 5.1 Movement Accessibility

```yaml
Toggle Sprint:
  Option: Press once to toggle, not hold
  Visual: Sprint indicator in HUD
  
Sticky Keys:
  Modifier keys (Shift, Ctrl) toggle instead of hold
  Visual indicator when active
  
One-Handed Mode:
  Preset: Movement on mouse, actions on keyboard
  Mouse side buttons: Forward/Back = Strafe
  
Reduced Mobility Mode:
  Auto-walk: Double-tap W to continue walking
  Auto-sprint: Option to always sprint when stamina available
```

### 5.2 Visual Accessibility

```yaml
Adjustable Interaction Range:
  Minimum: 1.0 meter
  Maximum: 3.0 meters
  Default: 2.0 meters
  
High Contrast Mode:
  Outlines on all interactables
  Increased UI contrast
  
Colorblind Modes:
  Deuteranopia (Green-weak)
  Protanopia (Red-weak)
  Tritanopia (Blue-weak)
  Achromatopsia (Monochrome)
  
Outline Thickness:
  Range: 1-5 pixels
  Affects all interactable highlighting
  
Camera Shake Toggle:
  Option to disable all camera shake
  Separate toggles for damage, explosions, sprint
  
Field of View:
  Range: 60-120 degrees
  Affects motion sensitivity
  
Motion Sickness Options:
  - Reduce head bob
  - Static camera option (no FOV change)
  - Vignette reduction
  - Center dot for focus
```

### 5.3 Audio Accessibility

```yaml
Subtitles:
  Enable: All dialogue and important audio
  Size: Small/Medium/Large
  Background: None/Semi-transparent/Opaque
  Speaker Labels: On/Off
  
Visual Audio Cues:
  Directional indicators for sounds
  Footstep visualization option
  
Audio Mixing:
  Master: 0-100%
  Music: 0-100%
  SFX: 0-100%
  Voice: 0-100%
  Ambient: 0-100%
  UI: 0-100%
  
Text-to-Speech:
  Chat messages read aloud
  UI element descriptions
  Speed: Adjustable
```

### 5.4 Cognitive Accessibility

```yaml
Simplified UI Mode:
  Reduced HUD elements
  Larger text
  Fewer simultaneous notifications
  
Extended Timers:
  Interaction time limits increased 50%
  Dialogue choices stay open longer
  
Tutorial Reminders:
  Contextual tips repeat periodically
  Optional hint system
  
Reduced Complexity Mode:
  Simplified crafting requirements
  Auto-sort inventory
  Tooltips always visible
```

---

## 6. Technical Implementation Notes

### 6.1 Network Synchronization

#### Movement Networking
```yaml
Client Prediction:
  - Client simulates movement locally
  - Sends inputs to server every tick (50ms)
  - Server validates and corrects if needed
  
Server Reconciliation:
  - Server processes inputs with 50ms tick
  - Sends position updates to all clients
  - Position sent: Every tick (20 TPS)
  
Interpolation:
  - Remote players: Interpolated between positions
  - Interpolation delay: 100ms (2 ticks)
  - Smooths network jitter
  
Bandwidth Optimization:
  - Delta compression: Only send changed values
  - Position threshold: 0.01 units minimum change
  - Rotation threshold: 1 degree minimum change
```

#### Interaction Networking
```yaml
Interaction Events:
  - Reliable RPC (guaranteed delivery)
  - Timestamped for ordering
  - Server authoritative validation
  
State Synchronization:
  - Container contents: Sync on open
  - Crafting progress: Sync every 500ms
  - NPC dialogue: Server-driven state machine
  
Rollback Protection:
  - Actions validated against server state
  - Invalid actions rejected with notification
  - Desync detection and correction
```

### 6.2 Performance Optimization

#### Interaction Detection
```yaml
Raycast Budget:
  - Max distance: 50 meters
  - Update frequency: Every frame for local player
  - Every 5 frames for optimization mode
  
LOD for Interactables:
  - 0-10m: Full detail, interaction enabled
  - 10-20m: Medium detail, interaction enabled
  - 20-50m: Low detail, raycast only
  - 50m+: No interaction detection
  
Occlusion Culling:
  - Interactables behind walls: Not processed
  - Update: Every 500ms for occluded objects
```

#### Camera Optimization
```yaml
Collision Checks:
  - Frequency: Every frame for active camera
  - Max distance: 10 units (camera max distance)
  - Layers: Buildings, Terrain only
  
Smoothing:
  - Position: Lerp with 0.1s interpolation
  - Rotation: Slerp with 0.05s interpolation
  - Reduces jitter, feels responsive
```

### 6.3 Physics Performance

```yaml
Player Physics:
  - Tick rate: 20 TPS (matches game tick)
  - Sub-stepping: 4× for smooth movement
  - Continuous collision: Enabled for player only
  
Collision Layers Optimization:
  - Static bodies: Sleep when not moving
  - Dynamic bodies: Update only when awake
  - Broad phase: Spatial hashing for efficient queries
  
Stamina Calculation:
  - Batch processed with player tick
  - No individual allocations per frame
  - Reuse calculation buffers
```

### 6.4 Input System

```yaml
Input Processing:
  - Polling rate: Every frame
  - Buffering: 3 frame input buffer
  - Dead zones: Configurable (default 10%)
  
Action Mapping:
  - Dictionary lookup: O(1) for all actions
  - Rebinding: Hot-swappable at runtime
  - Profile system: JSON-based storage
  
Platform Differences:
  - PC: Keyboard + Mouse (primary)
  - PC: Controller support (secondary)
  - Future: Console adaptations
```

---

## 7. Edge Cases & Error Handling

### 7.1 Movement Edge Cases

```yaml
Stuck Detection:
  - Monitor position changes over 3 seconds
  - If < 0.1m movement while input pressed: Trigger unstuck
  - Unstuck method: Slight teleport upward, backward push
  
Falling Through World:
  - Y position check: If < -100 units, respawn at last safe position
  - Safe positions: Recorded every 5 seconds
  - Recovery: Fade to black, teleport, fade in
  
Slope Sliding:
  - If on > 45° slope with no input: Auto-slide
  - Slide control: Minimal (10% influence)
  - Slide exit: Auto-trigger jump if space pressed
```

### 7.2 Interaction Edge Cases

```yaml
Rapid Interaction Spam:
  - Cooldown: 0.2 seconds between distinct interactions
  - Queue: Max 3 interactions queued
  - Overflow: Ignore additional inputs
  
Concurrent Interactions:
  - Priority system resolves conflicts
  - Cannot: Open two containers simultaneously
  - Cannot: Craft while in dialogue
  
Range Violations:
  - Validation: Server checks distance
  - Client-side: Grace period of 0.5 seconds
  - Failure: Interaction cancelled, materials refunded
  
Inventory Full:
  - Container transfer: Blocked with message
  - Resource gathering: Drops items on ground
  - Ground item timeout: 5 minutes before despawn
```

### 7.3 Camera Edge Cases

```yaml
Wall Clipping:
  - Camera collision: Prevents entering walls
  - Emergency: If clipped, snap to valid position
  - Visual: Brief black screen during emergency snap
  
Extreme Zoom:
  - Minimum: 2.0 units (inside player = invalid)
  - Enforcement: Hard limits, no override
  - Photo mode: Only mode allowing < 2.0 units
  
Camera Stutter:
  - Detection: Position variance over 10 frames
  - Smoothing: Increase interpolation time temporarily
  - Root cause: Usually network jitter (not local)
```

---

## 8. Testing Checklist

### 8.1 Movement Testing

```yaml
Basic Movement:
  [ ] Walk in all directions
  [ ] Sprint activates with sufficient stamina
  [ ] Sprint blocked below 20 stamina
  [ ] Crouch reduces height
  [ ] Jump reaches expected height
  [ ] Jump maintains forward momentum
  [ ] Stamina regenerates correctly
  [ ] Exhaustion state triggers appropriately

Advanced Movement:
  [ ] Slope walking at 45°
  [ ] Sliding on steep slopes
  [ ] Step up at 0.5m height
  [ ] Jump required for > 0.5m obstacles
  [ ] Swimming mechanics
  [ ] Fall damage calculation
  [ ] Carrying weight affects speed
```

### 8.2 Camera Testing

```yaml
Third-Person:
  [ ] Mouse rotation smooth
  [ ] Zoom in/out functional
  [ ] Collision avoidance works
  [ ] Occlusion fading works
  [ ] Snap-to-behind functional
  [ ] Mode transitions smooth

First-Person:
  [ ] FOV correct (90°)
  [ ] Body parts hidden appropriately
  [ ] Tool models visible
  [ ] Head bob toggle works
  [ ] Shadow still cast
```

### 8.3 Interaction Testing

```yaml
Detection:
  [ ] Raycast detection at 2m
  [ ] Highlight appears on valid targets
  [ ] Priority system works
  [ ] Crosshair state changes correctly

Resource Gathering:
  [ ] Tool swing animation
  [ ] Hit registration timing
  [ ] Resource depletion
  [ ] Yield calculation
  [ ] Stamina drain

Containers:
  [ ] Open/close functional
  [ ] Item transfer works
  [ ] Auto-stack works
  [ ] Capacity limits enforced

NPCs:
  [ ] Dialogue opens
  [ ] Social distance respected
  [ ] Trade interface functional
  [ ] Hire mechanics work

Building:
  [ ] Ghost preview appears
  [ ] Valid/invalid positioning
  [ ] Grid snapping
  [ ] Rotation
  [ ] Material consumption
  [ ] Building completion
```

---

## 9. Constants Reference

### 9.1 Values from Technical Constants

All values below reference `planning/meta/technical-constants.md`:

```yaml
Movement:
  WALK_SPEED: 3.0 m/s (MOVEMENT_SPEED_WALK)
  SPRINT_SPEED: 6.0 m/s (MOVEMENT_SPEED_SPRINT)
  CARRY_SPEED: 2.0 m/s (MOVEMENT_SPEED_CARRYING)
  SPRINT_STAMINA_COST: 2.0/s (SPRINT_STAMINA_COST_PER_SECOND)
  SPRINT_MIN_STAMINA: 20.0 (SPRINT_MIN_STAMINA_TO_START)

Stamina:
  MAX: 100.0 (STAMINA_MAX)
  REGEN_REST: 30.0/hour (STAMINA_REGEN_REST_PER_HOUR)
  REGEN_WALK: 10.0/hour (STAMINA_REGEN_WALK_PER_HOUR)

Inventory:
  BASE_WEIGHT: 50.0 kg (INVENTORY_WEIGHT_PLAYER_BASE_KG)
  MAX_WEIGHT: 100.0 kg (INVENTORY_WEIGHT_MAX_KG)
  SLOTS: 64 (INVENTORY_SLOTS_PLAYER)

Skills:
  MAX_LEVEL: 10 (SKILL_LEVEL_MAX)
  BONUS_PER_LEVEL: 5% (SKILL_BONUS_PER_LEVEL_PERCENT)

Network:
  TICK_RATE: 20 TPS (TICK_RATE)
  TICK_INTERVAL: 50ms (TICK_INTERVAL_MS)
```

### 9.2 Document-Specific Constants

```yaml
Movement:
  ACCELERATION: 8.0 units/s²
  DECELERATION: 10.0 units/s²
  TURN_RATE_WALK: 360°/s
  TURN_RATE_SPRINT: 270°/s
  JUMP_HEIGHT: 1.2 units
  JUMP_COOLDOWN: 0.5s
  COYOTE_TIME: 0.15s
  PRE_JUMP_BUFFER: 0.1s
  GRAVITY: 9.8 units/s²
  TERMINAL_VELOCITY: 30 m/s
  
Collision:
  PLAYER_HEIGHT: 1.8 units
  PLAYER_RADIUS: 0.4 units
  CROUCH_HEIGHT: 1.08 units (60%)
  STEP_HEIGHT: 0.5 units
  MAX_SLOPE: 45°
  MASS: 70 kg

Camera:
  DEFAULT_DISTANCE: 4.0 units
  MIN_DISTANCE: 2.0 units
  MAX_DISTANCE: 8.0 units
  HEIGHT_OFFSET: 1.5 units
  PITCH_MIN: -60°
  PITCH_MAX: +30°
  FOV_THIRD_PERSON: 70°
  FOV_FIRST_PERSON: 90°

Interaction:
  BASE_RANGE: 2.0 meters
  TOOL_RANGE: 3.0 meters
  ANGLE: 60°
  NPC_MIN_DISTANCE: 1.0m
  NPC_MAX_DISTANCE: 4.0m
  
Controls:
  INPUT_BUFFER: 3 frames
  DEAD_ZONE: 10%
  INTERACTION_COOLDOWN: 0.2s
```

---

## 10. Integration Notes

### 10.1 Related Systems

```yaml
Dependencies:
  - Technical Constants (planning/meta/technical-constants.md)
  - Physics System (Session 3 - 01b-physics-system-spec.md)
  - Input System (Session 3 - 01d-input-handling-spec.md)
  - UI/UX System (Session 3 - 07-ui-ux-paths.md)
  
Impacts:
  - Agent AI (Session 2 - 01-core-ai-architecture.md)
  - Combat System (Session 3 - 01e-combat-system-spec.md)
  - Building System (Session 3 - 01f-building-system-spec.md)
  - Economy (Session 2 - 02-economic-behavior.md)
```

### 10.2 Implementation Order

```yaml
Priority 1 (Core):
  1. Basic movement (walk, sprint, jump)
  2. Third-person camera
  3. Basic interaction (E key)
  4. Stamina system
  
Priority 2 (Essential):
  5. Crouching
  6. Resource gathering
  7. Container interaction
  8. All control mappings
  
Priority 3 (Polish):
  9. First-person camera
  10. Advanced building
  11. Full accessibility options
  12. Controller support
```

---

## Document History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-02-01 | 1.0 | Initial specification complete | Session 3 |

---

**END OF DOCUMENT**

*All numerical values must reference `planning/meta/technical-constants.md`. Discrepancies should be flagged for resolution.*
