# Weight Carrying System Specification

> **PROJECT**: Societies - AI-Powered Society Simulation Game  
> **DOCUMENT TYPE**: Core Gameplay System Specification  
> **STATUS**: Critical Design Document  
> **LAST UPDATED**: 2026-02-01  
> **VERSION**: v1.0.0  
> **PHASE**: MVP (Core Infrastructure)  
> **SESSION**: Session 3 - Core Gameplay Loops  

---

## 1. Executive Summary

### System Philosophy

The Weight Carrying System is a **critical gameplay mechanic** inspired by Eco's realistic logistics, designed to create meaningful gameplay consequences for material transport and settlement location. This system transforms simple inventory management into a strategic decision-making process that:

- **Forces infrastructure investment**: Players must build roads, carts, and transport networks
- **Creates economic niches**: Professional haulers and logistics become valuable roles
- **Influences settlement strategy**: Resource proximity matters more than raw availability
- **Encourages cooperation**: Heavy materials require multiple people or vehicles
- **Grounds the game in physical reality**: 1m³ blocks have real-world weights

### Design Pillars

| Pillar | Implementation |
|--------|---------------|
| **Realism** | Accurate kg/m³ densities from real-world materials |
| **Consequence** | Every material choice affects movement and logistics |
| **Progression** | Tools, vehicles, and infrastructure unlock new carrying capabilities |
| **Accessibility** | Clear UI feedback, gradual complexity introduction |
| **Multiplayer Impact** | Creates emergent gameplay through trade-offs |

### Key Numbers

- **Total Block Types**: 70+ distinct materials with unique weights
- **Player Base Capacity**: 100 kg (human average carrying capacity)
- **AI Agent Base**: 80 kg (slightly lower for balance)
- **Vehicle Range**: 200 kg (hand cart) to 10,000 kg (truck)
- **Weight Impact**: 0-100% of capacity affects movement speed (100% → 50%)

### Reference Games

- **Eco**: Primary inspiration - weight affects movement, vehicles essential
- **Satisfactory**: Bulk transport logistics concepts
- **Minecraft (modded)**: CarryOn mod - physical carrying mechanics
- **Factorio**: Belt and logistics networks (future phase reference)

---

## 2. Base Carrying Capacity

### 2.1 Default Capacities

| Entity Type | Base Capacity | Notes |
|-------------|---------------|-------|
| **Player (Human)** | 100 kg | Realistic average for fit adult |
| **AI Agent** | 80 kg | Slightly lower for balance |
| **Human with Backpack** | 150 kg | +50 kg equipment bonus |
| **AI with Backpack** | 130 kg | +50 kg equipment bonus |

### 2.2 Strength Skill Modifier

**Strength skill** provides linear carrying capacity increases:

```
Strength Level 0:  +0 kg  (base capacity)
Strength Level 1:  +20 kg
Strength Level 2:  +40 kg
Strength Level 3:  +60 kg
Strength Level 4:  +80 kg
Strength Level 5:  +100 kg (doubled capacity)
Strength Level 6:  +120 kg
Strength Level 7:  +140 kg
Strength Level 8:  +160 kg
Strength Level 9:  +180 kg
Strength Level 10: +200 kg (triple capacity)

Formula: Bonus = (Strength Level × 20) kg
```

### 2.3 Equipment Modifiers

| Equipment | Capacity Bonus | Weight Cost | Requirements |
|-----------|---------------|-------------|--------------|
| **Backpack** | +50 kg | 2 kg | Crafted (Cloth ×6) |
| **Heavy Backpack** | +100 kg | 5 kg | Strength 3+, Crafted |
| **Load Harness** | +75 kg | 4 kg | Strength 2+, Metalworking |
| **Shoulder Yoke** | +60 kg | 3 kg | Carpentry Level 2 |
| **Weight Belt** | +30 kg | 1.5 kg | Leatherworking Level 1 |

**Maximum Natural Capacity**: 300 kg (Player, Strength 10, Heavy Backpack)

### 2.4 C# Implementation - Base Capacity

```csharp
namespace Societies.Carrying {
    
    public class CarryingCapacity {
        
        // Base values
        public const float BASE_PLAYER_CAPACITY = 100.0f;
        public const float BASE_AI_CAPACITY = 80.0f;
        public const float STRENGTH_BONUS_PER_LEVEL = 20.0f;
        
        // Current entity stats
        public float CurrentCapacity { get; private set; }
        public float CurrentWeight { get; private set; }
        public float RemainingCapacity => CurrentCapacity - CurrentWeight;
        public float EncumbrancePercent => (CurrentWeight / CurrentCapacity) * 100.0f;
        
        private Entity _owner;
        private List<CapacityModifier> _modifiers;
        
        public CarryingCapacity(Entity owner) {
            _owner = owner;
            _modifiers = new List<CapacityModifier>();
            RecalculateCapacity();
        }
        
        public void RecalculateCapacity() {
            float baseCapacity = _owner.IsPlayer ? BASE_PLAYER_CAPACITY : BASE_AI_CAPACITY;
            
            // Add strength bonus
            float strengthBonus = _owner.Skills.GetLevel(SkillType.Strength) * STRENGTH_BONUS_PER_LEVEL;
            
            // Add equipment bonuses
            float equipmentBonus = CalculateEquipmentBonus();
            
            CurrentCapacity = baseCapacity + strengthBonus + equipmentBonus;
        }
        
        private float CalculateEquipmentBonus() {
            float bonus = 0;
            foreach (var item in _owner.Inventory.GetEquippedItems()) {
                if (item is ICapacityModifier modifier) {
                    bonus += modifier.CapacityBonus;
                }
            }
            return bonus;
        }
        
        public bool CanAddWeight(float weight) {
            return (CurrentWeight + weight) <= CurrentCapacity;
        }
        
        public void AddWeight(float weight) {
            CurrentWeight += weight;
            ClampWeight();
        }
        
        public void RemoveWeight(float weight) {
            CurrentWeight = Mathf.Max(0, CurrentWeight - weight);
        }
        
        private void ClampWeight() {
            // Allow temporary overload (up to 120%), but warn
            if (CurrentWeight > CurrentCapacity * 1.2f) {
                CurrentWeight = CurrentCapacity * 1.2f;
            }
        }
        
        public float GetMovementSpeedMultiplier() {
            return EncumbranceCalculator.CalculateSpeedMultiplier(EncumbrancePercent);
        }
    }
    
    public interface ICapacityModifier {
        float CapacityBonus { get; }
        float WeightCost { get; }
    }
}
```

---

## 3. Complete Block Weight Table

### 3.1 Weight Calculation Methodology

All block weights are based on **real-world material densities** × 1m³ volume:

```
Weight (kg) = Density (kg/m³) × Volume (1.0 m³) × Integrity Factor
```

**Integrity Factor**: Most blocks are solid, but some (like leaves) have lower density:
- Solid blocks: 1.0
- Compacted/loose materials: 0.6-0.9
- Plant matter: 0.1-0.3
- Refined materials: Based on actual density

### 3.2 Natural Terrain Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Stone (Generic)** | 2,600 kg/m³ | 2,500 kg | Basalt/Granite average |
| **Granite** | 2,650-2,750 | 2,700 kg | Hard igneous rock |
| **Basalt** | 2,800-3,000 | 2,800 kg | Dense volcanic rock |
| **Limestone** | 2,000-2,700 | 2,400 kg | Sedimentary, lighter |
| **Sandstone** | 2,000-2,600 | 2,200 kg | Porous, erodible |
| **Marble** | 2,500-2,800 | 2,600 kg | Metamorphic, polished |
| **Slate** | 2,700-2,800 | 2,700 kg | Fine-grained metamorphic |
| **Obsidian** | 2,350-2,600 | 2,400 kg | Volcanic glass |
| **Dirt/Soil** | 1,200-1,800 | 1,600 kg | Compacted topsoil |
| **Sand** | 1,500-1,700 | 1,500 kg | Loose, granular |
| **Gravel** | 1,600-1,900 | 1,800 kg | Mixed stone aggregate |
| **Clay** | 1,600-2,000 | 1,800 kg | Wet, malleable |
| **Silt** | 1,400-1,800 | 1,600 kg | Fine sediment |
| **Peat** | 200-500 | 400 kg | Organic, decomposed |
| **Mud** | 1,400-1,800 | 1,600 kg | Water-saturated soil |

