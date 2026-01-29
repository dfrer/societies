# Resource Economy Balance Spreadsheet

This document serves as a template for the Excel spreadsheet with formulas and balance calculations.

## Sheet 1: Resource Generation Rates

| Resource | Base Gather Rate | Tool Modifier | Skill Modifier | Final Rate | Unit |
|----------|------------------|---------------|----------------|------------|------|
| Wood | 10 | 1.0 + (ToolTier × 0.5) | 1.0 + (Skill/100) | =B2×C2×D2 | units/min |
| Stone | 5 | 1.0 + (ToolTier × 0.5) | 1.0 + (Skill/100) | =B3×C3×D3 | units/min |
| Iron Ore | 3 | 1.0 + (ToolTier × 0.5) | 1.0 + (Skill/100) | =B4×C4×D4 | units/min |
| Copper Ore | 3 | 1.0 + (ToolTier × 0.5) | 1.0 + (Skill/100) | =B5×C5×D5 | units/min |
| Food (Gather) | 2 | N/A | 1.0 + (Skill/100) | =B6×D6 | units/min |
| Food (Farm) | 5 | N/A | 1.0 + (Skill/100) | =B7×D7 | units/min |
| Water | 10 | N/A | N/A | =B8 | units/min |
| Coal | 4 | 1.0 + (ToolTier × 0.5) | 1.0 + (Skill/100) | =B9×C9×D9 | units/min |
| Gold | 1 | 1.0 + (ToolTier × 0.3) | 1.0 + (Skill/150) | =B10×C10×D10 | units/min |

**Formula Reference**:
- ToolTier: 0 (hands), 1 (stone), 2 (copper), 3 (bronze), 4 (iron), 5 (steel)
- Skill: 0-100 skill level

---

## Sheet 2: Production Chains

| Product | Input 1 | Qty 1 | Input 2 | Qty 2 | Time | Output | Efficiency |
|---------|---------|-------|---------|-------|------|--------|------------|
| Stone Axe | Wood | 5 | Stone | 10 | 2 min | 1 | 100% |
| Copper Ingot | Copper Ore | 3 | Coal | 1 | 5 min | 1 | 100% |
| Copper Tool | Copper Ingot | 2 | Wood | 3 | 3 min | 1 | 100% |
| Bronze Ingot | Copper | 2 | Tin | 1 | 5 min | 1 | 100% |
| Iron Ingot | Iron Ore | 3 | Coal | 2 | 8 min | 1 | 100% |
| Iron Tool | Iron Ingot | 2 | Wood | 3 | 5 min | 1 | 100% |
| Steel | Iron | 2 | Coal | 3 | 10 min | 1 | 100% |
| Bread | Flour | 1 | Water | 1 | 1 min | 2 | 100% |
| Flour | Wheat | 3 | - | - | 2 min | 1 | 100% |
| Cooked Meat | Raw Meat | 1 | Fire | - | 2 min | 1 | 100% |
| Building Materials | Wood | 20 | Stone | 10 | 10 min | 10 | 100% |

**Chain Analysis - Iron Tool Production**:

```
Iron Tool = 2 Iron Ingots + 3 Wood
Iron Ingot = 3 Iron Ore + 2 Coal

Total Raw Materials:
- Iron Ore: 6 units
- Coal: 4 units  
- Wood: 3 units

Total Time:
- Smelting: 8 min × 2 = 16 min
- Smithing: 5 min × 1 = 5 min
- Total: 21 minutes (base skill)
```

---

## Sheet 3: Consumption & Durability

### Tool Durability (Uses Before Breaking)

| Tool Type | Durability | Uses/Day (Heavy) | Uses/Day (Moderate) | Lifetime (Days) |
|-----------|------------|------------------|---------------------|-----------------|
| Stone | 20 | 20 | 10 | 1-2 |
| Copper | 50 | 20 | 10 | 3-5 |
| Bronze | 75 | 20 | 10 | 4-8 |
| Iron | 100 | 20 | 10 | 5-10 |
| Steel | 200 | 20 | 10 | 10-20 |

**Formula**: Lifetime = Durability / Uses Per Day

### Food Consumption

| Activity Level | Food/Hour | Food/Day | Weekly Need |
|----------------|-----------|----------|-------------|
| Idle | 0.5 | 12 | 84 |
| Light Work | 0.75 | 18 | 126 |
| Moderate Work | 1.0 | 24 | 168 |
| Heavy Work | 1.5 | 36 | 252 |
| Starvation Threshold | - | - | 72 hours |

**Starvation Effects**:
- 24h without food: Reduced efficiency (-25%)
- 48h without food: Severe penalty (-50%)
- 72h without food: Health decline begins

### Building Decay

| Building Type | Decay Rate | Maintenance Cost | Without Maintenance |
|---------------|------------|------------------|---------------------|
| Basic Shelter | 1%/day | 1 wood/week | Destroyed in 100 days |
| Wooden House | 0.5%/day | 2 wood/week | Destroyed in 200 days |
| Stone Building | 0.2%/day | 1 stone/month | Destroyed in 500 days |
| Metal Structure | 0.1%/day | 1 metal/month | Destroyed in 1000 days |

---

## Sheet 4: Economic Balance

### Supply/Demand Model

