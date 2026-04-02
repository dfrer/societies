# Session 4: Progression & Balance - World Resources Specification

**Planning Session**: 4 of 7  
**Status**: Content Ready  
**Date Created**: 2026-02-01  
**Document**: 03-world-resources.md

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Surface Resources](#2-surface-resources)
3. [Subsurface Resources](#3-subsurface-resources)
4. [Mineral Resources (Geological Strata)](#4-mineral-resources-geological-strata)
5. [Resource Regeneration](#5-resource-regeneration)
6. [Gathering Mechanics](#6-gathering-mechanics)
7. [Resource Processing](#7-resource-processing)
8. [Economic Impact](#8-economic-impact)
9. [Environmental Impact](#9-environmental-impact)
10. [Progression Balance](#10-progression-balance)

---

## 1. Executive Summary

### Resource Philosophy

Societies' resource system is built on three core principles:

1. **Depth-Based Progression**: Resources are stratified by depth, creating natural tech-level gates. Surface resources support primitive survival; deep resources enable advanced civilization.

2. **Abundance with Constraints**: Common resources (wood, stone) are plentiful but extraction is limited by carrying capacity and tool durability. Rare resources are scarce but not artificially gated—players must search, trade, or dig deeper.

3. **Renewable vs. Finite**: Clear distinction between renewable resources (biological) and finite resources (minerals). This creates strategic tension between sustainable harvesting and resource extraction.

### Scarcity and Abundance Balance

| Resource Category | Abundance | Rarity Driver | Strategic Value |
|-------------------|-----------|---------------|-----------------|
| **Surface Basic** | Plentiful | Carrying capacity, tool durability | Early survival, construction |
| **Surface Rare** | Scarce | Distance from spawn, biome locks | Trade goods, specialized crafting |
| **Shallow Ores** | Moderate | Depth requirement (10-50m) | Bronze/Copper Age progression |
| **Deep Ores** | Limited | Depth + vein distribution | Iron Age, industrialization |
| **Very Deep** | Very Rare | Depth + rarity + extraction cost | Late-game tech, space age |

### Progression Through Resource Tiers

**Tier 0 - Surface Scavenging (Days 1-3)**:
- Wood, stone, plants available without tools
- Basic survival: shelter, fire, simple tools
- No mining required

**Tier 1 - Shallow Extraction (Days 4-10)**:
- Hand mining to 10m depth
- Surface ore deposits (copper, coal)
- Copper tools enable moderate efficiency

**Tier 2 - Deep Mining (Days 10-25)**:
- Systematic mining to 50-100m
- Iron ore primary target
- Bronze/Iron tools required

**Tier 3 - Industrial Extraction (Days 25-60)**:
- Mechanized mining to 150m+
- Precious metals, rare earths
- Power requirements, infrastructure

**Tier 4 - Advanced Recovery (Days 60+)**:
- Very deep mining (200m near bedrock)
- Space-age materials
- Recycling, off-world resources

---

## 2. Surface Resources

### 2.1 Wood/Trees (Renewable)

**Tree Types by Biome**:

| Tree Type | Biome | Growth Time | Yield (logs) | Properties |
|-----------|-------|-------------|--------------|------------|
| **Oak** | Boreal | 3 days | 4-6 logs | Balanced, versatile |
| **Pine** | Boreal (alpine) | 4 days | 5-8 logs | Softwood, fast growing |
| **Spruce** | Boreal (taiga) | 4 days | 6-10 logs | Dense, construction grade |
| **Hardwood** | Jungle | 6 days | 3-5 logs | Dense, premium quality |
| **Birch** | Boreal (riverine) | 3 days | 3-4 logs | Decorative, paper source |

**Renewable Mechanics**:

```csharp
public class TreeRenewableSystem
{
    // Tree lifecycle stages
    public enum TreeStage
    {
        Sapling,      // Day 0-1: vulnerable, slow growth
        Young,        // Day 1-2: established, moderate growth
        Mature,       // Day 2-3: full yield when harvested
        Old,          // Day 3-5: max yield, chance of death
        Dead          // Day 5+: falls, becomes rotten wood
    }
    
    // Growth requirements
    public bool CanGrow(BlockPosition pos, TreeType type)
    {
        // Light requirement: 8+ sky light level
        if (GetSkyLight(pos) < 8) return false;
        
        // Space requirement: 3x3 column clear above sapling
        for (int y = 1; y <= type.MaxHeight; y++)
        {
            if (!IsAir(pos.X, pos.Y + y, pos.Z)) return false;
        }
        
        // Soil requirement
        var soilBlock = GetBlock(pos.X, pos.Y - 1, pos.Z);
        return IsValidSoil(soilBlock, type);
    }
    
    // Natural regeneration (sapling spawning)
    public void TrySpawnSapling(Chunk chunk, BiomeType biome)
    {
        // Only in unclaimed, naturally generated areas
        if (chunk.IsPlayerModified) return;
        
        // Chance based on biome tree density
        float spawnChance = GetBiomeTreeDensity(biome) * 0.01f; // 0.5-2% per tick
        
        if (Random.value < spawnChance)
        {
            var pos = GetRandomValidPosition(chunk);
            if (CanGrow(pos, GetRandomTreeType(biome)))
            {
                PlaceBlock(pos, BlockType.Sapling);
            }
        }
    }
}
```

**Yield Calculation**:

| Tool | Efficiency Modifier | Yield Multiplier | Durability Cost |
|------|---------------------|------------------|-----------------|
| Hand | 0.5x | 50% wood loss | N/A (slow) |
| Stone Axe | 1.0x | 100% yield | 1 per tree |
| Copper Axe | 1.2x | 120% yield | 1 per 2 trees |
| Iron Axe | 1.5x | 150% yield | 1 per 3 trees |
| Steel Axe | 2.0x | 200% yield | 1 per 5 trees |

### 2.2 Plants and Crops

**Wild Plant Resources**:

| Plant | Biome | Yield | Regrowth | Use |
|-------|-------|-------|----------|-----|
| **Tall Grass** | All | 1 fiber | 1 day | Rope, thatch |
| **Ferns** | Boreal | 2 fiber | 2 days | Bedding, compost |
| **Reeds** | Wetlands | 3 fiber | 1 day | Basket weaving, roofing |
| **Berry Bushes** | Boreal | 5-10 berries | 3 days | Food, dye |
| **Mushrooms** | Forest (dark) | 1-3 mushrooms | 5 days | Food, medicine |
| **Cacti** | Desert | 2-4 segments | 7 days | Water, building |
| **Flowers** | All | 1-2 flowers | 4 days | Dye, decoration |

**Cultivated Crops**:

| Crop | Growth Stages | Time to Mature | Yield | Biome Preference |
|------|---------------|----------------|-------|------------------|
| **Wheat** | 8 stages | 5 days | 3-5 grain + seeds | Boreal, moderate |
| **Vegetables** | 6 stages | 4 days | 2-4 vegetables | All |
| **Cotton** | 7 stages | 6 days | 2-3 cotton bolls | Warm |
| **Sugarcane** | 5 stages | 7 days | 4-6 canes | Hot, wet |
| **Herbs** | 4 stages | 3 days | 3-5 bundles | Moderate |

**Crop Growth Mechanics**:

```csharp
public class CropGrowthSystem
{
    public void UpdateCropGrowth(BlockPosition pos, CropType crop)
    {
        var cropBlock = GetBlock(pos);
        int currentStage = GetGrowthStage(cropBlock);
        
        // Base growth chance per tick
        float growthChance = 0.01f; // 1% per tick = ~10min per stage
        
        // Light modifier (optimal: 10-14 light level)
        float lightLevel = GetSkyLight(pos);
        float lightModifier = lightLevel < 8 ? 0.0f : 
                              lightLevel > 14 ? 0.5f : 1.0f;
        
        // Water modifier (hydrated soil within 4 blocks)
        bool isHydrated = IsWithinWaterRange(pos, radius: 4);
        float waterModifier = isHydrated ? 2.0f : 0.3f;
        
        // Biome temperature modifier
        float temp = GetTemperature(pos);
        float tempModifier = GetTemperatureGrowthModifier(crop, temp);
        
        // Final growth calculation
        float finalChance = growthChance * lightModifier * waterModifier * tempModifier;
        
        if (Random.value < finalChance)
        {
            AdvanceGrowthStage(pos, crop, currentStage + 1);
        }
    }
}
```

### 2.3 Surface Stone Deposits

**Surface Rock Formations**:

| Deposit Type | Biome | Size | Yield | Tool Required |
|--------------|-------|------|-------|---------------|
| **Boulders** | All | 3-8 blocks | 5-15 stone | None (slow) |
| **Rock Outcrops** | Mountains | 10-30 blocks | 20-50 stone | Pickaxe recommended |
| **Surface Ore** | All (rare) | 2-5 blocks | 2-8 ore | Pickaxe required |
| **Gravel Beds** | Riverbeds | 5-20 blocks | 5-15 gravel | Shovel recommended |

**Boulder Regeneration**:
- Extremely slow: 1% chance per day to spawn new boulder in valid location
- Requires: unclaimed land, rocky terrain, 10+ blocks from existing boulders
- Creates incentive for territory expansion

### 2.4 Wildlife Resources

**Hunting & Gathering**:

| Resource Source | Biome | Yield | Renewable | Method |
|-----------------|-------|-------|-----------|--------|
| **Small Game** | All | 2-5 meat, 1-2 hide | Yes (spawning) | Hunting, traps |
| **Deer** | Boreal | 8-15 meat, 3-5 hide | Yes (spawning) | Hunting |
| **Fish** | Water | 1-3 fish | Yes (spawning) | Fishing |
| **Birds** | All | 1-2 meat, 1-3 feathers | Yes (spawning) | Hunting, nets |
| **Insects** | All | 1 protein | Yes (abundant) | Gathering |
| **Eggs** | All | 1-3 eggs | Yes (nesting) | Gathering |

**Wildlife Population Dynamics**:

```csharp
public class WildlifePopulationSystem
{
    // Population cap per chunk based on biome carrying capacity
    public int GetPopulationCap(BiomeType biome, ResourceType animal)
    {
        return biome switch
        {
            BiomeType.Boreal => animal == ResourceType.Deer ? 3 : 8,
            BiomeType.Desert => animal == ResourceType.SmallGame ? 5 : 2,
            BiomeType.Jungle => animal == ResourceType.SmallGame ? 10 : 5,
            _ => 5
        };
    }
    
    // Spawning algorithm
    public void TrySpawnWildlife(Chunk chunk)
    {
        foreach (var animalType in GetValidAnimals(chunk.Biome))
        {
            int currentCount = CountAnimalsInChunk(chunk, animalType);
            int cap = GetPopulationCap(chunk.Biome, animalType);
            
            if (currentCount < cap && Random.value < 0.001f) // 0.1% per tick
            {
                var spawnPos = FindValidSpawnLocation(chunk, animalType);
                if (spawnPos.HasValue)
                {
                    SpawnAnimal(animalType, spawnPos.Value);
                }
            }
        }
    }
    
    // Reproduction (animals in pairs)
    public void UpdateReproduction()
    {
        var matingPairs = FindMatingPairs();
        foreach (var pair in matingPairs)
        {
            if (Random.value < 0.1f && CountAnimalsInArea(pair.Location, radius: 50) < GetLocalCap(pair.Type))
            {
                SpawnOffspring(pair.Type, pair.Location);
            }
        }
    }
}
```

---

## 3. Subsurface Resources

### 3.1 Soil Layers and Quality

**Soil Stratification**:

| Depth | Layer Name | Block Type | Properties | Agriculture |
|-------|-----------|------------|------------|-------------|
| 0 to -1m | Topsoil | Grass/Rich Soil | High fertility | Excellent |
| -1 to -3m | Subsoil | Dirt | Moderate fertility | Good |
| -3 to -5m | Deep Soil | Dirt/Clay mix | Low fertility | Poor |
| -5 to -10m | Weathered Rock | Gravel/Stone | No fertility | None |

**Soil Quality System**:

```csharp
public class SoilQuality
{
    public float Fertility { get; set; }      // 0.0 to 1.0
    public float MoistureRetention { get; set; } // 0.0 to 1.0
    public float Drainage { get; set; }       // 0.0 to 1.0
    public float Compaction { get; set; }     // 0.0 to 1.0 (higher = worse)
    public float Contamination { get; set; }  // 0.0 to 1.0 (pollution)
    
    // Derived crop growth multiplier
    public float GetGrowthMultiplier()
    {
        if (Contamination > 0.5f) return 0.0f; // Too polluted
        
        float baseMultiplier = Fertility * 0.4f + 
                              MoistureRetention * 0.3f + 
                              Drainage * 0.2f - 
                              Compaction * 0.3f;
        
        return Mathf.Clamp(baseMultiplier * (1 - Contamination), 0.1f, 2.0f);
    }
}

public class SoilDegradationSystem
{
    // Farming degrades soil over time
    public void ApplyFarmingImpact(BlockPosition pos, CropType crop)
    {
        var soil = GetSoilQuality(pos);
        
        // Nutrient depletion
        soil.Fertility -= 0.05f;
        
        // Compaction from walking/harvesting
        soil.Compaction += 0.02f;
        
        // Recovery during fallow periods
        if (IsFallow(pos))
        {
            soil.Fertility += 0.01f; // Slow natural recovery
            soil.Compaction -= 0.005f;
        }
    }
    
    // Fertilizer can restore fertility
    public void ApplyFertilizer(BlockPosition pos, FertilizerType type)
    {
        var soil = GetSoilQuality(pos);
        soil.Fertility = Mathf.Min(1.0f, soil.Fertility + type.FertilityBoost);
        
        // Some fertilizers have side effects
        if (type == FertilizerType.Chemical)
        {
            soil.Contamination += 0.1f;
        }
    }
}
```

### 3.2 Clay and Sand Deposits

**Sedimentary Resource Distribution**:

| Resource | Depth Range | Biome Bias | Deposit Size | Frequency |
|----------|-------------|------------|--------------|-----------|
| **Clay** | -1 to -10m | Wet biomes +50% | 10-50 blocks | Moderate |
| **Sand** | -1 to -5m | Deserts +100% | 20-100 blocks | High |
| **Gravel** | -5 to -30m | All | 15-60 blocks | Moderate |
| **Silt** | -2 to -8m | River valleys | 20-80 blocks | Moderate |

**Clay Extraction**:

- Found near water sources (rivers, lakes)
- Used for: pottery, bricks, ceramics
- Must be wet to be workable; dries into bricks
- Quality varies: common clay → fine kaolin (rare)

### 3.3 Gravel and Aggregate

**Gravel Deposit Characteristics**:

| Deposit Type | Location | Yield | Composition | Use |
|--------------|----------|-------|-------------|-----|
| **River Gravel** | Riverbeds, beaches | High | Mixed stone | Construction, filtering |
| **Glacial Till** | Boreal regions | Moderate | Mixed sediment | Fill, concrete |
| **Crushed Stone** | Quarried | Very High | Uniform | Concrete, roads |
| **Volcanic Gravel** | Near tuff formations | Low | Porous, light | Insulation, filtration |

**Aggregate Quality**:

```csharp
public class AggregateQuality
{
    public float StoneRatio { get; set; }      // 0.0 to 1.0
    public float SandRatio { get; set; }       // 0.0 to 1.0
    public float ClayRatio { get; set; }       // 0.0 to 1.0
    public float OrganicContent { get; set; }  // 0.0 to 1.0 (lower is better)
    
    // Concrete quality calculation
    public float GetConcreteStrength()
    {
        // Ideal: 60% stone, 30% sand, 10% clay binder
        float idealStone = 0.6f;
        float idealSand = 0.3f;
        float idealClay = 0.1f;
        
        float stoneScore = 1.0f - Mathf.Abs(StoneRatio - idealStone);
        float sandScore = 1.0f - Mathf.Abs(SandRatio - idealSand);
        float clayScore = 1.0f - Mathf.Abs(ClayRatio - idealClay);
        float purityScore = 1.0f - OrganicContent;
        
        return (stoneScore + sandScore + clayScore + purityScore) / 4.0f;
    }
}
```

---

## 4. Mineral Resources (Geological Strata)

### 4.1 Depth-Based Ore Distribution

**Geological Strata Resource Table**:

| Depth Range | Stratum | Primary Rocks | Ore Types | Rarity |
|-------------|---------|---------------|-----------|--------|
| 0 to -10m | Topsoil/Subsurface | Dirt, sand, gravel | None (surface deposits only) | N/A |
| -10 to -30m | Sedimentary | Stone, sandstone | Coal, copper (trace) | Common |
| -30 to -60m | Shallow Bedrock | Stone, limestone | Coal, copper, iron (trace) | Common |
| -60 to -100m | Deep Bedrock | Stone, granite | Iron, copper, gold (trace) | Uncommon |
| -100 to -150m | Lower Crust | Granite, gneiss | Iron, gold, gems | Rare |
| -150 to -200m | Mantle Boundary | Obsidian, basalt | Gems, rare earths, uranium | Very Rare |

### 4.2 Coal (Shallow: -10 to -80m)

**Coal Distribution**:

| Coal Type | Depth | Yield per Block | Energy Content | Special Properties |
|-----------|-------|-----------------|----------------|-------------------|
| **Lignite** | -10 to -30m | 1 coal | Low | Easy to mine, burns dirty |
| **Bituminous** | -30 to -60m | 1-2 coal | Medium | Standard fuel source |
| **Anthracite** | -60 to -80m | 2 coal | High | Clean burning, efficient |

**Coal Vein Characteristics**:

```csharp
public class CoalVeinGenerator
{
    public void GenerateCoalVeins(Chunk chunk)
    {
        // Coal appears in layers, not veins
        for (int y = -10; y >= -80; y--)
        {
            // Layer thickness: 2-5 blocks
            int layerThickness = Random.Range(2, 6);
            
            // Coal frequency by depth
            float coalChance = y > -30 ? 0.15f :      // 15% in shallow
                              y > -60 ? 0.25f :      // 25% in mid
                              0.20f;                  // 20% in deep
            
            // Apply to horizontal plane with noise variation
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    float noise = _coalNoise.GetNoise(chunk.WorldX + x, y, chunk.WorldZ + z);
                    
                    if (noise > (1.0f - coalChance))
                    {
                        // Determine coal type by depth
                        var coalType = y > -30 ? CoalType.Lignite :
                                      y > -60 ? CoalType.Bituminous :
                                      CoalType.Anthracite;
                        
                        chunk.SetBlock(x, y, z, BlockType.CoalOre, coalType);
                    }
                }
            }
        }
    }
}
```

**Extraction Requirements**:
- Hand mining: Possible but slow (30s per block)
- Stone pickaxe: Moderate speed (10s per block)
- Copper pickaxe: Good speed (6s per block)
- Iron+ pickaxe: Fast (3s per block)

### 4.3 Iron (Mid-Depth: -20 to -100m)

**Iron Ore Distribution**:

| Depth | Frequency | Vein Size | Tool Required | Yield |
|-------|-----------|-----------|---------------|-------|
| -20 to -40m | 5% | Small (5-15 blocks) | Stone pick | 1 ore |
| -40 to -70m | 15% | Medium (15-40 blocks) | Copper pick | 1 ore |
| -70 to -100m | 10% | Large (30-80 blocks) | Iron pick | 1-2 ore |

**Iron Vein Algorithm**:

```csharp
public class IronVeinGenerator
{
    public void GenerateIronVeins(Chunk chunk)
    {
        // Iron appears in discrete veins
        // Poisson point process for vein centers
        var veinCenters = PoissonDiskSampling.Generate3D(
            chunk.Bounds, 
            minDistance: 20f,  // Veins at least 20m apart
            maxAttempts: 50
        );
        
        foreach (var center in veinCenters)
        {
            // Only place if in valid depth range
            if (center.Y < -20 && center.Y > -100)
            {
                GenerateVein(chunk, center);
            }
        }
    }
    
    private void GenerateVein(Chunk chunk, Vector3 center)
    {
        // Vein size based on depth (deeper = larger)
        float depthFactor = Mathf.Abs(center.Y) / 100f;
        float radius = Random.Range(3f, 8f) * (1 + depthFactor);
        
        // Irregular shape using noise
        for (int x = -Mathf.CeilToInt(radius); x <= Mathf.CeilToInt(radius); x++)
        {
            for (int y = -Mathf.CeilToInt(radius); y <= Mathf.CeilToInt(radius); y++)
            {
                for (int z = -Mathf.CeilToInt(radius); z <= Mathf.CeilToInt(radius); z++)
                {
                    var pos = center + new Vector3(x, y, z);
                    float dist = pos.DistanceTo(center);
                    
                    // Density falls off from center
                    float density = 1.0f - (dist / radius);
                    
                    // Add noise for irregular edges
                    float edgeNoise = _veinNoise.GetNoise(pos.X, pos.Y, pos.Z);
                    
                    if (density > 0 && edgeNoise < density)
                    {
                        // Place iron ore
                        var localPos = WorldToLocal(pos);
                        if (chunk.IsValidPosition(localPos))
                        {
                            chunk.SetBlock(localPos, BlockType.IronOre);
                        }
                    }
                }
            }
        }
    }
}
```

### 4.4 Copper (Mid-Depth: -30 to -120m)

**Copper Distribution Pattern**:

| Depth | Frequency | Vein Size | Biome Modifier | Yield |
|-------|-----------|-----------|----------------|-------|
| -30 to -50m | 12% | Small (8-20 blocks) | Desert +20% | 1 ore |
| -50 to -90m | 18% | Medium (20-50 blocks) | Desert +20% | 1-2 ore |
| -90 to -120m | 8% | Small (10-25 blocks) | None | 1-2 ore |

**Copper vs. Iron Overlap**:
- Both present at -40 to -90m depth
- Copper more common shallower; iron more common deeper
- Strategic choice: mine for copper (easier, Bronze Age) or push deeper for iron

### 4.5 Precious Metals (Deep: -50 to -150m)

**Gold Distribution**:

| Depth | Frequency | Vein Size | Special Conditions | Yield |
|-------|-----------|-----------|-------------------|-------|
| -50 to -80m | 2% | Tiny (3-8 blocks) | Near granite | 1 ore |
| -80 to -120m | 5% | Small (8-20 blocks) | None | 1 ore |
| -120 to -150m | 3% | Small (8-15 blocks) | Near faults | 1-2 ore |

**Gold Extraction Challenges**:
- Very hard stone at depth (granite, deepslate)
- Requires iron+ pickaxe
- Often found near cave systems (hydrothermal deposition)

### 4.6 Rare Earths (Very Deep: -100 to -200m)

**Rare Resource Types**:

| Resource | Depth | Rarity | Use | Special Properties |
|----------|-------|--------|-----|-------------------|
| **Gems** | -100 to -180m | Very Rare | Currency, advanced crafting | Multiple types: diamond, ruby, sapphire |
| **Uranium** | -150 to -200m | Extremely Rare | Nuclear power, weapons | Radioactive (requires protection) |
| **Rare Earth Elements** | -120 to -200m | Rare | Electronics, magnets | Cluster deposits |
| **Platinum** | -140 to -200m | Very Rare | Catalysts, jewelry | Always near bedrock |

**Rare Resource Extraction Cost**:

```csharp
public class DeepMiningCostCalculator
{
    public MiningCost CalculateCost(BlockPosition pos, ResourceType resource)
    {
        float depth = Mathf.Abs(pos.Y);
        
        // Base extraction time increases with depth
        float timeMultiplier = 1.0f + (depth / 50f); // 2x at -50m, 5x at -200m
        
        // Tool wear increases with depth
        float toolWearMultiplier = 1.0f + (depth / 100f);
        
        // Energy/light requirements
        bool requiresLighting = depth > 50;
        bool requiresVentilation = depth > 100;
        bool requiresReinforcement = depth > 150;
        
        // Hazard chance
        float caveInChance = depth > 100 ? 0.001f * (depth - 100) / 10f : 0f;
        float gasChance = depth > 80 ? 0.0005f * (depth - 80) / 20f : 0f;
        
        return new MiningCost
        {
            ExtractionTime = baseExtractionTime * timeMultiplier,
            ToolWear = baseToolWear * toolWearMultiplier,
            RequiresLighting = requiresLighting,
            RequiresVentilation = requiresVentilation,
            RequiresReinforcement = requiresReinforcement,
            CaveInChance = caveInChance,
            GasExposureChance = gasChance
        };
    }
}
```

---

## 5. Resource Regeneration

### 5.1 Renewable Resources (Wood, Plants)

**Renewable Categories**:

| Resource Type | Regeneration Rate | Max Density | Constraints |
|---------------|-------------------|-------------|-------------|
| **Trees** | 3-6 days to mature | 20% of chunk | Space, light, soil |
| **Crops** | 3-7 days to harvest | Farm plots only | Water, light, player action |
| **Wild Plants** | 1-5 days | 10% of chunk | Biome appropriate |
| **Wildlife** | Continuous spawning | Carrying capacity | Habitat, predation |
| **Fish** | Continuous spawning | Water body capacity | Water quality |

**Regeneration Algorithm**:

```csharp
public class ResourceRegenerationSystem
{
    public void TickRegeneration(Chunk chunk)
    {
        // Trees: 0.1% chance per empty valid location per tick
        if (chunk.TreeCount < chunk.MaxTreeCapacity)
        {
            TrySpawnSapling(chunk);
        }
        
        // Wild plants: 0.5% chance per tick per eligible block
        TryRegrowWildPlants(chunk);
        
        // Wildlife: Population dynamics
        UpdateWildlifePopulations(chunk);
        
        // Soil recovery (very slow)
        RecoverSoilFertility(chunk);
    }
    
    private void TrySpawnSapling(Chunk chunk)
    {
        // Find valid location (grass/dirt, light level 9+, space above)
        var validLocations = FindValidTreeLocations(chunk);
        
        if (validLocations.Count == 0) return;
        
        // Spawn chance: 0.1% per tick per valid spot
        // = ~10% per day per spot at 20 TPS
        foreach (var pos in validLocations)
        {
            if (Random.value < 0.001f)
            {
                PlaceSapling(pos, GetRandomTreeType(chunk.Biome));
                break; // Max 1 per chunk per tick
            }
        }
    }
}
```

### 5.2 Non-Renewable Resources (Ores)

**Finite Resource Characteristics**:

| Resource | Total World Quantity (MVP) | Depletion Timeframe | Post-Depletion |
|----------|---------------------------|---------------------|----------------|
| **Coal** | ~500,000 blocks | 60-90 days (heavy use) | Import, alternative energy |
| **Iron** | ~200,000 blocks | 45-60 days | Deep mining, recycling |
| **Copper** | ~150,000 blocks | 40-50 days | Deep mining, alternatives |
| **Gold** | ~20,000 blocks | 30-40 days | Very deep mining, trade |
| **Gems** | ~5,000 blocks | Indefinite (rare use) | Extremely deep mining |

**Resource Depletion Tracking**:

```csharp
public class ResourceDepletionTracker
{
    public class ResourceStats
    {
        public int TotalGenerated { get; set; }
        public int TotalExtracted { get; set; }
        public int Remaining => TotalGenerated - TotalExtracted;
        public float DepletionPercent => (float)TotalExtracted / TotalGenerated;
    }
    
    private Dictionary<ResourceType, ResourceStats> _stats = new();
    
    public void LogExtraction(ResourceType resource, int amount, BlockPosition pos)
    {
        _stats[resource].TotalExtracted += amount;
        
        // Trigger alerts at thresholds
        float depletion = _stats[resource].DepletionPercent;
        if (depletion > 0.5f && depletion - (amount / (float)_stats[resource].TotalGenerated) <= 0.5f)
        {
            BroadcastAlert($"{resource} reserves have fallen below 50%");
        }
        if (depletion > 0.8f)
        {
            BroadcastAlert($"CRITICAL: {resource} reserves critically low ({(1-depletion)*100:F1}% remaining)");
        }
    }
}
```

### 5.3 Depletion and Scarcity

**Scarcity Effects on Economy**:

| Depletion Level | Price Multiplier | Extraction Priority | Alternative Incentives |
|-----------------|------------------|---------------------|------------------------|
| >75% remaining | 1.0x (baseline) | Low | None |
| 50-75% | 1.5x | Moderate | Recycling programs |
| 25-50% | 2.5x | High | Substitution research |
| 10-25% | 5.0x | Critical | Import infrastructure |
| <10% | 10.0x+ | Emergency | Synthetic alternatives |

**Depletion Visual Indicators**:
- Underground: Harder stone (more mining time)
- Surface: Deeper digging required for same resources
- Economic: Price increases in markets
- Agent behavior: Longer travel distances to find resources

### 5.4 Resource Migration (Advanced)

**Post-MVP Advanced System**:

As the server ages and surface/shallow resources deplete, several migration mechanics activate:

1. **Geological Surveying (Tech Unlock)**:
   - Players can survey to find remaining ore deposits
   - Reveals approximate locations of unexploited veins
   - Requires investment: time + equipment

2. **Deep Drilling**:
   - Bypass shallow layers entirely
   - Expensive infrastructure ( Industrial Age)
   - Access to deep reserves without full mining operations

3. **Resource Recycling**:
   - Salvage from abandoned structures
   - Reprocess waste/tailings
   - Metal reclamation from tools/weapons

4. **Trade Route Establishment**:
   - Import from distant regions
   - Economic solution to local depletion
   - Session 5: Governance - trade agreements

5. **Synthetic Substitution**:
   - Late-game tech alternatives
   - Biomaterials, engineered stone
   - Reduced dependency on mined resources

---

## 6. Gathering Mechanics

### 6.1 Tool Requirements Per Resource

**Tool Hierarchy**:

| Tool Material | Mining Level | Effective On | Durability | Efficiency |
|---------------|--------------|--------------|------------|------------|
| **Hand** | 0 | Dirt, sand, plants | ∞ | 0.5x |
| **Stone** | 1 | +wood, gravel, soft stone | 20 uses | 1.0x |
| **Copper** | 2 | +stone, coal, copper ore | 50 uses | 1.2x |
| **Bronze** | 2 | Same as copper, +iron ore | 75 uses | 1.3x |
| **Iron** | 3 | +all ores except deepest | 100 uses | 1.5x |
| **Steel** | 4 | +all resources | 200 uses | 2.0x |
| **Diamond** | 5 | +obsidian, bedrock (no yield) | 500 uses | 3.0x |

**Tool-Specific Gathering Rates**:

| Resource | Hand | Stone | Copper | Iron | Steel |
|----------|------|-------|--------|------|-------|
| Wood | 30s | 10s | 8s | 6s | 4s |
| Dirt | 5s | 2s | 1s | 0.5s | 0.3s |
| Stone | 120s | 15s | 10s | 5s | 3s |
| Coal Ore | ∞ | 20s | 12s | 6s | 4s |
| Iron Ore | ∞ | ∞ | 30s | 15s | 8s |
| Gold Ore | ∞ | ∞ | ∞ | 25s | 12s |
| Obsidian | ∞ | ∞ | ∞ | ∞ | 60s |

**Tool Effectiveness Formula**:

```csharp
public class GatheringCalculator
{
    public GatheringResult CalculateGathering(
        ResourceType resource, 
        ToolItem tool, 
        Entity gatherer,
        BlockPosition pos)
    {
        var resourceDef = ResourceRegistry.Get(resource);
        float baseTime = resourceDef.BaseGatherTime;
        
        // Tool efficiency
        float toolEfficiency = tool?.Efficiency ?? 0.5f; // Hand = 0.5
        
        // Check tool level requirement
        if (tool?.MiningLevel < resourceDef.RequiredMiningLevel)
        {
            return GatheringResult.Failure("Tool too weak for this resource");
        }
        
        // Skill modifier
        float skillModifier = 1.0f + (gatherer.Skills.Mining * 0.1f);
        
        // Depth modifier (deeper = harder)
        float depthModifier = 1.0f + (Mathf.Abs(pos.Y) / 200f);
        
        // Final time calculation
        float finalTime = baseTime / (toolEfficiency * skillModifier) * depthModifier;
        
        // Yield calculation
        int baseYield = resourceDef.BaseYield;
        float yieldMultiplier = toolEfficiency * (0.8f + Random.value * 0.4f); // ±20% variance
        int finalYield = Mathf.Max(1, Mathf.RoundToInt(baseYield * yieldMultiplier));
        
        // Tool wear
        int durabilityCost = Mathf.Max(1, Mathf.RoundToInt(resourceDef.Hardness / tool?.DurabilityEfficiency ?? 1f));
        
        return new GatheringResult
        {
            TimeRequired = finalTime,
            Yield = finalYield,
            ToolDurabilityCost = durabilityCost,
            Success = true
        };
    }
}
```

### 6.2 Mining Depth Challenges

**Depth-Based Challenges**:

| Depth | Challenge | Mitigation | Consequence |
|-------|-----------|------------|-------------|
| -20m | Darkness | Torches, lanterns | Slower work, mob spawning |
| -50m | Stone hardness | Better pickaxes | Increased tool wear |
| -80m | Long travel | Shaft elevators, ladders | Time inefficiency |
| -100m | Cave-ins | Supports, beams | Injury, resource loss |
| -150m | Toxic gas | Ventilation, masks | Health damage |
| -200m | Extreme pressure | Reinforced gear | Movement penalty |

**Cave System Exploration**:

Caves provide natural access to deeper resources without vertical digging:

| Cave Type | Depth Range | Resource Density | Danger Level |
|-----------|-------------|------------------|--------------|
| **Surface Caves** | -5 to -20m | Low | Low |
| **Shallow Tunnels** | -20 to -50m | Moderate | Moderate |
| **Deep Chambers** | -50 to -100m | High | High |
| **Abyssal Caves** | -100 to -180m | Very High | Very High |
| **Magma Chambers** | -180 to -200m | Extreme (rare resources) | Extreme |

**Cave Exploration Mechanics**:

```csharp
public class CaveExplorationSystem
{
    public class CaveHazard
    {
        public HazardType Type { get; set; }
        public float Probability { get; set; }
        public int Damage { get; set; }
        public bool IsMitigatedBy(PlayerGear gear);
    }
    
    public ExplorationResult ExploreCave(Entity explorer, Cave cave)
    {
        var result = new ExplorationResult();
        
        // Light check
        if (explorer.GetLightLevel() < 8)
        {
            result.AddPenalty(ExplorationPenalty.LowVisibility, 0.5f);
        }
        
        // Depth hazards
        foreach (var hazard in GetHazardsForDepth(cave.Depth))
        {
            if (Random.value < hazard.Probability && !hazard.IsMitigatedBy(explorer.Gear))
            {
                result.AddHazard(hazard);
                explorer.TakeDamage(hazard.Damage);
            }
        }
        
        // Resource discovery
        var discoveredResources = ScanForResources(cave, explorer.Skills.Prospecting);
        result.DiscoveredResources = discoveredResources;
        
        return result;
    }
}
```

### 6.3 Resource Node Sizes

**Node Size Distribution**:

| Size Category | Block Count | Typical For | Discovery Method |
|---------------|-------------|-------------|------------------|
| **Tiny** | 1-5 blocks | Gems, gold surface deposits | Visual scanning |
| **Small** | 5-20 blocks | Iron, copper shallow veins | Surface indicators |
| **Medium** | 20-50 blocks | Coal seams, iron veins | Prospecting |
| **Large** | 50-150 blocks | Major coal deposits | Systematic mining |
| **Huge** | 150-500 blocks | Rare, regional deposits | Geological survey |

**Node Visualization**:

- Tiny: Single block or small cluster visible on surface
- Small: Visible discoloration, vegetation changes
- Medium: Requires digging/exploration to fully assess
- Large: Regional feature, known to local players
- Huge: Strategic resource, may trigger territorial claims

---

## 7. Resource Processing

### 7.1 Raw to Processed Materials

**Processing Chains**:

| Raw Resource | Processing Step | Time | Output | Tool/Station |
|--------------|-----------------|------|--------|--------------|
| **Logs** | Cut | 5s | 4 planks | Saw, workbench |
| **Stone** | Crush | 10s | 2 gravel + 1 sand | Crusher, hammer |
| **Ore** | Smelt | 30s | 1 ingot (per 2 ore) | Furnace, fuel |
| **Clay** | Fire | 20s | 1 brick | Kiln, fire |
| **Food** | Cook | 10s | 2x nutrition, no disease | Fire, stove |
| **Hide** | Tan | 60s | 1 leather | Tanning station |

**Processing Efficiency**:

```csharp
public class ProcessingEfficiencyCalculator
{
    public float CalculateEfficiency(
        Entity processor,
        ProcessingStation station,
        Recipe recipe)
    {
        float baseEfficiency = 1.0f;
        
        // Skill bonus
        float skillBonus = processor.Skills.GetSkill(recipe.SkillType) * 0.05f;
        
        // Station quality bonus
        float stationBonus = station.QualityLevel * 0.1f;
        
        // Tool bonus (if applicable)
        float toolBonus = processor.HeldTool?.ProcessingBonus ?? 0f;
        
        // Batch processing bonus (economy of scale)
        float batchBonus = recipe.BatchSize > 1 ? 0.1f : 0f;
        
        return baseEfficiency + skillBonus + stationBonus + toolBonus + batchBonus;
    }
    
    public ProcessingResult Process(Recipe recipe, int quantity, float efficiency)
    {
        // Input calculation (may use less with high efficiency)
        float inputMultiplier = 1.0f - (efficiency - 1.0f) * 0.2f;
        int actualInput = Mathf.CeilToInt(recipe.InputQuantity * quantity * inputMultiplier);
        
        // Output calculation (may produce more with high efficiency)
        float outputMultiplier = 1.0f + (efficiency - 1.0f) * 0.3f;
        int actualOutput = Mathf.FloorToInt(recipe.OutputQuantity * quantity * outputMultiplier);
        
        // Time calculation (faster with efficiency)
        float timeMultiplier = 1.0f / efficiency;
        float actualTime = recipe.BaseTime * quantity * timeMultiplier;
        
        return new ProcessingResult
        {
            InputRequired = actualInput,
            OutputProduced = actualOutput,
            TimeRequired = actualTime,
            Quality = CalculateQuality(efficiency)
        };
    }
}
```

### 7.2 Refining and Smelting

**Smelting Requirements**:

| Input | Fuel Required | Temperature | Output | Byproduct |
|-------|---------------|-------------|--------|-----------|
| Iron Ore (2) | Coal (1) | 1200°C | Iron Ingot (1) | Slag (0.5) |
| Copper Ore (2) | Coal (1) | 1100°C | Copper Ingot (1) | Slag (0.5) |
| Gold Ore (2) | Coal (1) | 1000°C | Gold Ingot (1) | None |
| Sand | Coal (0.5) | 1400°C | Glass (1) | None |
| Clay | Wood (1) | 800°C | Brick (2) | Ash (0.2) |

**Temperature System**:

```csharp
public class SmeltingTemperatureSystem
{
    public class FurnaceState
    {
        public float CurrentTemperature { get; set; }
        public float TargetTemperature { get; set; }
        public FuelType CurrentFuel { get; set; }
        public float FuelRemaining { get; set; }
    }
    
    public void UpdateFurnace(Furnace furnace, float deltaTime)
    {
        // Heat up toward target
        float heatRate = 50f; // degrees per second
        if (furnace.CurrentTemperature < furnace.TargetTemperature)
        {
            furnace.CurrentTemperature += heatRate * deltaTime;
        }
        else if (furnace.CurrentTemperature > furnace.TargetTemperature)
        {
            furnace.CurrentTemperature -= heatRate * 0.5f * deltaTime; // Cools slower
        }
        
        // Consume fuel
        if (furnace.CurrentFuel != null)
        {
            float consumptionRate = furnace.TargetTemperature / furnace.CurrentFuel.MaxTemperature;
            furnace.FuelRemaining -= consumptionRate * deltaTime;
            
            if (furnace.FuelRemaining <= 0)
            {
                furnace.CurrentFuel = null;
                furnace.TargetTemperature = 25f; // Ambient
            }
        }
        
        // Process items if at temperature
        foreach (var item in furnace.InputItems)
        {
            var recipe = SmeltingRecipes.Get(item.Type);
            if (furnace.CurrentTemperature >= recipe.RequiredTemperature)
            {
                item.SmeltingProgress += deltaTime;
                if (item.SmeltingProgress >= recipe.SmeltingTime)
                {
                    CompleteSmelting(furnace, item, recipe);
                }
            }
        }
    }
}
```

### 7.3 Waste Byproducts (Tailings)

**Mining Waste Generation**:

Every mining operation produces waste material:

| Mining Operation | Waste Produced | Waste:Resource Ratio | Disposal Method |
|------------------|----------------|----------------------|-----------------|
| **Coal Mining** | Stone, gravel | 5:1 | Discard, fill, construction |
| **Iron Mining** | Stone, poor ore | 3:1 | Discard, tailings pond |
| **Copper Mining** | Stone, sulfur compounds | 4:1 | Tailings pond (toxic) |
| **Gold Mining** | Stone, cyanide residues | 10:1 | Special containment |
| **Stone Quarrying** | Dust, gravel | 0.5:1 | Construction fill |

**Tailings Management**:

```csharp
public class TailingsSystem
{
    public class TailingsPond
    {
        public Vector3 Location { get; set; }
        public float Capacity { get; set; }
        public float CurrentVolume { get; set; }
        public float Toxicity { get; set; }
        public List<WasteType> Contents { get; set; }
        
        public bool CanAccept(WasteType waste, float volume)
        {
            return CurrentVolume + volume <= Capacity;
        }
        
        public void AddWaste(WasteType waste, float volume)
        {
            Contents.Add(waste);
            CurrentVolume += volume;
            Toxicity = CalculateToxicity(Contents);
        }
        
        // Seepage into groundwater
        public void UpdateEnvironmentalImpact()
        {
            if (Toxicity > 0.5f && CurrentVolume > Capacity * 0.8f)
            {
                // Risk of contamination
                var nearbyWater = FindWaterSourcesWithin(50f);
                foreach (var water in nearbyWater)
                {
                    water.Contamination += Toxicity * 0.01f * deltaTime;
                }
            }
        }
    }
    
    // Alternative: Waste processing
    public class WasteProcessingPlant
    {
        public void ProcessTailings(TailingsPond pond)
        {
            // Extract remaining metals
            var recoveredMetal = ExtractMetals(pond.Contents);
            
            // Neutralize toxins
            var neutralizedWaste = Neutralize(pond.Contents);
            
            // Compress for storage
            var compressedVolume = Compress(neutralizedWaste);
            
            // Return processed volume (reduced)
            pond.CurrentVolume = compressedVolume;
            pond.Toxicity *= 0.1f; // 90% reduction
        }
    }
}
```

**Environmental Impact of Tailings**:

| Impact Type | Threshold | Effect | Mitigation |
|-------------|-----------|--------|------------|
| **Water contamination** | Toxicity > 0.5 | Polluted water sources | Lining, treatment |
| **Soil degradation** | Toxicity > 0.3 | Reduced crop yields | Capping, remediation |
| **Air pollution** | Dust > 100 units | Health effects | Wetting, covering |
| **Visual blight** | Any tailings | Property value reduction | Landscaping |

---

## 8. Economic Impact

### 8.1 Resource Scarcity Pricing

**Dynamic Pricing Model**:

Resource prices fluctuate based on supply and demand:

```csharp
public class ResourcePricingModel
{
    public float CalculatePrice(ResourceType resource, Market market)
    {
        var stats = market.GetResourceStats(resource);
        
        // Base price
        float basePrice = ResourceRegistry.Get(resource).BaseValue;
        
        // Supply factor (more supply = lower price)
        float supplyFactor = 1.0f / (1.0f + stats.DailySupply / stats.AverageSupply);
        
        // Demand factor (more demand = higher price)
        float demandFactor = 1.0f + (stats.DailyDemand / stats.AverageDemand - 1.0f) * 0.5f;
        
        // Scarcity factor (global reserves)
        float globalScarcity = 1.0f + GetGlobalDepletion(resource) * 2.0f;
        
        // Extraction cost factor (deeper = more expensive)
        float extractionCost = 1.0f + (stats.AverageExtractionDepth / 100f) * 0.5f;
        
        // Final price calculation
        float finalPrice = basePrice * supplyFactor * demandFactor * globalScarcity * extractionCost;
        
        // Price smoothing (prevent wild swings)
        float previousPrice = stats.PreviousPrice;
        float smoothedPrice = previousPrice * 0.7f + finalPrice * 0.3f;
        
        return smoothedPrice;
    }
}
```

**Price Elasticity Examples**:

| Resource | Base Price | At 50% Depletion | At 90% Depletion |
|----------|------------|------------------|------------------|
| **Coal** | 5 currency | 8 currency | 25 currency |
| **Iron** | 15 currency | 25 currency | 75 currency |
| **Copper** | 20 currency | 35 currency | 100 currency |
| **Gold** | 100 currency | 175 currency | 500 currency |

### 8.2 Market Dynamics

**Supply Chain Visualization**:

```
[Extraction] → [Processing] → [Manufacturing] → [Distribution] → [Consumption]
     ↓              ↓               ↓                ↓                ↓
   Miners       Smelters       Crafters      Merchants/Traders   Builders/
   Laborers     Refiners       Artisans      Shops               Consumers
```

**Market Depth by Resource**:

| Resource | Daily Volume | Active Traders | Price Volatility |
|----------|--------------|----------------|------------------|
| **Wood** | High | Many (10-20) | Low (±5%) |
| **Stone** | High | Many (8-15) | Low (±5%) |
| **Coal** | Moderate | Moderate (5-10) | Medium (±10%) |
| **Iron** | Moderate | Moderate (5-10) | Medium (±15%) |
| **Gold** | Low | Few (2-5) | High (±25%) |
| **Gems** | Very Low | Very Few (1-3) | Very High (±50%) |

**Speculation and Investment**:

Players can invest in resource futures:

- Buy resource contracts at current price for future delivery
- Profit if prices rise before contract matures
- Risk if prices fall or extraction fails
- Requires Session 5: Governance - contract enforcement

### 8.3 Trade Routes for Rare Resources

**Regional Resource Specialization**:

Different world regions have different resource abundances:

| Region | Abundant Resources | Scarce Resources | Trade Opportunity |
|--------|-------------------|------------------|-------------------|
| **Boreal North** | Wood, furs, iron | Sand, copper, spices | Export lumber, import metals |
| **Desert South** | Sand, copper, gold | Wood, water, food | Export metals, import organics |
| **Jungle East** | Hardwood, gems, food | Stone, coal, metals | Export luxury goods, import materials |
| **Mountain West** | Stone, gems, rare earths | Wood, food, water | Export minerals, import survival |

**Trade Route Mechanics**:

```csharp
public class TradeRouteSystem
{
    public class TradeRoute
    {
        public Vector3 Origin { get; set; }
        public Vector3 Destination { get; set; }
        public List<ResourceType> TradedResources { get; set; }
        public float Distance { get; set; }
        public float RiskLevel { get; set; }
        public List<Entity> ActiveCaravans { get; set; }
        
        public float CalculateProfitability()
        {
            float totalProfit = 0;
            
            foreach (var resource in TradedResources)
            {
                float buyPrice = GetPriceAt(Origin, resource);
                float sellPrice = GetPriceAt(Destination, resource);
                float transportCost = Distance * 0.1f; // 0.1 currency per meter
                
                totalProfit += (sellPrice - buyPrice - transportCost);
            }
            
            // Risk adjustment
            float riskMultiplier = 1.0f - RiskLevel * 0.5f;
            
            return totalProfit * riskMultiplier;
        }
    }
    
    public void UpdateTradeRoutes()
    {
        foreach (var route in ActiveRoutes)
        {
            // Spawn caravans based on profitability
            if (route.CalculateProfitability() > 50 && route.ActiveCaravans.Count < 3)
            {
                SpawnCaravan(route);
            }
            
            // Update caravan positions
            foreach (var caravan in route.ActiveCaravans)
            {
                MoveCaravan(caravan, route);
                
                // Risk events
                if (Random.value < route.RiskLevel * 0.001f)
                {
                    TriggerCaravanEvent(caravan, route);
                }
            }
        }
    }
}
```

---

## 9. Environmental Impact

### 9.1 Mining Pollution

**Pollution Types by Activity**:

| Mining Activity | Pollutant | Spread Range | Decay Rate | Impact |
|-----------------|-----------|--------------|------------|--------|
| **Surface mining** | Dust | 20m | 1 day | Visibility, health |
| **Deep mining** | Toxic gas | 50m (up) | 3 days | Health, wildlife |
| **Smelting** | Smoke, SO2 | 100m | 5 days | Crops, health, acid rain |
| **Tailings** | Heavy metals | 30m (seepage) | 30 days | Water, soil |
| **Deforestation** | CO2 (indirect) | Global | 100 days | Climate |

**Pollution Accumulation**:

```csharp
public class PollutionSystem
{
    public class PollutionSource
    {
        public Vector3 Location { get; set; }
        public PollutantType Type { get; set; }
        public float EmissionRate { get; set; }
        public float CurrentEmission { get; set; }
        
        public void Emit(float deltaTime)
        {
            CurrentEmission += EmissionRate * deltaTime;
        }
    }
    
    public class EnvironmentalPollution
    {
        public float AirQuality { get; set; } = 1.0f; // 1.0 = perfect
        public float WaterQuality { get; set; } = 1.0f;
        public float SoilQuality { get; set; } = 1.0f;
        
        public void ApplyPollution(PollutantType type, float amount, Vector3 source)
        {
            float distance = Vector3.Distance(source, this.Location);
            float attenuation = 1.0f / (1.0f + distance * 0.1f);
            float actualPollution = amount * attenuation;
            
            switch (type)
            {
                case PollutantType.Airborne:
                    AirQuality = Mathf.Max(0, AirQuality - actualPollution * 0.01f);
                    break;
                case PollutantType.Waterborne:
                    WaterQuality = Mathf.Max(0, WaterQuality - actualPollution * 0.02f);
                    break;
                case PollutantType.Soil:
                    SoilQuality = Mathf.Max(0, SoilQuality - actualPollution * 0.015f);
                    break;
            }
        }
        
        // Natural recovery
        public void Recover(float deltaTime)
        {
            float recoveryRate = 0.001f * deltaTime; // 0.1% per tick
            AirQuality = Mathf.Min(1.0f, AirQuality + recoveryRate);
            WaterQuality = Mathf.Min(1.0f, WaterQuality + recoveryRate * 0.5f);
            SoilQuality = Mathf.Min(1.0f, SoilQuality + recoveryRate * 0.3f);
        }
    }
}
```

**Health Effects**:

| Pollution Level | Air Quality | Health Impact | Crop Impact |
|-----------------|-------------|---------------|-------------|
| Clean | >0.9 | None | 100% yield |
| Mild | 0.7-0.9 | -5% stamina | 90% yield |
| Moderate | 0.5-0.7 | -15% stamina, sickness chance | 70% yield |
| Severe | 0.3-0.5 | -30% stamina, disease | 40% yield |
| Toxic | <0.3 | -50% stamina, death risk | 10% yield |

### 9.2 Deforestation Effects

**Forest Ecosystem Services**:

| Service | Forest Coverage | Degraded Coverage | Lost Coverage |
|---------|-----------------|-------------------|---------------|
| **Soil stability** | 100% | 70% | 30% |
| **Water retention** | 100% | 60% | 20% |
| **Wildlife habitat** | 100% | 50% | 10% |
| **Air quality** | 100% | 80% | 50% |
| **Climate regulation** | 100% | 75% | 40% |

**Deforestation Consequences**:

```csharp
public class DeforestationSystem
{
    public void CalculateDeforestationImpact(Chunk chunk)
    {
        float treeCoverage = chunk.TreeCount / chunk.MaxTreeCapacity;
        
        if (treeCoverage < 0.3f)
        {
            // Severe deforestation
            
            // Soil erosion
            float erosionRate = (0.3f - treeCoverage) * 0.1f;
            ApplySoilErosion(chunk, erosionRate);
            
            // Reduced rainfall
            float rainfallReduction = (0.3f - treeCoverage) * 0.3f;
            ModifyLocalClimate(chunk, rainfallMultiplier: 1.0f - rainfallReduction);
            
            // Wildlife exodus
            if (treeCoverage < 0.1f)
            {
                EvacuateWildlife(chunk);
            }
            
            // Flooding risk
            float floodRisk = (0.3f - treeCoverage) * 2.0f;
            chunk.FloodRiskMultiplier = 1.0f + floodRisk;
        }
    }
    
    public void ApplySoilErosion(Chunk chunk, float rate)
    {
        // Topsoil turns to gravel over time
        foreach (var pos in chunk.SurfaceBlocks)
        {
            if (IsTopsoil(pos) && Random.value < rate)
            {
                ReplaceBlock(pos, BlockType.Gravel);
            }
        }
    }
}
```

### 9.3 Resource Extraction Laws

**Governance Integration** (Session 5):

Resource extraction can be regulated through player governance:

| Regulation Type | Enforcement | Penalty | Purpose |
|-----------------|-------------|---------|---------|
| **Mining permits** | Required for large operations | Fines, shutdown | Track environmental impact |
| **Extraction quotas** | Limited ore per day | Fines, confiscation | Prevent rapid depletion |
| **Environmental bonds** | Upfront payment | Forfeiture | Fund rehabilitation |
| **Protected areas** | Absolute restriction | Criminal charges | Preserve ecosystems |
| **Pollution limits** | Monitored emissions | Fines, technology mandate | Control pollution |

**Law Effects on Economy**:

- Regulations increase extraction costs (10-30%)
- Create black markets for illegal extraction
- Drive technological innovation (cleaner methods)
- Generate government revenue (permits, fines)

### 9.4 Rehabilitation Requirements

**Mine Closure Standards**:

When mining operations cease, rehabilitation may be required:

| Rehabilitation Level | Requirements | Cost | Time | Result |
|---------------------|--------------|------|------|--------|
| **Basic** | Fill shafts, remove equipment | 20% of extraction value | 7 days | Safe, not usable |
| **Standard** + replant vegetation | 50% of extraction value | 30 days | Partial ecosystem |
| **Full** + soil restoration, water treatment | 100% of extraction value | 90 days | Full ecosystem |

**Rehabilitation Mechanics**:

```csharp
public class RehabilitationSystem
{
    public class RehabilitationProject
    {
        public Vector3 Location { get; set; }
        public float Progress { get; set; }
        public RehabilitationLevel TargetLevel { get; set; }
        public float Budget { get; set; }
        public List<Entity> Workers { get; set; }
        
        public void ProgressWork(float deltaTime)
        {
            float workAmount = CalculateWorkRate();
            Progress += workAmount * deltaTime;
            Budget -= workAmount * LaborCost * deltaTime;
            
            if (Progress >= 1.0f)
            {
                CompleteRehabilitation();
            }
        }
        
        private void CompleteRehabilitation()
        {
            // Restore terrain
            FillExcavations(Location);
            
            // Replant vegetation
            if (TargetLevel >= RehabilitationLevel.Standard)
            {
                ReplantVegetation(Location);
            }
            
            // Restore soil
            if (TargetLevel >= RehabilitationLevel.Full)
            {
                RestoreSoil(Location);
                TreatWater(Location);
            }
            
            // Remove pollution
            ClearPollution(Location);
            
            // Return land to jurisdiction
            MarkAsRehabilitated(Location);
        }
    }
}
```

---

## 10. Progression Balance

### 10.1 Early Game Resources (Surface)

**Phase 1: Immediate Survival (Days 1-3)**

| Resource | Availability | Method | Immediate Use |
|----------|--------------|--------|---------------|
| **Wood** | Plentiful | Hand breaking | Tools, shelter, fire |
| **Stone** | Common | Hand breaking | Basic tools, weapons |
| **Plants** | Plentiful | Hand gathering | Food, fiber, medicine |
| **Water** | Variable | Collection | Survival, farming |
| **Small game** | Moderate | Hunting/traps | Food, hide |

**Phase 2: Basic Settlement (Days 3-7)**

| Resource | Availability | Method | Use |
|----------|--------------|--------|-----|
| **Clay** | Moderate | Digging | Pottery, bricks |
| **Sand** | High (deserts) | Shovel | Glass, concrete |
| **Gravel** | Moderate | Digging | Construction |
| **Surface ores** | Rare | Prospecting | Early copper |
| **Boulders** | Common | Mining | Stone stockpile |

**Early Game Constraints**:
- No mining below 5m without stone tools
- Limited carrying capacity (50kg)
- No smelting without fire management
- Tools break quickly (stone)

### 10.2 Mid-Game Depth (50-100m)

**Phase 3: Bronze Age (Days 7-15)**

| Resource | Depth | Requirement | Unlock |
|----------|-------|-------------|--------|
| **Copper ore** | -20 to -60m | Copper pickaxe | Bronze tools |
| **Tin ore** | -30 to -80m | Copper pickaxe | Bronze (alloy) |
| **Coal (abundant)** | -20 to -60m | Any pickaxe | Smelting fuel |
| **Iron ore (trace)** | -40 to -80m | Bronze pickaxe | Iron preview |

**Phase 4: Iron Age (Days 15-30)**

| Resource | Depth | Requirement | Unlock |
|----------|-------|-------------|--------|
| **Iron ore** | -60 to -100m | Iron pickaxe | Iron tools, machines |
| **Gold (trace)** | -80 to -120m | Iron pickaxe | Currency, luxury |
| **Deep coal** | -60 to -100m | Any pickaxe | Industrial fuel |

**Mid-Game Challenges**:
- Cave navigation required (no direct shafts)
- Lighting infrastructure (torches, lanterns)
- Vertical transport (ladders, bucket elevators)
- Gas hazards at depth

### 10.3 Late-Game Deep Mining (150m+)

**Phase 5: Industrial Age (Days 30-60)**

| Resource | Depth | Requirement | Strategic Value |
|----------|-------|-------------|-----------------|
| **Gold deposits** | -120 to -150m | Steel pickaxe | Economic power |
| **Gem deposits** | -100 to -180m | Steel pickaxe | High-value trade |
| **Rare earths** | -150 to -200m | Steel + power | Electronics |
| **Deep iron** | -120 to -180m | Steel pickaxe | Sustained industry |

**Phase 6: Space Age (Days 60+)**

| Resource | Depth | Requirement | Purpose |
|----------|-------|-------------|---------|
| **Uranium** | -150 to -200m | Advanced tech | Nuclear power |
| **Platinum** | -180 to -200m | Diamond pickaxe | Catalysts, luxury |
| **Exotic matter** | Near bedrock | Space tech | Advanced materials |

**Late-Game Infrastructure**:
- Mechanical elevators (steam/electric)
- Pump systems for water/gas
- Automated mining machines
- Deep shaft reinforcement
- On-site processing plants

### 10.4 Tool Progression Alignment

**Tool-Resource Alignment Table**:

| Game Phase | Primary Tool | Unlocks Access To | Tech Gate |
|------------|--------------|-------------------|-----------|
| **Day 1-3** | Stone tools | Surface, shallow soil | None |
| **Day 4-10** | Copper tools | Stone, soft ores to 30m | Mining I |
| **Day 10-20** | Bronze tools | All ores to 60m, iron ore | Mining II, Metallurgy I |
| **Day 20-35** | Iron tools | All ores to 100m | Mining III, Metallurgy II |
| **Day 35-60** | Steel tools | Deepest ores, gems | Mining IV, Industrial I |
| **Day 60+** | Diamond/power | Bedrock proximity, rare earths | Mining V, Space Tech |

**Progression Pacing Validation**:

Each phase duration validated against Session 4 economic model:

| Phase | Duration | Resources Required | Time to Acquire |
|-------|----------|-------------------|-----------------|
| Stone→Copper | 3-5 days | 20 copper ore | 2-3 hours mining |
| Copper→Bronze | 2-3 days | 10 tin + 20 copper | 3-4 hours mining |
| Bronze→Iron | 5-10 days | 50 iron ore | 8-12 hours mining |
| Iron→Steel | 10-15 days | 100 iron + carbon | 15-20 hours processing |
| Steel→Diamond | 20-30 days | 500 deep resources | 30-40 hours deep mining |

**Resource Rate Validation** (from Session 4 day4-progression-and-balance.md):

- Wood: 10/min = reasonable for surface gathering
- Stone: 5/min = requires effort but not grind
- Iron ore: 3/min = meaningful challenge, 20 min per ingot
- Deep mining: 1-2/min = justifies mechanization

---

## Cross-References

### Dependencies

- **Requires**: 
  - [Session 1: 14-terrain-generation.md](../session-1-technical-architecture/14-terrain-generation.md) - Geological strata, ore distribution
  - [Session 1: 13-voxel-world-system.md](../session-1-technical-architecture/13-voxel-world-system.md) - Block types, world dimensions
  - [Session 4: day4-progression-and-balance.md](./day4-progression-and-balance.md) - Economic rates, tech progression
  
- **Informs**:
  - Session 5: Governance - Resource extraction laws, environmental regulation
  - Session 6: Prototyping - Resource gathering prototype scope
  - Session 7: Integration - World resource integration

### Integration Points

| System | Integration | Document |
|--------|-------------|----------|
| **Terrain Generation** | Ore vein placement | 14-terrain-generation.md |
| **Voxel World** | Block types, hardness | 13-voxel-world-system.md |
| **Economy** | Resource pricing, scarcity | day4-progression-and-balance.md |
| **AI Agents** | Resource gathering behaviors | Session 2 |
| **Governance** | Extraction laws, permits | Session 5 |

### Validation Against Session 1-2 Constraints

**Performance Budget Compliance**:

- Resource regeneration: 0.1-1% checks per tick per chunk
- Maximum: 100 chunks × 65,536 blocks × 0.001 = ~6,500 checks per tick
- At 20 TPS: 130,000 checks/second
- **Within Session 1 20 TPS budget** ✅

**Agent Economic Activity**:

- Resource gathering: ~2ms per action (position check, inventory update)
- At 100 agents gathering simultaneously: 200ms per tick
- **With bucketing**: 40 agents per bucket = 80ms per tick
- Acceptable with spatial partitioning (only nearby agents process)
- **Within Session 2 AI budget** ✅

---

## Open Questions & Future Research

### Unresolved Questions

- [ ] What's the optimal ore spawn rate to balance scarcity vs. frustration?
- [ ] How do we prevent "tunnel spam" (ugly abandoned mines)?
- [ ] Should rare resources be announced to all players or kept secret?
- [ ] What's the right balance between renewable and finite resources?
- [ ] How do we handle resource extraction in claimed territory?

### Research Needs

- [ ] Player retention data from games with resource depletion (Factorio, Eco)
- [ ] Optimal gathering rates for flow state maintenance
- [ ] Environmental impact modeling from real-world mining data
- [ ] Economic equilibrium modeling with finite resources

---

## Success Criteria

- [ ] All resource types defined with depth ranges and rarity
- [ ] Tool progression aligned with resource accessibility
- [ ] Renewable vs. finite resource balance documented
- [ ] Environmental impact mechanics specified
- [ ] Economic pricing model for scarcity
- [ ] Progression pacing validated against Session 4 targets
- [ ] Cross-references to Session 1 terrain generation established
- [ ] Integration points with Session 5 governance identified

---

**Status**: COMPLETE - World Resources Specification Ready

---

## Decisions Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-02-01 | 256-block vertical range with depth emphasis | Matches Session 1 terrain generation; emphasizes mining |
| 2026-02-01 | Geological strata system | Realistic progression gating; educational value |
| 2026-02-01 | Weight/carrying affects gathering | Adds logistics challenge; prevents infinite inventory |
| 2026-02-01 | Tool progression gates mining depth | Clear tech tree progression; meaningful upgrades |
| 2026-02-01 | Renewable resources (trees, plants) vs. finite (ores) | Creates strategic tension; sustainability theme |
| 2026-02-01 | Resource depletion tracking | Enables economic scarcity; end-game challenges |

---

**Navigation**: [← Previous: Session 4 Index](./[AGENTS-READ-FIRST]-index.md) | [Next: Gathering Mechanics Detail](./04-gathering-mechanics.md)