### 3.3 Organic/Plant Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Wood (Generic)** | 500-800 | 700 kg | Average dried lumber |
| **Oak Wood** | 600-900 | 750 kg | Hard hardwood |
| **Pine Wood** | 350-500 | 450 kg | Softwood, light |
| **Birch Wood** | 600-700 | 650 kg | Medium hardwood |
| **Jungle Wood** | 800-1,100 | 900 kg | Dense tropical |
| **Dark Oak** | 700-900 | 800 kg | Premium hardwood |
| **Spruce Wood** | 400-600 | 500 kg | Light softwood |
| **Leaves** | 100-300 | 200 kg | Loose, non-compact |
| **Grass Block** | 1,400-1,700 | 1,600 kg | Soil + vegetation |
| **Hay Bale** | 100-200 | 150 kg | Dried, compressed |
| **Straw** | 50-150 | 100 kg | Loose, very light |
| **Bamboo** | 400-800 | 600 kg | Hollow sections |
| **Cactus** | 300-600 | 400 kg | Water-stored tissue |
| **Sugar Cane** | 200-400 | 300 kg | Fibrous stalks |
| **Mushroom (Block)** | 100-300 | 200 kg | Organic, porous |
| **Melon** | 600-900 | 700 kg | Water content |
| **Pumpkin** | 400-700 | 600 kg | Thick-walled |

### 3.4 Ore and Mineral Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Iron Ore** | 3,200-3,800 | 3,500 kg | Hematite/Magnetite |
| **Gold Ore** | 4,000-5,000 | 4,500 kg | Heavy, precious |
| **Copper Ore** | 2,500-3,500 | 3,000 kg | Chalcopyrite |
| **Silver Ore** | 3,500-4,500 | 4,000 kg | Argentite |
| **Coal Ore** | 1,100-1,500 | 1,400 kg | Bituminous coal |
| **Diamond Ore** | 2,800-3,500 | 3,200 kg | Kimberlite host rock |
| **Emerald Ore** | 2,600-3,000 | 2,800 kg | Beryl in matrix |
| **Lapis Lazuli Ore** | 2,400-2,800 | 2,600 kg | Dense metamorphic |
| **Redstone Ore** | 2,600-3,000 | 2,800 kg | Cinnabar-based |
| **Quartz Ore** | 2,500-2,800 | 2,650 kg | Silica mineral |
| **Ancient Debris** | 7,000-9,000 | 8,000 kg | Netherite precursor |
| **Bedrock** | 2,800-3,200 | 3,000 kg | Unbreakable marker |

### 3.5 Refined Material Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Stone Bricks** | 2,200-2,600 | 2,400 kg | Cut and mortared |
| **Cobblestone** | 2,000-2,400 | 2,200 kg | Rough, irregular |
| **Smooth Stone** | 2,500-2,800 | 2,600 kg | Polished surface |
| **Stone Tiles** | 2,200-2,600 | 2,400 kg | Thin, decorative |
| **Mossy Stone** | 2,200-2,500 | 2,300 kg | Lighter with moss |
| **Cracked Stone** | 2,000-2,400 | 2,200 kg | Structural weakness |
| **Chiseled Stone** | 2,400-2,700 | 2,500 kg | Decorative cut |
| **Stone Pillar** | 2,400-2,800 | 2,600 kg | Solid support |
| **Terracotta** | 1,800-2,200 | 2,000 kg | Fired clay |
| **Glazed Terracotta** | 1,800-2,100 | 1,950 kg | Lighter glaze layer |
| **Concrete** | 2,200-2,500 | 2,400 kg | Modern material |
| **Concrete Powder** | 1,400-1,800 | 1,600 kg | Unset, loose |
| **Brick** | 1,600-2,000 | 1,800 kg | Fired clay units |
| **Nether Brick** | 2,000-2,400 | 2,200 kg | Nether material |
| **Prismarine** | 2,600-2,900 | 2,750 kg | Oceanic crystal |
| **Dark Prismarine** | 2,700-3,000 | 2,850 kg | Compressed variant |
| **End Stone** | 2,400-2,700 | 2,550 kg | Alien composition |
| **End Stone Bricks** | 2,200-2,600 | 2,400 kg | Refined end stone |
| **Purpur Block** | 2,100-2,500 | 2,300 kg | End material |
| **Purpur Pillar** | 2,200-2,600 | 2,400 kg | Solid purpur |

### 3.6 Metal Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Iron Block** | 7,850 | 7,800 kg | Pure iron |
| **Gold Block** | 19,300 | 19,300 kg | Extremely heavy |
| **Copper Block** | 8,960 | 9,000 kg | Conductive metal |
| **Silver Block** | 10,500 | 10,500 kg | Dense precious |
| **Lead Block** | 11,340 | 11,300 kg | Toxic, very heavy |
| **Tin Block** | 7,300 | 7,300 kg | Alloy component |
| **Bronze Block** | 8,800 | 8,800 kg | Copper-tin alloy |
| **Steel Block** | 7,850 | 7,850 kg | Iron-carbon alloy |
| **Nickel Block** | 8,900 | 8,900 kg | Corrosion-resistant |
| **Platinum Block** | 21,450 | 21,500 kg | Densest practical |
| **Titanium Block** | 4,500 | 4,500 kg | Strong, light |
| **Aluminum Block** | 2,700 | 2,700 kg | Lightweight metal |
| **Brass Block** | 8,400 | 8,400 kg | Copper-zinc alloy |
| **Zinc Block** | 7,140 | 7,100 kg | Galvanizing metal |
| **Chromium Block** | 7,190 | 7,200 kg | Stainless component |
| **Cobalt Block** | 8,900 | 8,900 kg | High-performance |
| **Netherite Block** | 15,000 | 15,000 kg | Fictional, very heavy |

### 3.7 Glass and Crystal Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Glass** | 2,400-2,600 | 2,500 kg | Silica glass |
| **Stained Glass** | 2,400-2,600 | 2,500 kg | Color doesn't affect weight |
| **Glass Pane** | 400-600 | 500 kg | Thin, 1/5th weight |
| **Sea Lantern** | 2,200-2,500 | 2,400 kg | Prismarine + glow |
| **Glowstone** | 500-1,000 | 750 kg | Luminescent, porous |
| **Beacon** | 3,000-3,500 | 3,200 kg | Dense mechanism |
| **Crystal Block** | 2,600-2,800 | 2,700 kg | Quartz structure |
| **Amethyst Block** | 2,650 | 2,650 kg | Purple quartz |
| **Budding Amethyst** | 2,600 | 2,600 kg | Growing crystal |
| **Ice** | 917 | 900 kg | Frozen water |
| **Packed Ice** | 950 | 950 kg | Compressed ice |
| **Blue Ice** | 980 | 980 kg | Dense ice variant |
| **Frosted Ice** | 900 | 900 kg | Fragile ice |

### 3.8 Nether Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Netherrack** | 2,800-3,200 | 3,000 kg | Nether stone |
| **Soul Sand** | 1,200-1,600 | 1,400 kg | Volcanic ash sand |
| **Soul Soil** | 1,000-1,400 | 1,200 kg | Denser soul sand |
| **Basalt (Polished)** | 2,800-3,000 | 2,900 kg | Refined volcanic |
| **Blackstone** | 2,600-2,900 | 2,750 kg | Dark volcanic rock |
| **Polished Blackstone** | 2,600-2,900 | 2,750 kg | Smooth variant |
| **Gilded Blackstone** | 3,000-3,500 | 3,200 kg | Gold-infused |
| **Nether Gold Ore** | 4,500-5,500 | 5,000 kg | Dense with gold |
| **Warped Stem** | 600-900 | 750 kg | Nether fungus wood |
| **Crimson Stem** | 600-900 | 750 kg | Red fungus wood |
| **Shroomlight** | 300-500 | 400 kg | Glowing fungus |
| **Twisting Vines** | 50-150 | 100 kg | Hanging plant |
| **Weeping Vines** | 50-150 | 100 kg | Ceiling plant |
| **Nether Wart Block** | 400-700 | 550 kg | Compressed fungus |
| **Warped Wart Block** | 400-700 | 550 kg | Blue fungus variant |

### 3.9 Utility/Mechanism Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Crafting Table** | 600-900 | 750 kg | Wooden with tools |
| **Furnace** | 2,200-2,600 | 2,400 kg | Stone construction |
| **Blast Furnace** | 3,000-3,500 | 3,200 kg | Reinforced furnace |
| **Smoker** | 2,000-2,400 | 2,200 kg | Food smoker |
| **Chest** | 400-700 | 600 kg | Wooden container |
| **Ender Chest** | 500-800 | 700 kg | Magical wood |
| **Trapped Chest** | 450-750 | 650 kg | Chest + mechanism |
| **Barrel** | 500-800 | 650 kg | Wooden cylinder |
| **Hopper** | 7,500 | 7,500 kg | Iron mechanism |
| **Dispenser** | 2,500 | 2,500 kg | Stone + mechanism |
| **Dropper** | 2,400 | 2,400 kg | Lighter dispenser |
| **Observer** | 2,200 | 2,200 kg | Detection device |
| **Piston** | 2,800 | 2,800 kg | Mechanical block |
| **Sticky Piston** | 2,900 | 2,900 kg | Piston + slime |
| **Slime Block** | 200-400 | 300 kg | Elastic organic |
| **Honey Block** | 1,400-1,600 | 1,500 kg | Viscous sugar |
| **Note Block** | 700-1,000 | 850 kg | Wooden instrument |
| **Jukebox** | 800-1,200 | 1,000 kg | Musical device |
| **TNT** | 1,500-1,800 | 1,650 kg | Explosive compound |
| **Bookshelf** | 400-700 | 600 kg | Wood + books |
| **Lectern** | 500-800 | 650 kg | Reading stand |
| **Anvil** | 7,800 | 7,800 kg | Heavy iron tool |
| **Chipped Anvil** | 7,500 | 7,500 kg | Damaged anvil |
| **Damaged Anvil** | 7,000 | 7,000 kg | Heavily damaged |
| **Composter** | 400-700 | 550 kg | Wooden bin |
| **Cauldron** | 7,500 | 7,500 kg | Iron vessel |
| **Brewing Stand** | 2,000-2,500 | 2,300 kg | Glass + metal |
| **Enchanting Table** | 3,000-3,500 | 3,200 kg | Magical stone |
| **Beacon** | 3,000-3,500 | 3,200 kg | Light mechanism |
| **Conduit** | 2,800-3,200 | 3,000 kg | Ocean structure |
| **Daylight Detector** | 1,500-2,000 | 1,750 kg | Sensor device |
| **Redstone Lamp** | 2,400 | 2,400 kg | Illumination |
| **Target** | 800-1,200 | 1,000 kg | Practice target |