| Resource | Daily Supply (per capita) | Daily Demand (per capita) | Surplus/Deficit | Price Elasticity |
|----------|---------------------------|---------------------------|-----------------|------------------|
| Food | 24 units | 18 units | +6 | Low |
| Wood | 60 units | 15 units | +45 | Medium |
| Stone | 30 units | 8 units | +22 | Medium |
| Iron | 6 units | 3 units | +3 | High |
| Tools | 0.1 units | 0.2 units | -0.1 | Very High |

### Wealth Distribution Model

| Population Segment | % of Population | % of Wealth | Gini Contribution |
|--------------------|-----------------|-------------|-------------------|
| Top 10% | 10% | 25% | +0.15 |
| Upper-Middle 20% | 20% | 30% | +0.02 |
| Middle 40% | 40% | 35% | -0.05 |
| Lower-Middle 20% | 20% | 8% | -0.06 |
| Bottom 10% | 10% | 2% | -0.06 |
| **TOTAL** | 100% | 100% | **Gini: 0.32** |

**Target Gini**: 0.30 - 0.40 (moderate inequality)

### Price Belief Formation

```
Agent Price Belief = (Recent Market Average × 0.6) + 
                     (Historical Average × 0.3) + 
                     (Personal Experience × 0.1) +
                     PersonalityBias

Where:
- Recent Market Average: Last 10 transactions
- Historical Average: Last 7 days
- Personal Experience: Last 5 personal transactions
- PersonalityBias: ±20% based on optimism/pessimism
```

---

## Sheet 5: Automation Impact

### Labor Requirements by Tech Level

| Task | Manual Labor | Basic Machine | Powered Machine | Automated | Efficiency Gain |
|------|--------------|---------------|-----------------|-----------|-----------------|
| Mining | 100% | 75% | 40% | 10% | 10x |
| Farming | 100% | 60% | 30% | 5% | 20x |
| Transport | 100% | 80% | 50% | 20% | 5x |
| Crafting | 100% | 70% | 35% | 15% | 7x |
| Building | 100% | 90% | 60% | 30% | 3x |

**Automation Costs**:
- Basic Machine: 2x material cost, 50% time savings
- Powered Machine: 5x material cost, energy required, 70% time savings
- Automated: 20x material cost, high energy, 90% time savings

---

## Sheet 6: Market Balance Calculations

### Price Equilibrium Formula

```
Equilibrium Price = (Average Production Cost × 1.2) × 
                    (Demand / Supply)^0.5 ×
                    Market Sentiment

Where:
- Production Cost: Labor + Materials + Energy
- Demand/Supply: Ratio (1.0 = balanced)
- Market Sentiment: 0.8-1.2 (panic to boom)
```

### Example Price Calculations

| Item | Production Cost | Supply/Demand | Market Sentiment | Equilibrium Price |
|------|----------------|---------------|------------------|-------------------|
| Stone Axe | 50 credits | 1.0 | 1.0 | 60 credits |
| Iron Sword | 200 credits | 0.8 | 1.1 | 264 credits |
| Bread | 5 credits | 1.2 | 0.9 | 5.9 credits |
| Luxury Chair | 500 credits | 0.5 | 1.2 | 720 credits |

---

## Balance Validation Checklist

### Early Game (Days 1-7)
- [ ] Can gather enough food to survive?
- [ ] Tools last reasonable time?
- [ ] Basic crafting accessible?
- [ ] Progression feels achievable?

### Mid Game (Days 7-30)
- [ ] Specialization rewards significant?
- [ ] Trade economically viable?
- [ ] Automation worth the investment?
- [ ] Wealth accumulation possible?

### Late Game (Days 30+)
- [ ] Advanced goods in demand?
- [ ] Market remains stable?
- [ ] Latecomers can catch up?
- [ ] Inflation under control?

---

## Notes for Excel Implementation

**Formulas to Add**:
1. **Resource Generation**: `=BaseRate*(1+(ToolTier*0.5))*(1+(Skill/100))`
2. **Tool Lifetime**: `=Durability/UsesPerDay`
3. **Gini Coefficient**: Use built-in statistical functions or custom formula
4. **Equilibrium Price**: `=ProductionCost*1.2*POWER(Demand/Supply,0.5)*Sentiment`
5. **Supply/Demand Ratio**: `=TotalSupply/TotalDemand`

**Charts to Create**:
1. Resource flow diagram (Sankey chart)
2. Production chain visualization
3. Price history over time
4. Wealth distribution curve
5. Supply vs demand scatter plot

**Conditional Formatting**:
- Red: Deficit resources (Supply < Demand)
- Yellow: Tight balance (0.8 < Ratio < 1.2)
- Green: Surplus (Ratio > 1.2)

**Data Validation**:
- ToolTier: Integer 0-5
- Skill: Integer 0-100
- Sentiment: Decimal 0.8-1.2
- Durability: Positive integer

---

**Instructions for Excel**:
1. Create workbook: "resource-economy-balance.xlsx"
2. Create sheets: "Generation", "Production", "Consumption", "Economic", "Automation", "Market", "Validation"
3. Copy tables into respective sheets
4. Implement formulas as specified
5. Add charts for visualization
6. Use conditional formatting for quick analysis
7. Create summary dashboard on first sheet
