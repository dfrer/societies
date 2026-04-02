# Inventory System Specification

**Session**: 3 - Core Gameplay Loops  
**Document**: 01e-inventory-system-spec.md  
**Status**: Draft  
**Last Updated**: 2026-02-01  
**References**: [Technical Constants](../meta/technical-constants.md)

---

## Table of Contents

1. [Inventory Structure](#1-inventory-structure)
2. [Weight System](#2-weight-system)
3. [Inventory Operations](#3-inventory-operations)
4. [Inventory UI Design](#4-inventory-ui-design)
5. [Hotbar System](#5-hotbar-system)
6. [Special Inventory Types](#6-special-inventory-types)
7. [Item Properties](#7-item-properties)
8. [Storage Systems](#8-storage-systems)
9. [Item Database](#9-item-database)

---

## 1. Inventory Structure

### Player Inventory

**Base Configuration** (from Technical Constants):
```
Slot Count: 64 slots (8×8 grid)
  - Constant: INVENTORY_SLOTS_PLAYER = 64
  - Agent inventory: INVENTORY_SLOTS_AGENT = 64 (same size)

Weight Limit: 100 kg
  - Constant: INVENTORY_WEIGHT_MAX_KG = 100.0f
  - Overflow: Cannot pick up items beyond weight limit

Base Capacity: 50 kg
  - Constant: INVENTORY_WEIGHT_PLAYER_BASE_KG = 50.0f
  - Upgradable through skills/equipment
```

**Slot Properties**:
```
Slot Types:
  - Standard slots: 64 total
  - Each slot holds one item type
  - Stackable items: Multiple units per slot (up to max stack size)
  - Unique items: 1 per slot (tools, weapons, armor)

Stack Limits by Category:
  - Tools & Equipment: 1 unit/slot (non-stackable)
  - Weapons: 1 unit/slot
  - Armor: 1 unit/slot
  - Consumables: Varies by type
```

**Visual Layout**:
```
Grid display in inventory UI:
  - 8 columns × 8 rows = 64 slots
  - Item icons with quantity overlay
  - Weight bar showing current/max (100 kg)
  - Encumbrance indicator (color-coded)
  
Slot Visual Properties:
  - Size: 64×64 pixels per slot
  - Gap: 4 pixels between slots
  - Border: 2px highlight when selected
  - Quantity badge: Bottom-right corner
```

### Stack Sizes by Item Category

**Building Materials** (from Technical Constants):
```
Wood: 100 units/slot
  - Constant: STACK_SIZE_WOOD = 100
  - Weight: 0.5 kg/unit

Stone: 50 units/slot
  - Constant: STACK_SIZE_STONE = 50
  - Weight: 2.0 kg/unit

Clay: 50 units/slot
  - Derived from stone stack size pattern
  - Weight: 1.0 kg/unit

Bricks: 30 units/slot
  - Processed material, smaller stacks
  - Weight: 1.5 kg/unit

Planks: 50 units/slot
  - Processed wood
  - Weight: 0.4 kg/unit
```

**Raw Materials** (from Technical Constants):
```
Ore (all types): 50 units/slot
  - Constant: STACK_SIZE_ORE = 50
  - Iron ore: 2.0 kg/unit
  - Copper ore: 2.0 kg/unit
  - Gold ore: 3.0 kg/unit
  - Coal: 1.0 kg/unit

Ingots: 20 units/slot
  - Processed metal, compact
  - Iron ingot: 1.0 kg/unit
  - Gold ingot: 2.0 kg/unit
  - Copper ingot: 1.0 kg/unit

Gems: 100 units/slot
  - Small, lightweight
  - Weight: 0.05 kg/unit

Raw Materials (general): 100 units/slot
  - Constant: STACK_SIZE_MATERIALS = 100
  - Applies to crafting components
```

**Food** (from Technical Constants):
```
Raw meat: 10 units/slot
  - Perishable, limited stacking
  - Weight: 0.3 kg/unit

Cooked food: 10 units/slot
  - Meals and prepared food
  - Weight: 0.25 kg/unit

Crops: 20 units/slot
  - Vegetables, fruits, grains
  - Weight: 0.1 kg/unit

Seeds: 100 units/slot
  - Tiny, stackable
  - Weight: 0.01 kg/unit

Food (general): 20 units/slot
  - Constant: STACK_SIZE_FOOD = 20
```

**Tools & Equipment**:
```
All tools: 1 unit/slot (unique)
  - Constant: STACK_SIZE_TOOLS = 1
  - Stone tool: 2.0 kg
  - Iron tool: 3.0 kg
  - Steel tool: 3.0 kg

Weapons: 1 unit/slot
  - Swords, bows, etc.
  - Weight: 2.0-5.0 kg depending on type

Armor: 1 unit/slot
  - Helmets, chest plates, etc.
  - Weight: 1.0-8.0 kg per piece

Accessories: 1 unit/slot
  - Rings, amulets, belts
  - Weight: 0.1-0.5 kg
```

**Miscellaneous**:
```
Currency (Credits): 10,000 units/slot
  - Lightweight, highly stackable
  - Weight: 0.001 kg/unit (effectively weightless)

Documents: 20 units/slot
  - Papers, maps, contracts
  - Weight: 0.01 kg/unit

Potions: 10 units/slot
  - Consumable effects
  - Weight: 0.2 kg/unit

Miscellaneous items: Varies
  - Quest items: 1 unit/slot
  - Keys: 1 unit/slot
  - Collectibles: 1-10 units/slot
```

---

## 2. Weight System

### Item Weights (kg)

**Building Materials**:
```
Wood: 0.5 kg/unit
  - Light, bulky material
  - 100 units = 50 kg (half inventory capacity)

Stone: 2.0 kg/unit
  - Heavy, dense material
  - 50 units = 100 kg (full inventory capacity)

Clay: 1.0 kg/unit
  - Medium weight
  - 50 units = 50 kg

Bricks: 1.5 kg/unit
  - Processed stone/clay
  - 30 units = 45 kg

Planks: 0.4 kg/unit
  - Processed wood
  - 50 units = 20 kg
```

**Raw Materials**:
```
Iron ingot: 1.0 kg/unit
  - Common metal
  - 20 units = 20 kg

Gold ingot: 2.0 kg/unit
  - Dense precious metal
  - 20 units = 40 kg

Copper ingot: 1.0 kg/unit
  - Similar to iron
  - 20 units = 20 kg

Ore (iron): 2.0 kg/unit
  - Unprocessed, includes waste rock
  - 50 units = 100 kg

Ore (gold): 3.0 kg/unit
  - Denser ore
  - 33 units = 99 kg (practical limit)

Coal: 1.0 kg/unit
  - Fuel material
  - 50 units = 50 kg
```

**Food**:
```
Meat (raw): 0.3 kg/unit
  - Animal flesh
  - 10 units = 3 kg

Meat (cooked): 0.25 kg/unit
  - Prepared meals
  - 10 units = 2.5 kg

Bread: 0.2 kg/unit
  - Baked goods
  - 20 units = 4 kg

Vegetables: 0.1 kg/unit
  - Plants and crops
  - 20 units = 2 kg

Fruits: 0.15 kg/unit
  - Foraged food
  - 20 units = 3 kg
```

**Tools & Equipment**:
```
Stone tool: 2.0 kg
  - Primitive tool
  - Single item, heavy

Iron tool: 3.0 kg
  - Standard tool
  - Single item

Steel tool: 3.0 kg
  - Advanced tool
  - Same weight as iron, better durability

Sword (iron): 3.0 kg
  - One-handed weapon

Sword (steel): 3.5 kg
  - Higher quality weapon

Bow: 1.5 kg
  - Ranged weapon

Arrow: 0.05 kg
  - Ammunition
  - Stacks of 50
```

**Armor Weights**:
```
Cloth armor: 1.0 kg
  - Light protection
  - Full set: 4.0 kg

Leather armor: 2.0 kg
  - Medium-light protection
  - Full set: 8.0 kg

Iron armor: 5.0 kg
  - Heavy protection
  - Full set: 20.0 kg

Steel armor: 6.0 kg
  - Maximum protection
  - Full set: 24.0 kg
```

### Player Weight Calculations

```
Player Base Weight: 70 kg
  - Character model weight (clothes + body)
  - Does not count toward encumbrance

Inventory Capacity: 100 kg
  - Constant: INVENTORY_WEIGHT_MAX_KG = 100.0f
  - Additional weight player can carry

Total Maximum: 170 kg
  - Base 70 kg + Inventory 100 kg
  - Movement based on inventory weight ratio
```

### Encumbrance Effects

**Weight Ratio Calculation**:
```
Weight Ratio = CurrentInventoryWeight / MaxInventoryWeight

Example:
  - Current: 45 kg
  - Max: 100 kg
  - Ratio: 45 / 100 = 45% (Light encumbrance)
```

**Encumbrance Tiers**:

**Tier 1: Light (0-50% / 0-50 kg)**
```
Movement: 100% speed
  - Normal walking speed
  - Sprint available

Stamina: Normal drain
  - No additional stamina cost
  - Standard regeneration

UI Indicator: Green
  - Color: #00FF00
  - Weight bar: Full green
  - No visual warnings

Audio: None
  - Normal breathing sounds
  - No special effects
```

**Tier 2: Medium (51-75% / 51-75 kg)**
```
Movement: 90% speed (-10%)
  - Walking: 2.7 m/s (down from 3.0 m/s)
  - Sprint: 5.4 m/s (down from 6.0 m/s)

Stamina: +20% drain
  - Activities consume 20% more stamina
  - Regeneration: Normal

UI Indicator: Yellow
  - Color: #FFFF00
  - Weight bar: Yellow fill
  - Text: "Medium Load"

Audio: Slight breathing
  - Occasional heavier breath
  - Volume: Low
```

**Tier 3: Heavy (76-90% / 76-90 kg)**
```
Movement: 75% speed (-25%)
  - Walking: 2.25 m/s
  - Sprint: DISABLED
  - Cannot sprint while heavily encumbered

Stamina: +50% drain
  - Significantly faster stamina depletion
  - Regeneration: Reduced by 25%

UI Indicator: Orange
  - Color: #FFA500
  - Weight bar: Orange fill, pulsing
  - Text: "Heavy Load"
  - Warning icon displayed

Audio: Heavy breathing, grunt on jump
  - Constant heavier breathing loop
  - Grunt sound effect when jumping
  - Volume: Medium

Restrictions:
  - Sprint disabled
  - Jump height reduced by 20%
  - Cannot swim (will sink)
```

**Tier 4: Overburdened (91-100% / 91-100 kg)**
```
Movement: 50% speed (-50%)
  - Walking: 1.5 m/s
  - Sprint: DISABLED
  - Movement feels sluggish

Stamina: +100% drain
  - Stamina depletes twice as fast
  - Regeneration: Reduced by 50%

UI Indicator: Red, Flashing
  - Color: #FF0000
  - Weight bar: Red, pulsing rapidly
  - Text: "OVERBURDENED"
  - Warning icon with exclamation
  - Screen edge vignette (red)

Audio: Constant heavy breathing
  - Labored breathing loop
  - Occasional strain sounds
  - Volume: High

Health Penalty:
  - Over 95%: Gradual health loss
  - -0.5 HP per minute above 95 kg
  - Warning: "You are carrying too much!"

Restrictions:
  - Sprint disabled
  - Jump disabled completely
  - Cannot swim
  - Cannot use fast travel
```

**Tier 5: Immobilized (Over 100%)**
```
Movement: 0% speed
  - Cannot move
  - Character frozen in place
  - Must drop items to move

UI Indicator: Critical Red
  - Color: #8B0000 (dark red)
  - Full-screen warning
  - Text: "IMMOBILIZED - DROP ITEMS"
  - Flashing overlay

Audio: Strained breathing, failure sounds
  - Loud breathing
  - Character grunt on attempt to move

Required Action:
  - Must drop items to reduce weight below 100%
  - Can access inventory while immobilized
  - Drop items to ground
  - Cannot pick up new items
```

---

## 3. Inventory Operations

### Adding Items

**Acquisition Methods**:
```
1. Gathering
   - Trigger: Resource node depletion
   - Action: Automatic addition to inventory
   - Priority: Fill existing stacks first
   - Overflow: Drop at feet if no space

2. Looting
   - Trigger: Manual pickup (E key default)
   - Action: Raycast to item, press E
   - Animation: Pickup animation (0.5s)
   - Range: 2 meters

3. Trading
   - Source: Other player or AI agent
   - Action: Receive in trade window
   - Confirmation: Both parties accept
   - Immediate: Items transfer on accept

4. Crafting
   - Source: Crafting station output
   - Action: Click "Take" or auto-collect
   - Location: Appears in inventory or held
   - Materials: Consumed from inventory

5. Purchase
   - Source: Store, market, NPC vendor
   - Action: Buy with credits
   - Limit: Must have space and funds
   - Instant: Added immediately on purchase

6. Container Looting
   - Source: Chests, storage, drops
   - Action: Open container, transfer items
   - UI: Container view opens
   - Bulk: Can take all with one button
```

**Auto-Stacking Algorithm**:
```
When item added:
  1. Search for existing stack of same item type
  2. If found and has space:
     - Add to existing stack
     - Update quantity display
     - Return success
  3. If no partial stack found:
     - Find empty slot
     - Create new stack with quantity
     - Return success
  4. If no empty slot:
     - Return failure (inventory full)

Stack Overflow Handling:
  - If quantity exceeds max stack size:
    - Fill current stack to max
    - Create new stack with remainder
    - Continue until all items placed or inventory full
  
  Example: Adding 150 wood to stack of 80 (max 100)
    - Existing stack: 80 + 20 = 100 (full)
    - New stack: 130 remaining = 100 (new full stack)
    - New stack: 30 remaining = 30 (partial stack)
    - Result: [100, 100, 30] across 3 slots
```

**Weight Validation**:
```
Before adding item:
  1. Calculate current total weight
  2. Add item weight × quantity
  3. If new total > max weight:
     - Block pickup
     - Show "Too Heavy" message
     - Item remains in world
  4. If new total ≤ max weight:
     - Allow pickup
     - Add to inventory

Weight Warning Thresholds:
  - At 90% capacity: Yellow warning
  - At 95% capacity: Orange warning  
  - At 100% capacity: Red warning, block pickup
```

**Overflow Handling**:
```
Inventory Full Message:
  - Text: "Inventory Full"
  - Display: Center screen, 2 seconds
  - Color: Red
  - Sound: Error chime

Too Heavy Message:
  - Text: "Too Heavy - Drop Items"
  - Display: Center screen, 3 seconds
  - Color: Red
  - Sound: Heavy error sound

Item Drop on Failure:
  - Item remains as physical object in world
  - Location: At player's feet
  - Duration: 5 minutes until despawn
  - Recovery: Can pick up again after making space
```

### Removing Items

**Removal Methods**:
```
1. Drop (Q key default)
   - Action: Press Q to drop held/active item
   - Quantity: Drops entire stack by default
   - Location: 1 meter in front of player
   - Physics: Item has collision, can roll
   - Recovery: Can pick up within 5 minutes

2. Use/Consume
   - Action: Right-click or hotkey use
   - Types: Food, potions, tools
   - Destruction: Item consumed/destroyed
   - Effect: Applies item's effect

3. Crafting Consumption
   - Action: Crafting recipe uses materials
   - Automatic: Deducted when crafting starts
   - Quantity: Exact recipe amount
   - Return: Cannot undo

4. Trading/Giving
   - Action: Transfer to other entity
   - UI: Drag to trade window or player
   - Confirmation: May require accept
   - Immediate: Transfer on completion

5. Container Storage
   - Action: Move to chest/storage
   - UI: Drag to container slot
   - Ownership: Container's inventory
   - Security: Based on container permissions

6. Destruction
   - Action: Delete item permanently
   - Method: Drag to trash icon or destroy button
   - Confirmation: "Are you sure?" dialog
   - Recovery: None (item gone forever)
```

**Drop Mechanics**:
```
Drop Physics:
  - Spawn location: 1m in front of player, 0.5m height
  - Initial velocity: Forward vector × 2 m/s
  - Gravity: Standard 9.8 m/s²
  - Collision: Box collider matching item size
  - Lifetime: 5 minutes (300 seconds)

Item Entity Properties:
  - Type: Dynamic physics object
  - Mass: Matches item weight
  - Interactable: Can be picked up by anyone
  - Highlight: Glow effect when near
  - Label: Item name + quantity

Despawn Timer:
  - Duration: 5 minutes
  - Warning: Flashing at 4:30 mark
  - Final: Item destroyed, no recovery
  - Exception: High-value items (epic/legendary) never despawn

Ownership:
  - Dropped items: No owner (public)
  - Death drops: Owned by deceased player for 10 minutes
  - Stolen items: No special tracking
```

**Bulk Operations**:
```
Shift+Click: Move Half Stack
  - Action: Splits stack 50/50
  - Round up: Odd numbers favor destination
  - Example: 99 items → 49 and 50
  - Works between inventories

Ctrl+Click: Move Single Unit
  - Action: Transfer 1 item from stack
  - Precision: Exact quantity control
  - Useful: Splitting stacks manually
  - Modifier: Hold Ctrl while clicking

Alt+Click: Mark for Bulk Action
  - Action: Toggles selection highlight
  - Multiple: Can mark many items
  - Action: Apply bulk operation to all marked
  - Operations: Drop all marked, Move all marked

Drag and Drop: Specific Amount
  - Action: Drag stack, release
  - Dialog: "How many?" input
  - Precision: Exact number entry
  - Confirm: Release to complete

"Drop All" Button:
  - Location: Inventory UI bottom
  - Action: Drop all non-equipped items
  - Confirmation: "Drop everything?" dialog
  - Dangerous: Use with caution
```

### Moving Items

**Drag and Drop System**:
```
Basic Drag Operation:
  1. Mouse down on item (hold 0.1s to start drag)
  2. Item icon follows cursor
  3. Valid drop targets highlight green
  4. Release mouse to drop
  5. Item transfers to target slot

Drag Visual Feedback:
  - Cursor: Changes to hand grasping item
  - Item: Semi-transparent icon follows mouse
  - Source slot: Dimmed (50% opacity)
  - Target slot: Highlighted border
  - Invalid: Red X over cursor

Swap Behavior:
  - If target slot occupied by different item:
    - Items swap positions
    - Both move simultaneously
  - If target slot same item (stackable):
    - Stacks merge up to max
    - Remainder stays in source

Cancel Drag:
  - Press Escape
  - Right-click
  - Release outside valid zones
  - Item returns to original slot
```

**Keyboard Shortcuts**:
```
Shift+Click: Quick Transfer
  - Action: Move item to other inventory
  - Context: Player ↔ Container
  - Quantity: Full stack or half (configurable)
  - Speed: Faster than drag-and-drop

Ctrl+Click: Split Stack
  - Action: Divide stack in half
  - Precision: 50/50 split
  - New stack: Created in first empty slot
  - Original: Reduced by half

Alt+Click: Mark Item
  - Action: Toggle selection for bulk
  - Visual: Highlight border on item
  - Multiple: Can mark many items
  - Clear: Click again to unmark

Number Keys (1-9): Hotbar Assignment
  - Action: Move to hotbar slot
  - Immediate: Item appears in hotbar
  - Swap: If hotbar occupied, items swap
  - Visual: Hotbar slot flashes

Other Shortcuts:
  - Double-click: Use/equip item
  - Middle-click: Drop single unit
  - R key: Quick equip (armor/tools)
```

**Between Inventory Types**:
```
Player ↔ Container:
  - Open container UI (chest, storage)
  - Side-by-side inventory view
  - Drag items between panels
  - Shift+click for quick transfer
  - "Take All" / "Store All" buttons

Player ↔ Trade Window:
  - Both players open trade
  - Offer area and inventory shown
  - Drag items to offer area
  - Cannot take back after accept
  - Cancel returns items to inventory

Container ↔ Container (via Player):
  - Player acts as bridge
  - Take from Container A
  - Store in Container B
  - No direct container-to-container

Player ↔ Crafting Station:
  - Station shows recipe requirements
  - Highlights items you have
  - Crafting consumes from inventory
  - Output added to inventory
```

---

## 4. Inventory UI Design

### Main Inventory Screen

**Layout (1920×1080 Reference)**:
```
┌─────────────────────────────────────────────────────────┐
│                    INVENTORY SCREEN                     │
├──────────────────┬──────────────────────────────────────┤
│                  │                                      │
│  EQUIPMENT       │      INVENTORY GRID                  │
│  PANEL           │      (64 slots, 8×8)                 │
│  (Left)          │                                      │
│                  │   ┌─┬─┬─┬─┬─┬─┬─┬─┐                  │
│  [Head]          │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│  [Body]          │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│  [Hands]         │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│  [Feet]          │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│  [Accessory]     │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│                  │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│  Stats Panel     │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│  (Weight/        │   ├─┼─┼─┼─┼─┼─┼─┼─┤                  │
│   Defense)       │   └─┴─┴─┴─┴─┴─┴─┴─┘                  │
│                  │                                      │
├──────────────────┴──────────────────────────────────────┤
│                                                         │
│  Weight: 45/100 kg (45%) [████████░░░░░░░░░░░░] GREEN   │
│  Credits: 1,250 Cr                                      │
│                                                         │
│  [Sort ▼] [Drop All] [Close]                            │
│                                                         │
└─────────────────────────────────────────────────────────┘

Panel Dimensions:
  - Equipment Panel: 200×400 pixels
  - Inventory Grid: 544×544 pixels (64×64 slots + 4px gaps)
  - Weight Bar: 400×24 pixels
  - Total Window: ~800×700 pixels
```

**Slot Design**:
```
Slot Properties:
  - Size: 64×64 pixels
  - Border: 2px solid (color varies by state)
  - Background: Dark gray (#2A2A2A)
  - Corner radius: 4px

Slot States:
  - Empty: Dark gray background only
  - Occupied: Item icon centered (56×56)
  - Selected: Gold border (#FFD700), 3px
  - Hovered: Light border (#FFFFFF)
  - Drag source: 50% opacity
  - Drop target: Green border (#00FF00)
  - Invalid drop: Red border (#FF0000)

Quantity Badge:
  - Position: Bottom-right of slot
  - Size: 20×20 pixels
  - Background: Semi-transparent black
  - Text: White, bold, 12px font
  - Format: "99+" for stacks over 99
```

**Weight Bar Design**:
```
Bar Properties:
  - Width: 400 pixels
  - Height: 24 pixels
  - Background: Dark gray (#1A1A1A)
  - Fill: Color-coded by encumbrance tier
  - Border: 1px solid gray

Color Coding:
  - Light (0-50%): Green (#00FF00)
  - Medium (51-75%): Yellow (#FFFF00)
  - Heavy (76-90%): Orange (#FFA500)
  - Overburdened (91-100%): Red (#FF0000)

Text Display:
  - Format: "Current / Max kg (Percentage%)"
  - Example: "45/100 kg (45%)"
  - Font: 14px, white
  - Position: Center of bar or above

Warning States:
  - Over 90%: Bar pulses slowly
  - Over 95%: Bar pulses rapidly
  - 100%: Flashing red overlay
```

### Item Tooltip

**Tooltip Content**:
```
Hover over item displays:
  ┌────────────────────────────┐
  │ Iron Pickaxe               │ ← Name (colored by rarity)
  │ ─────────────────────────  │
  │ A sturdy mining tool.      │ ← Description
  │                            │
  │ Weight: 3.0 kg             │ ← Weight
  │ Value: 75 Cr               │ ← Value (if known)
  │                            │
  │ Efficiency: +50%           │ ← Stats (if equipment)
  │ Durability: 120/150        │ ← Durability (if applicable)
  │                            │
  │ [Right-click for options]  │ ← Hint
  └────────────────────────────┘
```

**Tooltip Styling**:
```
Visual Properties:
  - Background: Semi-transparent black (#000000CC)
  - Border: 1px solid gray (#666666)
  - Corner radius: 6px
  - Padding: 12px
  - Max width: 300 pixels
  - Shadow: Drop shadow for depth

Text Styling:
  - Title: 16px, bold, rarity-colored
  - Description: 12px, white, italic
  - Stats: 12px, white, key-value pairs
  - Labels: Light gray (#AAAAAA)
  - Values: White or colored (green for positive)

Rarity Colors:
  - Common: White (#FFFFFF)
  - Uncommon: Green (#00FF00)
  - Rare: Blue (#0088FF)
  - Epic: Purple (#AA00FF)
  - Legendary: Gold (#FFD700)

Positioning:
  - Default: Offset 16px from cursor
  - Constraint: Stays within screen bounds
  - Flip: Moves to left if near right edge
  - Layer: Always on top
```

**Context Menu (Right-Click)**:
```
Right-click on item shows:
  ┌──────────────────┐
  │ Use              │
  │ Equip            │
  │ Drop             │
  │ Split Stack...   │
  │ Move to Hotbar   │
  │ Mark Favorite    │
  │ Destroy          │
  └──────────────────┘

Menu Properties:
  - Background: Dark gray
  - Width: 150 pixels
  - Item height: 32 pixels
  - Hover: Highlight row
  - Icons: Left side for quick recognition
```

### Container UI

**Container Interface**:
```
When accessing storage:
┌─────────────────────────────────────────────────────────┐
│  [Container Name]                    [Player Inventory] │
├──────────────────────────┬──────────────────────────────┤
│                          │                              │
│   CONTAINER              │   INVENTORY                  │
│   [30-300 slots]         │   [64 slots]                 │
│                          │                              │
│   ┌─┬─┬─┬─┬─┬─┐          │   ┌─┬─┬─┬─┬─┬─┬─┬─┐          │
│   ├─┼─┼─┼─┼─┼─┤          │   ├─┼─┼─┼─┼─┼─┼─┼─┤          │
│   ├─┼─┼─┼─┼─┼─┤          │   ├─┼─┼─┼─┼─┼─┼─┼─┤          │
│   │...│...│...│          │   ├─┼─┼─┼─┼─┼─┼─┼─┤          │
│                          │   └─┴─┴─┴─┴─┴─┴─┴─┘          │
│                          │                              │
├──────────────────────────┴──────────────────────────────┤
│                                                         │
│  [Search: _________] [Filter: All ▼] [Sort: Name ▼]    │
│                                                         │
│  [Take All] [Store All] [Quick Stack] [Transfer]        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**Container Features**:
```
Search Box:
  - Real-time filtering
  - Searches item names
  - Case-insensitive
  - Clear button (X)

Filter Dropdown:
  - All Items
  - Building Materials
  - Raw Materials
  - Food
  - Tools
  - Equipment
  - Valuables

Sort Options:
  - Name (A-Z)
  - Name (Z-A)
  - Quantity (High-Low)
  - Quantity (Low-High)
  - Value (High-Low)
  - Weight (Light-Heavy)
  - Recently Acquired

Quick Transfer Buttons:
  - Take All: Move all from container to player
  - Store All: Move all from player to container
  - Quick Stack: Only items that exist in container
  - Transfer: Move selected/marked items
```

---

## 5. Hotbar System

### Hotbar Slots

**Hotbar Configuration**:
```
Count: 9 slots
Position: Bottom center of screen
Size: 60×60 pixels per slot
Total width: 540 pixels (9 × 60)
Offset from bottom: 20 pixels
```

**Hotbar Layout**:
```
Screen Bottom:
                          ┌─┬─┬─┬─┬─┬─┬─┬─┬─┐
                          │1│2│3│4│5│6│7│8│9│
                          └─┴─┴─┴─┴─┴─┴─┴─┴─┘
                          
                          Active slot: Gold border
                          Keybinds: 1-9 keys
```

**Hotbar Functionality**:
```
Allowed Items:
  - Tools (pickaxes, axes, etc.)
  - Weapons (swords, bows)
  - Consumables (food, potions)
  - Building blueprints (if enabled)
  
Blocked Items:
  - Raw materials (wood, stone)
  - Crafting materials (ingots)
  - Non-usable items

Selection Methods:
  - Number keys 1-9: Direct selection
  - Scroll wheel: Rotate through hotbar
  - Click: Mouse click on slot

Active Slot Visual:
  - Highlight: 3px gold border (#FFD700)
  - Glow: Subtle glow effect
  - Animation: Slight scale up (105%)
  
Item Usage:
  - Left-click: Use/equip active item
  - Right-click: Alternative use (if applicable)
  - Hold: Charge up (for some items)
```

**Quick Stack Feature**:
```
Usage Scenario:
  1. Player opens container
  2. Clicks "Quick Stack" button
  3. System identifies items in container
  4. Moves matching items from player inventory
  5. Leaves non-matching items

Example:
  - Container has: Wood (50), Stone (20)
  - Player has: Wood (80), Stone (30), Iron (10)
  - Quick Stack result:
    - Wood moved: 80 → container (now 130 total)
    - Stone moved: 30 → container (now 50 total)
    - Iron stays: Not in container originally

Smart Stacking:
  - Respects stack limits
  - Fills partial stacks first
  - Only moves exact item matches
  - Ignores equipped items
```

---

## 6. Special Inventory Types

### Equipment Slots

**Equipment Panel**:
```
Separate from main inventory:

┌────────────────┐
│  [HEAD]        │ ← Hats, helmets, crowns
│      ▼         │
│  [BODY]        │ ← Clothing, armor, robes
│      ▼         │
│  [HANDS]       │ ← Gloves, gauntlets, rings
│      ▼         │
│  [FEET]        │ ← Boots, shoes, sandals
│      ▼         │
│  [BACK]        │ ← Capes, backpacks, wings
│      ▼         │
│  [BELT]        │ ← Tools, pouches, accessories
│      ▼         │
│  [WEAPON]      │ ← Main hand weapon
│      ▼         │
│  [OFFHAND]     │ ← Shield, torch, secondary
└────────────────┘
```

**Equipment Categories**:
```
Head Slot:
  - Cloth hats: Light armor
  - Leather helmets: Medium armor
  - Metal helmets: Heavy armor
  - Crowns: Special items

Body Slot:
  - Shirts/robes: No armor
  - Leather armor: Light protection
  - Chainmail: Medium protection
  - Plate armor: Heavy protection

Hands Slot:
  - Gloves: Minor protection
  - Gauntlets: Major protection
  - Rings: Magical effects
  - Can equip 2 rings (one per hand)

Feet Slot:
  - Shoes: Basic footwear
  - Boots: Protection + speed
  - Heavy boots: Max protection

Back Slot:
  - Capes: Cosmetic + minor stat
  - Backpacks: +Inventory capacity
  - Wings: Special (if applicable)

Belt Slot:
  - Tool belts: Quick tool access
  - Pouches: Extra storage
  - Accessories: Stat bonuses

Weapon Slot (Main Hand):
  - Swords, axes, maces
  - Pickaxes, tools (as weapons)
  - Two-handed weapons (occupies both hands)

Offhand Slot:
  - Shields: Block damage
  - Torches: Light source
  - Secondary weapon (dual wield)
```

**Equipment Effects**:
```
Stat Modifications:
  - Defense: Reduces incoming damage
  - Weight: Adds to encumbrance
  - Speed: Movement modifiers
  - Skills: Bonus to skill levels

Visual Changes:
  - Character model updates
  - Equipment visible on avatar
  - Different models per equipment type
  - Color variations supported

Weight Counts:
  - All equipped items count toward encumbrance
  - Heavy armor significantly impacts movement
  - Strategic choice: Protection vs. Mobility
```

### Crafting Interface

**Crafting UI**:
```
Special inventory view:

┌──────────────────────────────────────────────┐
│  CRAFTING STATION - [Station Name]           │
├───────────────────┬──────────────────────────┤
│                   │                          │
│  RECIPE LIST      │  SELECTED RECIPE         │
│                   │                          │
│  [Pickaxe]        │  Iron Pickaxe            │
│  [Axe]            │  ─────────────────       │
│  [Sword]          │  Requires:               │
│  [Helmet]         │    • Wood (2) ✓          │
│  [...]            │    • Iron Ingot (3) ✓    │
│                   │    • Leather (1) ✗       │
│                   │                          │
│  [Filter ▼]       │  You have: 0/1 Leather   │
│                   │                          │
│                   │  Output:                 │
│                   │  [Iron Pickaxe Preview]  │
│                   │                          │
│                   │  [Craft] [Craft All]     │
│                   │  Time: 30 seconds        │
└───────────────────┴──────────────────────────┘
```

**Crafting Features**:
```
Recipe Display:
  - Shows all available recipes
  - Unlocked by skill level
  - Filterable by category
  - Searchable by name

Ingredient Highlighting:
  - ✓ Green: You have enough
  - ✗ Red: You don't have enough
  - Yellow: You have some but not enough
  - Shows current count vs. required

Material Source:
  - Pulls from main inventory automatically
  - No separate crafting inventory needed
  - Materials consumed on craft start
  - Cannot cancel once started

Output Preview:
  - Shows resulting item
  - Displays stats and quality
  - Quality based on skill level
  - Can cancel before confirming
```

### Trading Interface

**Trading UI**:
```
Two-sided view:

┌──────────────────────────────────────────────┐
│  TRADE WITH [Player/Agent Name]              │
├───────────────────┬──────────────────────────┤
│   YOUR OFFER      │    THEIR OFFER           │
├───────────────────┼──────────────────────────┤
│                   │                          │
│  [Iron Sword]     │  [50 Credits]            │
│  [20 Wood]        │  [Coal (10)]             │
│                   │                          │
│  Value: 150 Cr    │  Value: 145 Cr           │
│                   │                          │
├───────────────────┴──────────────────────────┤
│                                              │
│   YOUR INVENTORY    │    THEIR INVENTORY     │
│   (Selectable)      │    (View Only)         │
│                                              │
│  [Items you can offer]  [Their items]        │
│                                              │
├──────────────────────────────────────────────┤
│                                              │
│  [You: Accept ✓]    [Them: Waiting...]       │
│                                              │
│  Status: Waiting for both to accept          │
│                                              │
│  [Accept] [Modify Offer] [Cancel]            │
└──────────────────────────────────────────────┘
```

**Trading Mechanics**:
```
Offer Creation:
  - Drag items from inventory to offer area
  - Can add credits to offer
  - Can remove items before accepting
  - Shows total value of offer

Acceptance Process:
  1. Both parties add items to offer
  2. Both click "Ready" to review
  3. Both click "Accept" to confirm
  4. 3-second countdown for final confirmation
  5. Items transfer simultaneously

Safety Features:
  - Both must accept
  - Items locked during countdown
  - Either party can cancel anytime before final
  - Confirmation dialog for high-value trades

Value Display:
  - Shows approximate credit value
  - Helps ensure fair trades
  - Warning if value difference >50%
```

---

## 7. Item Properties

### Item Rarity System

**Rarity Tiers**:
```
Common (White) - 70% of items
  - Color: #FFFFFF
  - Description: Basic, widely available
  - Examples: Wood, stone, simple tools
  - Value: Base price
  - Effects: None

Uncommon (Green) - 20% of items
  - Color: #00FF00
  - Description: Slightly better quality
  - Examples: Iron tools, processed materials
  - Value: 1.2× base price
  - Effects: Minor stat bonuses

Rare (Blue) - 8% of items
  - Color: #0088FF
  - Description: Notable improvement
  - Examples: Steel tools, quality armor
  - Value: 1.5× base price
  - Effects: Significant bonuses

Epic (Purple) - 1.9% of items
  - Color: #AA00FF
  - Description: Exceptional quality
  - Examples: Mastercrafted items, rare materials
  - Value: 2.5× base price
  - Effects: Major bonuses, special properties
  - Visual: Subtle glow effect

Legendary (Gold) - 0.1% of items
  - Color: #FFD700
  - Description: Unique, game-changing
  - Examples: Named artifacts, unique items
  - Value: 5×+ base price
  - Effects: Powerful unique abilities
  - Visual: Golden glow, particle effects
```

**Rarity Effects**:
```
Tooltip Color:
  - Item name colored by rarity
  - Border matches rarity
  - Easy visual identification

Value Multiplier:
  - Higher rarity = higher base value
  - NPCs pay more for rare items
  - Trading value increased

Crafting Requirements:
  - Rare+ items need better materials
  - Higher skill requirements
  - More complex recipes

Durability Bonus:
  - Uncommon: +10% durability
  - Rare: +25% durability
  - Epic: +50% durability
  - Legendary: +100% durability (or unbreakable)
```

### Item States

**Condition States**:
```
Normal: Standard condition
  - 100% effectiveness
  - Normal durability decay
  - No special indicators

Damaged: Reduced effectiveness
  - <75% durability remaining
  - 75% effectiveness
  - Visual: Slight wear on icon
  - Warning: "Item is damaged"

Broken: Unusable
  - 0% durability
  - Cannot use item
  - Visual: Cracked icon, grayed out
  - Must repair before use

Repaired: Recently fixed
  - Restored to 80% of max durability
  - Can use normally
  - Visual: "Repaired" tag
```

**Special States**:
```
Enchanted: Magical properties
  - Has bonus stats or effects
  - Visual: Magic glow, sparkles
  - Tooltip shows enchantments
  - Examples: "+10% gathering speed"

Cursed: Negative effects
  - Has drawbacks when equipped
  - Examples: -5% speed, bad luck
  - Visual: Dark aura, red tints
  - Requires special removal

Blessed: Divine bonus
  - Positive effects without enchantment
  - Examples: +luck, +reputation
  - Visual: Holy glow, golden aura
  - Cannot be combined with cursed

Unique: One-of-a-kind
  - Only one exists in world
  - Cannot be crafted or duplicated
  - Usually quest-related
  - Visual: Special border, nameplate
```

---

## 8. Storage Systems

### Container Types

**Small Chest**:
```
Capacity: 30 slots
Crafting Recipe:
  - 10 wood planks
  - 2 iron nails

Properties:
  - Portable: Can pick up when empty
  - Weight: 5 kg (empty)
  - Access: Single user at a time
  - Decay: 30 days if unaccessed

Use Cases:
  - Personal storage at home
  - Temporary mining cache
  - Early game storage
```

**Large Chest**:
```
Capacity: 60 slots
Crafting Recipe:
  - 20 wood planks
  - 5 iron ingots
  - 4 iron nails

Properties:
  - Stationary: Must place and access
  - Weight: 15 kg (empty)
  - Access: Single user at a time
  - Upgrade: Can upgrade from small chest

Use Cases:
  - Main home storage
  - Workshop storage
  - Resource stockpile
```

**Crate**:
```
Capacity: 20 slots
Crafting Recipe:
  - 8 wood planks (cheap)

Properties:
  - Portable: Can pick up with contents
  - Weight: 3 kg + contents
  - Vehicle: Can load onto carts/trucks
  - Temporary: Lower decay resistance

Use Cases:
  - Transporting goods
  - Temporary project storage
  - Trade shipments
```

**Warehouse**:
```
Capacity: 300 slots
Crafting Recipe:
  - Building module (not craftable item)
  - Requires: 100 stone, 50 wood, 20 iron

Properties:
  - Structure: Must be built as building
  - Multi-user: Multiple players can access
  - Features:
    - Advanced sorting
    - Category filtering
    - Search functionality
    - Access logs
  - Security: Configurable permissions

Use Cases:
  - Town storage
  - Guild/shared resources
  - Market storage
```

**Safe**:
```
Capacity: 10 slots
Crafting Recipe:
  - 20 steel ingots
  - 5 gold ingots
  - 1 advanced mechanism

Properties:
  - Locked: Only owner can access
  - Security: Highest level
  - Immune: Cannot be stolen from
  - Location: Hidden possible
  - Weight: 50 kg (heavy)

Use Cases:
  - High-value items
  - Emergency supplies
  - Secret storage
```

**Specialized Storage**:
```
Food Pantry:
  - Capacity: 40 slots
  - Bonus: Food lasts 2× longer
  - Recipe: 15 wood + 5 stone

Tool Rack:
  - Capacity: 20 slots (tools only)
  - Bonus: Tools repair slowly when stored
  - Recipe: 10 wood + 3 iron

Weapon Locker:
  - Capacity: 15 slots (weapons only)
  - Bonus: Weapons don't decay
  - Recipe: 20 iron + 5 steel

Refrigerator:
  - Capacity: 25 slots (food only)
  - Bonus: Food never spoils
  - Recipe: High-tech (late game)
```

### Storage Rules

**Ownership Types**:
```
Personal:
  - Owner: Single player/agent
  - Access: Owner only
  - Transfer: Can give/sell to others
  - Decay: Based on owner activity

Town:
  - Owner: Town/government
  - Access: All town citizens
  - Permissions: Mayor can restrict
  - Decay: Based on town activity

Public:
  - Owner: None (community)
  - Access: Anyone
  - Risk: Can be stolen from
  - Decay: 7 days (faster)

Private (List):
  - Owner: Creator
  - Access: Specific player list
  - Management: Owner adds/removes
  - Decay: Based on owner activity
```

**Decay System**:
```
Timer: 30 days without access
  - Access: Opening container resets timer
  - Check: Daily verification
  - Warning: 3-day warning before decay

Decay Process:
  1. Container unaccessed for 30 days
  2. Warning sent to owner
  3. After 3 more days, container "decays"
  4. Contents dropped on ground
  5. Container destroyed

Exceptions:
  - Safe: No decay
  - Town buildings: Based on town status
  - Active claims: Extend decay timers
```

**Theft and Security**:
```
Theft Mechanics:
  - Unsecured containers: Can steal from
  - Secured containers: Cannot steal
  - Crime flag: Theft tracked by system
  - Witnesses: Agents/players can report

Consequences:
  - Reputation loss: -10 to -50
  - Bounty: May get bounty placed
  - Law: Subject to town laws
  - Evidence: Crime logged

Security Levels:
  - None: Public access
  - Basic: Owner only
  - Town: Citizens only
  - Custom: Permission list
```

---

## 9. Item Database

### Item ID System

**ID Format**:
```
Format: [CATEGORY]_[TYPE]_[VARIANT]
Example: TOOL_PICKAXE_IRON

Full ID Structure:
  Category (3-4 chars) + Separator + 
  Type (variable) + Separator + 
  Variant (variable)

Integer IDs:
  Range: 1-65535
  Type: ushort (16-bit)
  Assignment: Sequential by category
```

**Category Codes**:
```
RAW: Raw materials
  - Examples: RAW_WOOD_OAK, RAW_ORE_IRON
  - ID Range: 1-5000

PROC: Processed materials
  - Examples: PROC_PLANK_OAK, PROC_INGOT_IRON
  - ID Range: 5001-10000

TOOL: Tools
  - Examples: TOOL_PICKAXE_STONE, TOOL_AXE_IRON
  - ID Range: 10001-15000

FOOD: Food items
  - Examples: FOOD_MEAT_COOKED, FOOD_BREAD
  - ID Range: 15001-20000

BUILD: Building materials
  - Examples: BUILD_STONE_BLOCK, BUILD_WOOD_WALL
  - ID Range: 20001-25000

EQUIP: Equipment/Armor
  - Examples: EQUIP_HELMET_IRON, EQUIP_BOOTS_LEATHER
  - ID Range: 25001-30000

WEAP: Weapons
  - Examples: WEAP_SWORD_STEEL, WEAP_BOW_WOOD
  - ID Range: 30001-35000

DECO: Decorations
  - Examples: DECO_PAINTING, DECO_STATUE
  - ID Range: 35001-40000

MISC: Miscellaneous
  - Examples: MISC_CURRENCY, MISC_DOCUMENT
  - ID Range: 40001-50000

QUEST: Quest items
  - Examples: QUEST_KEY_ANCIENT, QUEST_ARTIFACT
  - ID Range: 50001-60000

UNIQ: Unique items
  - Examples: UNIQ_LEGENDARY_SWORD
  - ID Range: 60001-65535
```

**Database Schema**:
```csharp
public struct ItemData
{
    public ushort Id;              // 1-65535
    public string Name;            // Display name
    public string Description;     // Flavor text
    public ItemCategory Category;  // Enum category
    public float Weight;           // kg per unit
    public int MaxStackSize;       // 1-10000
    public int BaseValue;          // Credits
    public byte Rarity;            // 0-4 (Common to Legendary)
    public bool IsEquipable;       // Can equip
    public EquipmentSlot Slot;     // If equipable
    public int DurabilityMax;      // If has durability
    public int[] Stats;            // Stat bonuses
    public string[] Tags;          // Search/filter tags
}
```

**Item Properties Table**:
```
Key Properties:
  - ID: Unique identifier
  - Name: Localized display name
  - Icon: 64×64 texture path
  - Model: 3D model (if applicable)
  - Weight: Physical weight in kg
  - StackSize: Maximum per slot
  - Value: Base credit value
  - Rarity: Drop/quality tier
  
Functional Properties:
  - EquipSlot: Where it equips
  - Durability: Uses before break
  - Repairable: Can be fixed
  - Consumable: Used on activate
  - CraftingMaterial: Used in recipes
  
Visual Properties:
  - IconColor: Tint color
  - GlowColor: Rarity glow
  - ParticleEffect: Special effects
  - SoundPickup: Audio on collect
  - SoundUse: Audio on use
```

---

## Implementation Notes

### Data Structure
```csharp
public class Inventory
{
    public const int MAX_SLOTS = 64;
    public const float MAX_WEIGHT = 100.0f;
    
    public InventorySlot[] Slots = new InventorySlot[MAX_SLOTS];
    public float CurrentWeight = 0.0f;
    
    public bool AddItem(Item item, int quantity)
    {
        // Check weight limit
        if (CurrentWeight + (item.Weight * quantity) > MAX_WEIGHT)
            return false;
        
        // Try to stack
        foreach (var slot in Slots)
        {
            if (slot.Item?.Id == item.Id && slot.Quantity < item.MaxStackSize)
            {
                int space = item.MaxStackSize - slot.Quantity;
                int toAdd = Math.Min(space, quantity);
                slot.Quantity += toAdd;
                quantity -= toAdd;
                CurrentWeight += item.Weight * toAdd;
                
                if (quantity == 0) return true;
            }
        }
        
        // Find empty slot for remainder
        // ... implementation
    }
}
```

### Network Synchronization
```csharp
// Only sync changes, not full inventory
public struct InventoryDelta
{
    public byte SlotIndex;
    public ushort ItemId;
    public int Quantity;
    public bool IsRemoval;
}

// Sync on:
// - Item pickup/drop
// - Item use/consumption
// - Item move between slots
// - Weight changes (encumbrance)
```

### UI Updates
```csharp
// Events for UI binding
public event Action<int, InventorySlot> OnSlotChanged;
public event Action<float> OnWeightChanged;
public event Action<EncumbranceTier> OnEncumbranceChanged;
```

---

**END OF DOCUMENT**

*This inventory system specification provides complete technical details for implementation. All constants reference the [Technical Constants](../meta/technical-constants.md) document.*