### 3.10 Redstone/Logic Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Redstone Block** | 2,600 | 2,600 kg | Compressed dust |
| **Redstone Torch** | 100-300 | 200 kg | Light mechanism |
| **Redstone Repeater** | 500-800 | 650 kg | Logic component |
| **Redstone Comparator** | 500-800 | 650 kg | Comparison logic |
| **Lever** | 200-400 | 300 kg | Simple switch |
| **Button (Stone)** | 1,000-1,500 | 1,200 kg | Solid switch |
| **Button (Wood)** | 300-500 | 400 kg | Light switch |
| **Pressure Plate (Stone)** | 1,500-2,000 | 1,750 kg | Heavy trigger |
| **Pressure Plate (Wood)** | 400-700 | 550 kg | Light trigger |
| **Pressure Plate (Gold)** | 10,000 | 10,000 kg | Precious trigger |
| **Pressure Plate (Iron)** | 7,800 | 7,800 kg | Heavy trigger |
| **Tripwire Hook** | 200-400 | 300 kg | Detection hook |
| **Sculk Sensor** | 600-900 | 750 kg | Vibration detector |
| **Sculk Shrieker** | 700-1,000 | 850 kg | Alarm system |
| **Sculk Catalyst** | 800-1,100 | 950 kg | Experience collector |
| **Calibrated Sculk Sensor** | 700-1,000 | 850 kg | Tuned detector |

### 3.11 Decorative/Building Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Wool** | 100-300 | 200 kg | Light textile |
| **Carpet** | 30-60 | 50 kg | Very light |
| **Slab (Stone)** | 1,200-1,400 | 1,300 kg | Half block |
| **Slab (Wood)** | 300-450 | 400 kg | Half block |
| **Stairs (Stone)** | 1,800-2,200 | 2,000 kg | 3/4 block |
| **Stairs (Wood)** | 450-700 | 600 kg | 3/4 block |
| **Fence** | 150-250 | 200 kg | Post + rails |
| **Fence Gate** | 200-350 | 275 kg | Opening section |
| **Wall** | 1,400-1,800 | 1,600 kg | Thin barrier |
| **Trapdoor (Wood)** | 350-550 | 450 kg | Hinged panel |
| **Trapdoor (Iron)** | 3,900 | 3,900 kg | Heavy metal |
| **Door (Wood)** | 400-650 | 525 kg | Entry panel |
| **Door (Iron)** | 4,500 | 4,500 kg | Heavy security |
| **Ladder** | 50-100 | 75 kg | Climbing tool |
| **Scaffolding** | 80-150 | 100 kg | Temporary structure |
| **Sign** | 100-200 | 150 kg | Wooden board |
| **Item Frame** | 50-100 | 75 kg | Display holder |
| **Painting** | 30-60 | 50 kg | Canvas + frame |
| **Armor Stand** | 150-300 | 225 kg | Metal frame |
| **Flower Pot** | 300-500 | 400 kg | Clay container |
| **Heads/Skulls** | 1,500-2,500 | 2,000 kg | Trophy blocks |
| **Dragon Egg** | 5,000-8,000 | 6,500 kg | Mythical object |
| **End Rod** | 800-1,200 | 1,000 kg | Decorative light |
| **Lightning Rod** | 7,800 | 7,800 kg | Copper conductor |
| **Spyglass** | 500-800 | 650 kg | Viewing device |

### 3.12 Food/Agriculture Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Wheat** | 100-200 | 150 kg | Grain crop |
| **Carrots** | 600-700 | 650 kg | Root vegetable |
| **Potatoes** | 700-800 | 750 kg | Starchy tuber |
| **Beetroot** | 600-750 | 675 kg | Root crop |
| **Cocoa** | 500-700 | 600 kg | Bean pods |
| **Pumpkin** | 400-700 | 600 kg | Large fruit |
| **Melon** | 600-900 | 700 kg | Heavy fruit |
| **Sweet Berry Bush** | 50-150 | 100 kg | Small shrub |
| **Kelp** | 100-300 | 200 kg | Seaweed |
| **Sea Pickle** | 50-150 | 100 kg | Small marine |
| **Coral (Block)** | 1,500-2,000 | 1,750 kg | Calcium skeleton |
| **Coral Fan** | 200-400 | 300 kg | Thin coral |
| **Sponge** | 20-100 | 50 kg | Porous, light |
| **Wet Sponge** | 1,200-1,600 | 1,400 kg | Water-filled |
| **Dried Kelp Block** | 100-200 | 150 kg | Compressed |
| **Cake** | 400-700 | 550 kg | Baked good |

### 3.13 Snow/Water/Liquid Blocks

| Block Type | Real Density | In-Game Weight | Notes |
|------------|--------------|----------------|-------|
| **Snow (Block)** | 100-300 | 200 kg | Compressed snow |
| **Snow Layer** | 20-50 | 35 kg | Thin layer |
| **Powder Snow** | 50-150 | 100 kg | Dangerous variant |
| **Water** | 1,000 | 1,000 kg | Liquid standard |
| **Lava** | 2,500-3,000 | 2,800 kg | Molten rock |
| **Magma Block** | 2,800-3,200 | 3,000 kg | Cooled surface |
| **Bubble Column** | 1,000 | 1,000 kg | Water feature |

### 3.14 C# Implementation - Block Weights

```csharp
namespace Societies.Blocks {
    
    public static class BlockWeightDatabase {
        
        // Dictionary mapping BlockType to weight in kg
        private static readonly Dictionary<BlockType, float> _blockWeights = new() {
            // Natural Terrain
            [BlockType.Stone] = 2500f,
            [BlockType.Granite] = 2700f,
            [BlockType.Basalt] = 2800f,
            [BlockType.Limestone] = 2400f,
            [BlockType.Sandstone] = 2200f,
            [BlockType.Marble] = 2600f,
            [BlockType.Slate] = 2700f,
            [BlockType.Obsidian] = 2400f,
            [BlockType.Dirt] = 1600f,
            [BlockType.Sand] = 1500f,
            [BlockType.Gravel] = 1800f,
            [BlockType.Clay] = 1800f,
            [BlockType.Silt] = 1600f,
            [BlockType.Peat] = 400f,
            [BlockType.Mud] = 1600f,
            
            // Organic/Plant
            [BlockType.Wood] = 700f,
            [BlockType.OakWood] = 750f,
            [BlockType.PineWood] = 450f,
            [BlockType.BirchWood] = 650f,
            [BlockType.JungleWood] = 900f,
            [BlockType.DarkOakWood] = 800f,
            [BlockType.SpruceWood] = 500f,
            [BlockType.Leaves] = 200f,
            [BlockType.GrassBlock] = 1600f,
            [BlockType.HayBale] = 150f,
            [BlockType.Straw] = 100f,
            [BlockType.Bamboo] = 600f,
            [BlockType.Cactus] = 400f,
            [BlockType.SugarCane] = 300f,
            [BlockType.MushroomBlock] = 200f,
            [BlockType.Melon] = 700f,
            [BlockType.Pumpkin] = 600f,
            
            // Ores
            [BlockType.IronOre] = 3500f,
            [BlockType.GoldOre] = 4500f,
            [BlockType.CopperOre] = 3000f,
            [BlockType.SilverOre] = 4000f,
            [BlockType.CoalOre] = 1400f,
            [BlockType.DiamondOre] = 3200f,
            [BlockType.EmeraldOre] = 2800f,
            [BlockType.LapisOre] = 2600f,
            [BlockType.RedstoneOre] = 2800f,
            [BlockType.QuartzOre] = 2650f,
            [BlockType.AncientDebris] = 8000f,
            [BlockType.Bedrock] = 3000f,
            
            // Refined Materials
            [BlockType.StoneBricks] = 2400f,
            [BlockType.Cobblestone] = 2200f,
            [BlockType.SmoothStone] = 2600f,
            [BlockType.Terracotta] = 2000f,
            [BlockType.Concrete] = 2400f,
            [BlockType.Brick] = 1800f,
            [BlockType.EndStone] = 2550f,
            [BlockType.Prismarine] = 2750f,
            [BlockType.Netherrack] = 3000f,
            [BlockType.SoulSand] = 1400f,
            
            // Metals
            [BlockType.IronBlock] = 7800f,
            [BlockType.GoldBlock] = 19300f,
            [BlockType.CopperBlock] = 9000f,
            [BlockType.SilverBlock] = 10500f,
            [BlockType.LeadBlock] = 11300f,
            [BlockType.SteelBlock] = 7850f,
            [BlockType.BronzeBlock] = 8800f,
            [BlockType.AluminumBlock] = 2700f,
            [BlockType.TitaniumBlock] = 4500f,
            [BlockType.NetheriteBlock] = 15000f,
            
            // Glass/Crystal
            [BlockType.Glass] = 2500f,
            [BlockType.Glowstone] = 750f,
            [BlockType.Ice] = 900f,
            [BlockType.PackedIce] = 950f,
            [BlockType.BlueIce] = 980f,
            [BlockType.Amethyst] = 2650f,
            
            // Utility
            [BlockType.CraftingTable] = 750f,
            [BlockType.Furnace] = 2400f,
            [BlockType.Chest] = 600f,
            [BlockType.Hopper] = 7500f,
            [BlockType.Dispenser] = 2500f,
            [BlockType.Piston] = 2800f,
            [BlockType.SlimeBlock] = 300f,
            [BlockType.HoneyBlock] = 1500f,
            [BlockType.Bookshelf] = 600f,
            [BlockType.Anvil] = 7800f,
            [BlockType.EnchantingTable] = 3200f,
            
            // Decorative
            [BlockType.Wool] = 200f,
            [BlockType.Carpet] = 50f,
            [BlockType.StoneSlab] = 1300f,
            [BlockType.WoodSlab] = 400f,
            [BlockType.Door] = 525f,
            [BlockType.IronDoor] = 4500f,
            [BlockType.Trapdoor] = 450f,
            [BlockType.Ladder] = 75f,
            
            // Snow/Water
            [BlockType.SnowBlock] = 200f,
            [BlockType.Water] = 1000f,
            [BlockType.Lava] = 2800f,
            
            // Food/Agriculture
            [BlockType.HayBale] = 150f,
            [BlockType.Pumpkin] = 600f,
            [BlockType.Melon] = 700f,
        };
        
        public static float GetWeight(BlockType type) {
            return _blockWeights.TryGetValue(type, out float weight) ? weight : 2000f;
        }
        
        public static float GetWeight(string blockName) {
            if (Enum.TryParse<BlockType>(blockName, out BlockType type)) {
                return GetWeight(type);
            }
            return 2000f; // Default to stone-like weight
        }
        
        public static Dictionary<BlockCategory, List<(BlockType type, float weight)>> GetBlocksByCategory() {
            var categories = new Dictionary<BlockCategory, List<(BlockType, float)>>();
            
            foreach (var kvp in _blockWeights) {
                var category = GetCategory(kvp.Key);
                if (!categories.ContainsKey(category)) {
                    categories[category] = new List<(BlockType, float)>();
                }
                categories[category].Add((kvp.Key, kvp.Value));
            }
            
            return categories;
        }
        
        private static BlockCategory GetCategory(BlockType type) {
            // Simplified categorization logic
            return type.ToString() switch {
                var s when s.Contains("Ore") => BlockCategory.Ore,
                var s when s.Contains("Wood") || s.Contains("Log") => BlockCategory.Wood,
                var s when s.Contains("Stone") || s.Contains("Brick") => BlockCategory.Stone,
                var s when s.Contains("Metal") || s.Contains("Iron") || s.Contains("Gold") => BlockCategory.Metal,
                var s when s.Contains("Glass") || s.Contains("Ice") => BlockCategory.Glass,
                var s when s.Contains("Wool") || s.Contains("Carpet") => BlockCategory.Textile,
                var s when s.Contains("Leaves") || s.Contains("Grass") => BlockCategory.Organic,
                _ => BlockCategory.Miscellaneous
            };
        }
    }
    
    public enum BlockCategory {
        NaturalTerrain,
        Wood,
        Organic,
        Ore,
        Stone,
        Refined,
        Metal,
        Glass,
        Utility,
        Redstone,
        Decorative,
        Food,
        Textile,
        Nether,
        End,
        Miscellaneous
    }
}
```

---

## 4. Encumbrance System

### 4.1 Encumbrance Tiers

| Tier | Weight % | Speed Multiplier | Stamina Multiplier | Visual Indicator |
|------|----------|------------------|-------------------|------------------|
| **Light** | 0-30% | 100% | 100% | Green - Normal |
| **Medium** | 30-60% | 90% | 110% | Yellow - Slowed |
| **Heavy** | 60-90% | 75% | 125% | Orange - Burdened |
| **Overloaded** | 90-100% | 50% | 150% | Red - Struggling |
| **Immobilized** | >100% | 0% | - | Red Flashing - Stuck |

### 4.2 Detailed Tier Effects

#### Light (0-30% - Green)
- Movement: Full speed
- Stamina drain: Normal
- Jump height: Full
- Sprinting: Allowed
- UI: Green weight indicator

#### Medium (30-60% - Yellow)
- Movement: 10% reduction (90% speed)
- Stamina drain: +10% faster
- Jump height: 90% of normal
- Sprinting: Allowed (slower)
- UI: Yellow weight indicator

#### Heavy (60-90% - Orange)
- Movement: 25% reduction (75% speed)
- Stamina drain: +25% faster
- Jump height: 75% of normal
- Sprinting: Disabled
- UI: Orange weight indicator + warning text

#### Overloaded (90-100% - Red)
- Movement: 50% reduction (half speed)
- Stamina drain: +50% faster
- Jump height: Disabled (cannot jump)
- Sprinting: Disabled
- UI: Red weight indicator + "OVERLOADED" warning
- Cannot climb ladders
- Movement drains stamina continuously

#### Immobilized (>100% - Flashing Red)
- Movement: Complete stop
- Cannot jump, sprint, or climb
- Must drop items to move
- UI: Flashing red + "DROP ITEMS TO MOVE"
- Health drain: -1 HP per 5 seconds (optional hardcore mode)

### 4.3 Encumbrance Calculation Formula

```csharp
public static class EncumbranceCalculator {
    
    /// <summary>
    /// Calculates movement speed multiplier based on encumbrance percentage
    /// </summary>
    public static float CalculateSpeedMultiplier(float encumbrancePercent) {
        return encumbrancePercent switch {
            <= 30f => 1.0f,                           // Light: 100%
            <= 60f => Mathf.Lerp(1.0f, 0.9f, (encumbrancePercent - 30f) / 30f),   // Medium: 90-100%
            <= 90f => Mathf.Lerp(0.9f, 0.75f, (encumbrancePercent - 60f) / 30f),  // Heavy: 75-90%
            <= 100f => Mathf.Lerp(0.75f, 0.5f, (encumbrancePercent - 90f) / 10f), // Overloaded: 50-75%
            _ => 0.0f                                 // Immobilized: 0%
        };
    }
    
    /// <summary>
    /// Calculates stamina drain multiplier
    /// </summary>
    public static float CalculateStaminaMultiplier(float encumbrancePercent) {
        return encumbrancePercent switch {
            <= 30f => 1.0f,
            <= 60f => 1.1f,
            <= 90f => 1.25f,
            <= 100f => 1.5f,
            _ => 2.0f  // Rapid stamina drain when overloaded
        };
    }
    
    /// <summary>
    /// Calculates jump height multiplier
    /// </summary>
    public static float CalculateJumpMultiplier(float encumbrancePercent) {
        return encumbrancePercent switch {
            <= 60f => 1.0f,
            <= 90f => 0.75f,
            _ => 0.0f  // Cannot jump when overloaded
        };
    }
    
    /// <summary>
    /// Determines if sprinting is allowed
    /// </summary>
    public static bool CanSprint(float encumbrancePercent) {
        return encumbrancePercent <= 60f;
    }
    
    /// <summary>
    /// Determines if climbing is allowed
    /// </summary>
    public static bool CanClimb(float encumbrancePercent) {
        return encumbrancePercent <= 90f;
    }
    
    /// <summary>
    /// Get encumbrance tier for display
    /// </summary>
    public static EncumbranceTier GetTier(float encumbrancePercent) {
        return encumbrancePercent switch {
            <= 30f => EncumbranceTier.Light,
            <= 60f => EncumbranceTier.Medium,
            <= 90f => EncumbranceTier.Heavy,
            <= 100f => EncumbranceTier.Overloaded,
            _ => EncumbranceTier.Immobilized
        };
    }
    
    /// <summary>
    /// Get color for UI representation
    /// </summary>
    public static Color GetTierColor(EncumbranceTier tier) {
        return tier switch {
            EncumbranceTier.Light => new Color(0.2f, 0.8f, 0.2f),      // Green
            EncumbranceTier.Medium => new Color(0.9f, 0.9f, 0.2f),     // Yellow
            EncumbranceTier.Heavy => new Color(0.9f, 0.6f, 0.2f),      // Orange
            EncumbranceTier.Overloaded => new Color(0.9f, 0.2f, 0.2f), // Red
            EncumbranceTier.Immobilized => new Color(0.9f, 0.0f, 0.0f),// Bright Red
            _ => Color.White
        };
    }
}

public enum EncumbranceTier {
    Light,
    Medium,
    Heavy,
    Overloaded,
    Immobilized
}
```

---

## 5. Movement Effects

### 5.1 Movement Modifier System

The movement system integrates with encumbrance through a modifier pipeline:

```csharp
namespace Societies.Movement {
    
    public class MovementModifierSystem : Node {
        
        private Player _player;
        private float _baseSpeed;
        private float _baseJump;
        private float _baseStaminaCost;
        
        // Current multipliers
        public float SpeedMultiplier { get; private set; } = 1.0f;
        public float JumpMultiplier { get; private set; } = 1.0f;
        public float StaminaMultiplier { get; private set; } = 1.0f;
        
        public override void _Ready() {
            _player = GetParent<Player>();
            _baseSpeed = _player.BaseMovementSpeed;
            _baseJump = _player.BaseJumpHeight;
            _baseStaminaCost = _player.BaseStaminaCost;
        }
        
        public void UpdateFromEncumbrance(float encumbrancePercent) {
            SpeedMultiplier = EncumbranceCalculator.CalculateSpeedMultiplier(encumbrancePercent);
            JumpMultiplier = EncumbranceCalculator.CalculateJumpMultiplier(encumbrancePercent);
            StaminaMultiplier = EncumbranceCalculator.CalculateStaminaMultiplier(encumbrancePercent);
            
            ApplyMovementModifiers();
        }
        
        private void ApplyMovementModifiers() {
            // Apply to player controller
            _player.CurrentMovementSpeed = _baseSpeed * SpeedMultiplier;
            _player.CurrentJumpHeight = _baseJump * JumpMultiplier;
            _player.CurrentStaminaCost = _baseStaminaCost * StaminaMultiplier;
            
            // Update sprint availability
            _player.CanSprint = EncumbranceCalculator.CanSprint(
                _player.CarryingCapacity.EncumbrancePercent
            );
            
            // Update climb availability
            _player.CanClimb = EncumbranceCalculator.CanClimb(
                _player.CarryingCapacity.EncumbrancePercent
            );
        }
        
        /// <summary>
        /// Call every frame to handle continuous effects
        /// </summary>
        public override void _Process(double delta) {
            HandleOverloadedStaminaDrain((float)delta);
        }
        
        private void HandleOverloadedStaminaDrain(float delta) {
            float encumbrance = _player.CarryingCapacity.EncumbrancePercent;
            
            // Drain stamina when overloaded (even when not moving)
            if (encumbrance > 100f) {
                float drainRate = 5.0f * ((encumbrance - 100f) / 20f); // 5-15 stamina per second
                _player.Stamina -= drainRate * delta;
                
                // Warn player
                if (_player.Stamina < 20f) {
                    _player.ShowWarning("Drop items! Stamina critical!");
                }
            }
            
            // Additional drain when moving while heavy
            if (encumbrance > 90f && _player.IsMoving) {
                float movementDrain = 2.0f * ((encumbrance - 90f) / 10f);
                _player.Stamina -= movementDrain * delta;
            }
        }
    }
}
```

### 5.2 Terrain-Based Movement Modifiers

Weight affects movement differently based on terrain:

| Terrain | Light (0-30%) | Medium (30-60%) | Heavy (60-90%) | Overloaded (90-100%) |
|---------|---------------|-----------------|----------------|---------------------|
| **Flat Ground** | 100% | 90% | 75% | 50% |
| **Uphill** | 90% | 75% | 50% | 25% |
| **Downhill** | 110% | 100% | 85% | 60% |
| **Stairs** | 85% | 70% | 50% | 25% |
| **Ladders** | 75% | 60% | 40% | Blocked |
| **Water** | 60% | 40% | 20% | Drowning risk |
| **Ice** | 120% | 110% | 95% | Slippery |
| **Soul Sand** | 70% | 50% | 30% | 10% |
| **Slime** | 50% | 30% | 10% | Stuck |

### 5.3 Stamina System Integration

```csharp
public class StaminaSystem {
    
    public float CurrentStamina { get; private set; }
    public float MaxStamina { get; private set; }
    public float RegenRate { get; private set; }
    
    private CarryingCapacity _carrying;
    
    public void UpdateStamina(float deltaTime) {
        float encumbrance = _carrying.EncumbrancePercent;
        
        // Base regen when not moving and not overloaded
        if (!IsMoving && encumbrance <= 100f) {
            float regenMultiplier = encumbrance <= 60f ? 1.0f : 0.5f;
            CurrentStamina += RegenRate * regenMultiplier * deltaTime;
        }
        
        // Clamp to max
        CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
        
        // Exhaustion effects
        if (CurrentStamina <= 0) {
            ApplyExhaustion();
        }
    }
    
    private void ApplyExhaustion() {
        // Forced to drop items when exhausted and overloaded
        if (_carrying.EncumbrancePercent > 100f) {
            AutoDropHeaviestItems(1); // Drop 1 heaviest item
        }
        
        // Movement penalties
        SpeedMultiplier *= 0.5f;
        
        // Screen effects
        ApplyScreenDarkening();
    }
}
```

---

## 6. Transport Vehicles

### 6.1 Vehicle Tiers

| Vehicle | Capacity | Empty Weight | Crafting Difficulty | Tech Tier |
|---------|----------|--------------|---------------------|-----------|
| **Hand Cart** | 200 kg | 15 kg | Level 1 Carpentry | Primitive |
| **Wheelbarrow** | 300 kg | 12 kg | Level 2 Carpentry | Primitive |
| **Minecart** | 1,000 kg | 45 kg | Level 2 Metalworking | Industrial |
| **Wagon** | 500 kg | 120 kg | Level 3 Carpentry | Agricultural |
| **Hand Truck** | 150 kg | 8 kg | Level 1 Metalworking | Urban |
| **Boat (Small)** | 400 kg | 80 kg | Level 2 Carpentry | Maritime |
| **Boat (Cargo)** | 2,000 kg | 200 kg | Level 4 Carpentry | Maritime |
| **Truck (Future)** | 10,000 kg | 2,000 kg | Level 8 Engineering | Modern |

### 6.2 Hand Cart

**Specifications**:
- Capacity: 200 kg
- Empty Weight: 15 kg
- Dimensions: 1.2m wide × 0.8m tall × 1.5m long
- Crafting: 8 Wooden Planks, 4 Sticks, 16 Iron Nails, 2 Wooden Wheels
- Speed: Walking speed when pushed (3 m/s empty, 2 m/s loaded)

**Physics**:
- Can be pushed/pulled by 1 person
- Inertia increases with load (harder to start/stop)
- Can traverse slopes up to 15°
- Requires 2 people to push up stairs

```csharp
public class HandCart : TransportVehicle {
    
    public override void _Ready() {
        base._Ready();
        
        MaxCapacity = 200f;
        EmptyWeight = 15f;
        PushForce = 30f;
        MaxPushSpeed = 3.0f;
        
        // Physics setup
        Mass = EmptyWeight;
        CenterOfMass = new Vector3(0, 0.4f, 0);
        
        // Two-wheel configuration
        SetupWheel(new Vector3(-0.4f, 0.2f, 0.5f));
        SetupWheel(new Vector3(0.4f, 0.2f, 0.5f));
    }
    
    protected override void OnPushed(Vector3 direction, float force) {
        // Calculate load factor
        float loadFactor = 1.0f - (CurrentLoad / MaxCapacity * 0.5f);
        float effectiveSpeed = MaxPushSpeed * loadFactor;
        
        // Apply force
        LinearVelocity = LinearVelocity.MoveToward(
            direction * effectiveSpeed, 
            force * loadFactor * (float)GetPhysicsProcessDeltaTime()
        );
    }
}
```

### 6.3 Wheelbarrow

**Specifications**:
- Capacity: 300 kg
- Empty Weight: 12 kg
- Dimensions: 0.8m wide × 0.9m tall × 1.2m long
- Single wheel design
- Tilting dump mechanism
- Maneuverable in tight spaces

**Unique Mechanics**:
- Can "dump" contents at destination
- Maneuverability bonus (+20% turn speed)
- Less stable on slopes (risk of tipping)

### 6.4 Minecart

**Specifications**:
- Capacity: 1,000 kg
- Empty Weight: 45 kg
- Rail-based movement only
- Can couple up to 5 carts together
- Powered by gravity or push

**Rail System**:
- Requires rails to move
- Can travel up/down slopes
- Automatic braking at end of line
- Coupling mechanism for multiple carts

```csharp
public class Minecart : RailVehicle {
    
    [Export] public float MaxSpeed { get; set; } = 8.0f;
    [Export] public float RailFriction { get; set; } = 0.02f;
    
    private RailPath _currentRail;
    private float _progressOnRail;
    private float _currentSpeed;
    
    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);
        
        if (_currentRail != null) {
            MoveAlongRail((float)delta);
        }
        
        // Update mass with cargo
        Mass = EmptyWeight + CurrentLoadWeight;
    }
    
    private void MoveAlongRail(float delta) {
        float curveLength = _currentRail.Curve.GetBakedLength();
        
        // Apply slope physics
        Vector3 tangent = GetRailTangent();
        float slope = -tangent.Y;
        
        // Gravity assist or resistance
        float gravityForce = slope * 9.8f * Mass;
        float frictionForce = RailFriction * Mass * 9.8f;
        
        float netForce = gravityForce - frictionForce;
        float acceleration = netForce / Mass;
        
        _currentSpeed += acceleration * delta;
        _currentSpeed = Mathf.Clamp(_currentSpeed, -MaxSpeed, MaxSpeed);
        
        // Move
        _progressOnRail += (_currentSpeed * delta) / curveLength;
        UpdatePositionOnRail();
    }
}
```

### 6.5 Wagon

**Specifications**:
- Capacity: 500 kg (cargo) + 2 passengers
- Empty Weight: 120 kg
- Four wheels for stability
- Requires animal or 2+ people to move
- Hitch system for draft animals

**Hitching System**:
```csharp
public class HitchSystem {
    
    public bool IsHitched { get; private set; }
    public Entity Hitcher { get; private set; }
    public float PullForce { get; private set; }
    
    public void Hitch(Entity entity) {
        Hitcher = entity;
        IsHitched = true;
        PullForce = entity switch {
            Player player => player.Strength * 10f,
            Animal animal => animal.PullStrength,
            _ => 0f
        };
    }
    
    public void Unhitch() {
        Hitcher = null;
        IsHitched = false;
        PullForce = 0f;
    }
}
```

### 6.6 Vehicle Capacity Table

| Vehicle | Max Capacity | Optimal Load | Speed (Empty) | Speed (Loaded) | Stamina Cost |
|---------|--------------|--------------|---------------|----------------|--------------|
| **Hand Cart** | 200 kg | 150 kg | 3.0 m/s | 2.0 m/s | 5/sec |
| **Wheelbarrow** | 300 kg | 225 kg | 2.8 m/s | 1.8 m/s | 6/sec |
| **Minecart** | 1,000 kg | 750 kg | 8.0 m/s | 6.0 m/s | 0 (gravity) |
| **Wagon** | 500 kg | 375 kg | 2.5 m/s | 1.5 m/s | 8/sec |
| **Hand Truck** | 150 kg | 100 kg | 3.2 m/s | 2.2 m/s | 4/sec |
| **Small Boat** | 400 kg | 300 kg | 2.0 m/s | 1.2 m/s | 3/sec |
| **Cargo Boat** | 2,000 kg | 1,500 kg | 3.0 m/s | 1.5 m/s | 0 (wind/motor) |
| **Truck** | 10,000 kg | 8,000 kg | 15 m/s | 12 m/s | Fuel |

---

## 7. Storage Systems

### 7.1 Container Capacity Table

| Container | Capacity | Slot Count | Accessibility | Security |
|-----------|----------|------------|---------------|----------|
| **Crate** | 100 kg | 18 slots | Open top | None |
| **Chest** | 200 kg | 27 slots | Lid access | Lockable |
| **Barrel** | 150 kg | 12 slots | Open top | Lid seal |
| **Warehouse** | 2,000 kg | 300 slots | Wide door | Lockable |
| **Safe** | 100 kg | 10 slots | Small door | High security |
| **Stockpile** | 5,000 kg | Bulk slots | Open | None |
| **Shipping Container** | 10,000 kg | 100 slots | Double doors | Lockable |

### 7.2 Container Properties

```csharp
public class StorageContainer : VoxelEntity {
    
    [Export] public float MaxCapacity { get; set; }
    [Export] public int MaxSlots { get; set; }
    [Export] public ContainerType Type { get; set; }
    [Export] public bool IsLockable { get; set; }
    
    private Inventory _inventory;
    private float _currentWeight;
    
    public override void _Ready() {
        base._Ready();
        
        _inventory = new Inventory {
            MaxSlots = MaxSlots,
            MaxWeight = MaxCapacity
        };
        
        // Update entity mass
        UpdateMass();
    }
    
    public bool TryStoreItem(Item item, int quantity) {
        float itemWeight = item.WeightPerUnit * quantity;
        
        if (_currentWeight + itemWeight > MaxCapacity) {
            ShowMessage($"Container full! Needs {itemWeight:F1}kg, has {MaxCapacity - _currentWeight:F1}kg free");
            return false;
        }
        
        if (_inventory.TryAddItem(item, quantity)) {
            _currentWeight += itemWeight;
            UpdateMass();
            return true;
        }
        
        return false;
    }
    
    private void UpdateMass() {
        Mass = BaseMass + _currentWeight;
    }
}

public enum ContainerType {
    Crate,      // Light, portable, no lock
    Chest,      // Medium, lockable
    Barrel,     // Round, rolls, liquid capable
    Warehouse,  // Large, static
    Safe,       // Small, high security
    Stockpile,  // Huge, bulk storage
    Shipping    // Large, transportable
}
```

### 7.3 Stockpile Mechanics

Stockpiles are special bulk storage for large quantities:

```csharp
public class Stockpile : StorageContainer {
    
    [Export] public Vector3I Size { get; set; } = new Vector3I(4, 2, 4); // 4×2×4 blocks
    
    private Dictionary<BlockType, int> _storedBlocks;
    
    public float TotalCapacity => Size.X * Size.Y * Size.Z * 2500f; // Stone equivalent
    
    public void StoreBlock(BlockType type, int count) {
        float weight = BlockWeightDatabase.GetWeight(type) * count;
        
        if (CanStore(weight)) {
            _storedBlocks[type] = _storedBlocks.GetValueOrDefault(type) + count;
            _currentWeight += weight;
            UpdateVisuals();
        }
    }
    
    private void UpdateVisuals() {
        // Create piled visual representation
        float fillPercent = _currentWeight / TotalCapacity;
        int visibleLayers = Mathf.CeilToInt(fillPercent * Size.Y);
        
        // Update mesh to show material piles
        for (int y = 0; y < visibleLayers; y++) {
            UpdateLayerMesh(y);
        }
    }
}
```

### 7.4 Warehouse Bulk Storage

For large-scale storage of multiple container types:

- **Racking System**: Increases vertical storage
- **Organization**: Labeling and categorization
- **Access**: Requires ladders for high shelves
- **Loading**: Designed for cart/boat access

---

## 8. UI/Feedback System

### 8.1 HUD Elements

```csharp
public class WeightCarryingUI : Control {
    
    private ProgressBar _weightBar;
    private Label _weightText;
    private Label _encumbranceLabel;
    private Panel _warningPanel;
    
    public void UpdateDisplay(CarryingCapacity capacity) {
        float percent = capacity.EncumbrancePercent;
        var tier = EncumbranceCalculator.GetTier(percent);
        var color = EncumbranceCalculator.GetTierColor(tier);
        
        // Update bar
        _weightBar.Value = percent;
        _weightBar.Modulate = color;
        
        // Update text
        _weightText.Text = $"{capacity.CurrentWeight:F1} / {capacity.CurrentCapacity:F0} kg";
        
        // Update encumbrance indicator
        _encumbranceLabel.Text = tier.ToString().ToUpper();
        _encumbranceLabel.Modulate = color;
        
        // Show warnings
        if (tier == EncumbranceTier.Overloaded || tier == EncumbranceTier.Immobilized) {
            _warningPanel.Visible = true;
            _warningPanel.Modulate = color;
        } else {
            _warningPanel.Visible = false;
        }
    }
}
```

### 8.2 UI Layout

```
[WEIGHT DISPLAY - Bottom Right Corner]

┌─────────────────────────┐
│  CARRYING               │
│  ━━━━━━━━━━━━━━▓▓▓▓▓░   │  <- Progress bar (filled portion shows load)
│  85.5 / 100 kg          │  <- Exact numbers
│  HEAVY                  │  <- Encumbrance tier (color coded)
│                         │
│  ⚠ Cannot sprint        │  <- Active restrictions (only when applicable)
│  ⛔ Cannot jump          │
└─────────────────────────┘

Colors:
- Light (0-30%): Green
- Medium (30-60%): Yellow  
- Heavy (60-90%): Orange
- Overloaded (90-100%): Red
- Immobilized (>100%): Flashing Red
```

### 8.3 Inventory Weight Display

```csharp
public class InventoryWeightPanel : Panel {
    
    public void UpdateItemWeights(List<Item> items) {
        foreach (var item in items) {
            var weightLabel = GetLabelForItem(item);
            weightLabel.Text = $"{item.Weight:F1}kg";
            
            // Color code heavy items
            if (item.Weight > 500f) {
                weightLabel.Modulate = Colors.Red;
            } else if (item.Weight > 100f) {
                weightLabel.Modulate = Colors.Orange;
            } else {
                weightLabel.Modulate = Colors.White;
            }
        }
    }
}
```

### 8.4 Warning System

```csharp
public class CarryingWarningSystem : Node {
    
    private CarryingCapacity _capacity;
    private float _lastWarningTime;
    private const float WARNING_COOLDOWN = 5.0f;
    
    public void CheckAndWarn(float encumbrancePercent) {
        float currentTime = Time.GetTimeDictFromSystem()["second"];
        
        if (currentTime - _lastWarningTime < WARNING_COOLDOWN) {
            return;
        }
        
        string warning = encumbrancePercent switch {
            > 100f => "CRITICAL: DROP ITEMS TO MOVE!",
            > 95f => "WARNING: Nearly overloaded!",
            > 80f => "Heavy load - stamina draining faster",
            > 60f => "Load is getting heavy",
            _ => null
        };
        
        if (warning != null) {
            ShowWarning(warning);
            _lastWarningTime = currentTime;
        }
    }
    
    private void ShowWarning(string message) {
        // Show in chat
        ChatSystem.SendSystemMessage(message);
        
        // Show on screen
        NotificationManager.Show(message, 3.0f);
        
        // Play sound
        AudioManager.PlayWarningSound();
    }
}
```

### 8.5 Container Capacity Visualization

When interacting with containers:

```
[CHEST INTERFACE]

┌──────────────────────────────────┐
│ CHEST - 45.2 / 200 kg (22%)      │  <- Capacity header
│ Light Load                       │  <- Tier indicator
├──────────────────────────────────┤
│ [Slot 1] Stone ×5     12.5 kg    │  <- Individual item weights
│ [Slot 2] Iron Ore ×8  28.0 kg    │
│ [Slot 3] Wood ×20     14.0 kg    │
│ ...                              │
├──────────────────────────────────┤
│ Can store: 154.8 kg more         │  <- Remaining capacity
└──────────────────────────────────┘
```

---

## 9. Gameplay Impact

### 9.1 Local Processing Incentive

The weight system creates natural incentives for **local resource processing**:

**Example - Iron Production Chain**:

| Stage | Material | Weight | Per Unit |
|-------|----------|--------|----------|
| Raw Ore | Iron Ore | 3,500 kg | 1 block |
| Smelted | Iron Ingot | 7,850 kg | But 1/5th the volume |
| Refined | Steel Ingot | 7,850 kg | Higher value, same weight |
| Crafted | Iron Tools | 50-200 kg | Usable items |

**Strategic Decision**:
- Transporting 10 ore blocks: 35,000 kg (requires 5 trips or vehicle)
- Smelt on-site to 10 ingots: 7,850 kg (1 trip with backpack)
- **Conclusion**: Build furnaces near mines, not in cities

### 9.2 Infrastructure Importance

Weight creates demand for transportation infrastructure:

```
SETTLEMENT LOCATION ANALYSIS

Scenario A: Mountain Settlement
- Iron mine: 500m away at 50m elevation
- Moving 1,000 kg ore:
  * By hand: 28 trips, 30 minutes, 5,000 stamina
  * With cart: 6 trips, 8 minutes, 1,200 stamina
  * With minecart: 2 trips, 3 minutes, 200 stamina

Infrastructure Decision:
✓ Build rail system (upfront cost: 500 kg materials)
✓ ROI: 5 trips saved (2,500 stamina, 25 minutes)
```

### 9.3 Settlement Location Strategy

**Weight influences settlement planning**:

| Factor | Impact on Location |
|--------|-------------------|
| **Resource Proximity** | Heavy materials favor close extraction |
| **Transportation** | Flat terrain > uphill for carts |
| **Water Access** | Boats have highest capacity |
| **Processing Sites** | Refine heavy materials near source |
| **Trade Routes** | Light, valuable goods can travel far |

**Settlement Archetypes**:

1. **Mining Town** (Processing Hub)
   - Location: Adjacent to ore deposits
   - Infrastructure: Furnaces, smithies, rail to city
   - Exports: Refined metals (1/5th the weight of ore)

2. **Agricultural Village** (Raw Production)
   - Location: Flat plains with water
   - Infrastructure: Farms, storage, roads
   - Exports: Food, light materials

3. **Trade Port** (Logistics Hub)
   - Location: Harbor with flat terrain
   - Infrastructure: Warehouses, docks, weighing stations
   - Role: Redistribution center

4. **Industrial City** (Manufacturing)
   - Location: Transport junction
   - Infrastructure: Workshops, assembly, transport
   - Imports: Refined materials
   - Exports: Finished goods (low weight, high value)

### 9.4 Economic Emergence

Weight creates **emergent economic roles**:

| Role | Activity | Equipment | Value Proposition |
|------|----------|-----------|-------------------|
| **Miner** | Extract ore | Pickaxe, backpack | Raw material source |
| **Smelter** | Refine ore | Furnace, fuel | Weight reduction (5×) |
| **Hauler** | Transport | Cart, wagon | Saves others time/stamina |
| **Builder** | Construction | Tools | Assembles structures |
| **Merchant** | Trade | Storage, contracts | Finds buyers, routes goods |

**Transport Cost Calculation**:
```
Cost per km = (Stamina Cost + Time Value + Wear Risk) / Capacity

Example: Moving 1,000 kg 5km
- By hand (10 trips): 500 stamina × 10 = 5,000 stamina
- By cart (1 trip): 200 stamina + cart wear
- Hauler fee: 100 credits per 100 kg per km
  = 1,000/100 × 5km × 100cr = 5,000 credits

Decision Point: Is your time worth 5,000 credits?
```

### 9.5 Multiplayer Cooperation

Weight encourages **collaborative gameplay**:

**Team Mining Operation**:
```
6-Person Iron Mining Team

2 Miners (extract ore):
- Output: 20 blocks/hour (70,000 kg)
- Role: Requires tools, safety

1 Smelter (on-site):
- Processes ore to ingots
- Reduces weight to 14,000 kg

2 Haulers (transport to city):
- Round trip: 30 minutes
- Capacity: 1,000 kg per trip
- Output: 14 trips/hour

1 Manager (coordinates, sells):
- Tracks inventory, sets prices

Efficiency: Without weight system, 1 person does everything.
With weight system, 6-person team is 10× more efficient.
```

---

## 10. Technical Implementation

### 10.1 Core System Architecture

```csharp
namespace Societies.Carrying {
    
    /// <summary>
    /// Central manager for all weight-related systems
    /// </summary>
    public class WeightCarryingManager : Node {
        
        public static WeightCarryingManager Instance { get; private set; }
        
        // Component systems
        public BlockWeightDatabase BlockDatabase { get; private set; }
        public EncumbranceCalculator EncumbranceSystem { get; private set; }
        public VehicleManager TransportSystem { get; private set; }
        
        // Events
        public delegate void WeightChangedHandler(Entity entity, float newWeight, float capacity);
        public static event WeightChangedHandler OnWeightChanged;
        
        public override void _EnterTree() {
            if (Instance == null) {
                Instance = this;
            }
        }
        
        public override void _Ready() {
            BlockDatabase = new BlockWeightDatabase();
            EncumbranceSystem = new EncumbranceCalculator();
            TransportSystem = GetNode<VehicleManager>("VehicleManager");
        }
        
        /// <summary>
        /// Register an entity with the carrying system
        /// </summary>
        public CarryingCapacity RegisterEntity(Entity entity) {
            var capacity = new CarryingCapacity(entity);
            entity.SetMeta("carrying_capacity", capacity);
            return capacity;
        }
        
        /// <summary>
        /// Update all active entities (server-side optimization)
        /// </summary>
        public void UpdateAllEntities(float deltaTime) {
            foreach (var entity in GetTree().GetNodesInGroup("carrying_entities")) {
                if (entity is Entity e) {
                    UpdateEntity(e, deltaTime);
                }
            }
        }
        
        private void UpdateEntity(Entity entity, float deltaTime) {
            var capacity = entity.GetCarryingCapacity();
            if (capacity == null) return;
            
            // Recalculate if inventory changed
            if (entity.Inventory.HasChanged) {
                capacity.RecalculateWeight();
            }
            
            // Apply movement effects
            ApplyMovementModifiers(entity, capacity);
            
            // Check warnings
            if (entity.IsPlayer) {
                CheckPlayerWarnings(entity, capacity);
            }
            
            // Emit event
            OnWeightChanged?.Invoke(entity, capacity.CurrentWeight, capacity.CurrentCapacity);
        }
        
        private void ApplyMovementModifiers(Entity entity, CarryingCapacity capacity) {
            float encumbrance = capacity.EncumbrancePercent;
            
            // Update movement component
            if (entity.HasNode("MovementController")) {
                var movement = entity.GetNode<MovementController>("MovementController");
                movement.SetEncumbranceMultiplier(
                    EncumbranceCalculator.CalculateSpeedMultiplier(encumbrance)
                );
            }
            
            // Update stamina component
            if (entity.HasNode("StaminaSystem")) {
                var stamina = entity.GetNode<StaminaSystem>("StaminaSystem");
                stamina.SetDrainMultiplier(
                    EncumbranceCalculator.CalculateStaminaMultiplier(encumbrance)
                );
            }
        }
        
        private void CheckPlayerWarnings(Entity player, CarryingCapacity capacity) {
            if (capacity.EncumbrancePercent > 90f) {
                player.ShowWarning($"OVERLOADED: {capacity.CurrentWeight:F1}kg / {capacity.CurrentCapacity:F0}kg");
            }
        }
    }
}
```

### 10.2 Network Synchronization

Weight must sync across multiplayer:

```csharp
public class CarryingNetworkSync : Node {
    
    [Export] public Entity Entity { get; set; }
    
    // Sync frequency based on state
    private const float FULL_SYNC_RATE = 1.0f;  // 1 Hz for full updates
    private const float FAST_SYNC_RATE = 10.0f; // 10 Hz when overloaded
    
    private float _timeSinceLastSync;
    private float _lastWeight;
    
    public override void _Process(double delta) {
        _timeSinceLastSync += (float)delta;
        
        var capacity = Entity.GetCarryingCapacity();
        if (capacity == null) return;
        
        // Determine sync rate based on state
        float syncRate = capacity.EncumbrancePercent > 90f ? FAST_SYNC_RATE : FULL_SYNC_RATE;
        float syncInterval = 1.0f / syncRate;
        
        // Sync if interval passed or weight changed significantly
        if (_timeSinceLastSync >= syncInterval || 
            Mathf.Abs(capacity.CurrentWeight - _lastWeight) > 5.0f) {
            
            Rpc(nameof(SyncCarryingState), capacity.CurrentWeight, capacity.CurrentCapacity);
            _lastWeight = capacity.CurrentWeight;
            _timeSinceLastSync = 0f;
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.Authority)]
    public void SyncCarryingState(float currentWeight, float capacity) {
        if (!Multiplayer.IsServer()) {
            var carrying = Entity.GetCarryingCapacity();
            carrying?.SetWeight(currentWeight);
        }
    }
}
```

### 10.3 Weight Calculation Optimizations

```csharp
public class OptimizedWeightCalculator {
    
    // Cache for frequently accessed weights
    private static Dictionary<BlockType, float> _weightCache;
    private const int MAX_CACHE_SIZE = 1000;
    
    public static float GetWeightFast(BlockType type) {
        // Fast lookup from cache
        if (_weightCache.TryGetValue(type, out float weight)) {
            return weight;
        }
        
        // Compute and cache
        weight = BlockWeightDatabase.GetWeight(type);
        
        if (_weightCache.Count < MAX_CACHE_SIZE) {
            _weightCache[type] = weight;
        }
        
        return weight;
    }
    
    /// <summary>
    /// Batch calculate weight for inventory
    /// </summary>
    public static float CalculateInventoryWeight(List<ItemStack> items) {
        float total = 0;
        
        // Parallel processing for large inventories
        if (items.Count > 100) {
            object lockObj = new object();
            
            Parallel.ForEach(items, item => {
                float weight = GetItemWeight(item);
                lock (lockObj) {
                    total += weight;
                }
            });
        } else {
            foreach (var item in items) {
                total += GetItemWeight(item);
            }
        }
        
        return total;
    }
    
    private static float GetItemWeight(ItemStack stack) {
        return stack.Item.WeightPerUnit * stack.Quantity;
    }
}
```

### 10.4 Integration with Physics

```csharp
public class WeightPhysicsIntegration : Node {
    
    private RigidBody3D _body;
    private CarryingCapacity _capacity;
    
    public override void _Ready() {
        _body = GetParent<RigidBody3D>();
        _capacity = _body.GetCarryingCapacity();
        
        // Subscribe to weight changes
        WeightCarryingManager.OnWeightChanged += OnWeightChanged;
    }
    
    private void OnWeightChanged(Entity entity, float newWeight, float capacity) {
        if (entity != _body) return;
        
        // Update physics mass
        _body.Mass = _body.BaseMass + newWeight;
        
        // Adjust center of mass based on load distribution
        UpdateCenterOfMass();
    }
    
    private void UpdateCenterOfMass() {
        // Lower COM for stability with heavy loads
        float loadRatio = _capacity.EncumbrancePercent / 100f;
        float comOffset = Mathf.Lerp(0.3f, 0.1f, loadRatio);
        
        _body.CenterOfMass = new Vector3(0, comOffset, 0);
    }
}
```

### 10.5 Persistence

Save/Load carrying capacity:

```csharp
public class CarryingPersistence {
    
    public Dictionary<string, object> Save(Entity entity) {
        var capacity = entity.GetCarryingCapacity();
        
        return new Dictionary<string, object> {
            ["current_weight"] = capacity.CurrentWeight,
            ["max_capacity"] = capacity.CurrentCapacity,
            ["strength_level"] = entity.Skills.GetLevel(SkillType.Strength),
            ["equipment"] = SaveEquipmentModifiers(entity)
        };
    }
    
    public void Load(Entity entity, Dictionary<string, object> data) {
        var capacity = entity.GetCarryingCapacity();
        
        capacity.SetWeight((float)data["current_weight"]);
        
        // Restore equipment
        LoadEquipmentModifiers(entity, (List<string>)data["equipment"]);
        
        // Recalculate base capacity (strength might have changed)
        capacity.RecalculateCapacity();
    }
}
```

---

## 11. Balance Considerations

### 11.1 Progression Curve

| Stage | Capacity | Typical Load | Access | Game Time |
|-------|----------|--------------|--------|-----------|
| **Survival** | 100 kg | 60-80 kg | Basic | 0-2 hours |
| **Establishment** | 150 kg | 100-130 kg | Backpack | 2-10 hours |
| **Development** | 200 kg | 150-180 kg | Hand Cart | 10-30 hours |
| **Industrial** | 300+ kg | 200-280 kg | Vehicles | 30+ hours |

### 11.2 Difficulty Settings

| Setting | Player Base | AI Base | Stamina Impact | Vehicle Mult |
|---------|-------------|---------|----------------|--------------|
| **Casual** | 150 kg | 100 kg | 50% | 2× |
| **Normal** | 100 kg | 80 kg | 100% | 1× |
| **Hardcore** | 80 kg | 60 kg | 150% | 0.75× |
| **Realistic** | 50 kg | 40 kg | 200% | 0.5× |

### 11.3 Anti-Frustration Measures

- **Auto-drop**: When immobilized, offer to auto-drop heaviest item
- **Temporary buffs**: Potions/food can temporarily boost capacity
- **Rest stops**: Sitting restores stamina 2× faster
- **Group carrying**: Two players can carry 1 heavy block together

---

## 12. Future Extensions

### 12.1 Post-MVP Features

1. **Animal Transport**: Horses, donkeys, llamas with saddlebags
2. **Conveyor Systems**: Automated belt transport for factories
3. **Cranes**: Vertical lifting for construction
4. **Air Transport**: Hot air balloons for light cargo
5. **Underground Pipes**: Automated fluid/small item transport

### 12.2 Advanced Mechanics

- **Packaging**: Items can be packed into lighter crates
- **Material Science**: Alloys with better strength-to-weight
- **Exoskeletons**: Mechanical assist suits
- **Teleporters**: Late-game instant transport (high energy cost)

---

## 13. Summary

### Key Metrics

- **70+ Block Types** with realistic kg/m³ weights
- **100 kg Base Capacity** for players
- **4 Encumbrance Tiers** affecting movement
- **6 Vehicle Types** for transport
- **5× Weight Reduction** through on-site processing

### Design Wins

1. **Creates meaningful logistics decisions**
2. **Encourages infrastructure investment**
3. **Enables emergent economic roles**
4. **Rewards cooperative gameplay**
5. **Grounds game in physical reality**

### Critical Success Factors

- Clear UI feedback prevents confusion
- Gradual introduction through equipment upgrades
- Multiple solutions (vehicles, teamwork, processing)
- Skill progression provides satisfying growth

---

**Document End**
