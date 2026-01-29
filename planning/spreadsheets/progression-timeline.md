# Progression Timeline Spreadsheet

This document serves as a template for the Excel spreadsheet with tech tree, milestones, and timeline calculations.

## Sheet 1: Technology Tree

| Tech Name | Age | Prerequisites | Research Cost | Research Time | Unlock Cost | Tier |
|-----------|-----|---------------|---------------|---------------|-------------|------|
| Stone Tools | Stone | None | 10 XP | 10 min | 50 credits | 1 |
| Fire | Stone | None | 15 XP | 15 min | 30 credits | 1 |
| Basic Farming | Stone | Stone Tools | 25 XP | 30 min | 100 credits | 2 |
| Copper Tools | Copper | Stone Tools + Fire | 50 XP | 1 hour | 200 credits | 3 |
| Animal Husbandry | Copper | Basic Farming | 40 XP | 45 min | 150 credits | 3 |
| Bronze Tools | Bronze | Copper Tools × 2 | 100 XP | 2 hours | 500 credits | 4 |
| Writing | Bronze | Bronze Tools | 80 XP | 1.5 hours | 300 credits | 4 |
| Iron Tools | Iron | Bronze Tools + Mining | 200 XP | 4 hours | 1000 credits | 5 |
| Advanced Farming | Iron | Iron Tools | 150 XP | 3 hours | 800 credits | 5 |
| Steel | Medieval | Iron Tools × 3 | 400 XP | 8 hours | 2500 credits | 6 |
| Windmill | Medieval | Steel | 300 XP | 6 hours | 2000 credits | 6 |
| Steam Power | Industrial | Steel + Coal Mining | 800 XP | 1 day | 5000 credits | 7 |
| Mass Production | Industrial | Steam Power | 600 XP | 18 hours | 4000 credits | 7 |
| Electronics | Modern | Steam Power × 2 | 1500 XP | 2 days | 10000 credits | 8 |
| Computing | Modern | Electronics | 1200 XP | 1.5 days | 8000 credits | 8 |
| Rocketry | Space | Electronics × 3 | 3000 XP | 3 days | 20000 credits | 9 |
| Life Support | Space | Rocketry | 2500 XP | 2.5 days | 15000 credits | 9 |
| Planetary Defense | Space | Rocketry + Computing | 4000 XP | 4 days | 30000 credits | 10 |

### Tech Tree Dependencies (Visual)

```
Stone Age (Tier 1-2):
  Stone Tools → Basic Farming
  Fire → Cooking

Copper Age (Tier 3):
  Stone Tools + Fire → Copper Tools
  Basic Farming → Animal Husbandry

Bronze Age (Tier 4):
  Copper Tools (×2) → Bronze Tools
  Bronze Tools → Writing

Iron Age (Tier 5):
  Bronze Tools + Mining → Iron Tools
  Iron Tools → Advanced Farming

Medieval (Tier 6):
  Iron Tools (×3) → Steel
  Steel → Windmill

Industrial (Tier 7):
  Steel + Coal Mining → Steam Power
  Steam Power → Mass Production

Modern (Tier 8):
  Steam Power (×2) → Electronics
  Electronics → Computing

Space Age (Tier 9-10):
  Electronics (×3) → Rocketry
  Rocketry → Life Support
  Rocketry + Computing → Planetary Defense
```

---

## Sheet 2: Server Lifecycle Timeline

| Day | Phase | Key Events | Population | Tech Level | Focus |
|-----|-------|------------|------------|------------|-------|
| 1 | Survival | Spawn, basic tools | 20-30 | Stone | Establishment |
| 3 | Survival | First shelter | 25-35 | Stone | Food security |
| 7 | Establishment | Neighborhood forms | 30-45 | Copper | Specialization |
| 10 | Establishment | First trade routes | 40-55 | Copper | Economy |
| 15 | Growth | Town formation | 50-70 | Bronze | Governance |
| 20 | Growth | Industry begins | 65-85 | Iron | Infrastructure |
| 25 | Crisis Prep | Meteor detected | 80-100 | Iron/Steel | Defense prep |
| 30 | Crisis | Meteor impact | 60-80 (post) | Steel | Recovery |
| 35 | Recovery | Cleanup begins | 70-90 | Steel | Rebuilding |
| 45 | Recovery | Ecosystem healing | 90-110 | Industrial | Environment |
| 60 | Advanced | New threats emerge | 110-130 | Modern | Research |
| 90 | Late Game | Space program | 140-160 | Space | Expansion |
| 120 | Endgame | Interstellar prep | 160-180 | Space | Legacy |

### Milestone Checkpoints

| Checkpoint | Target Day | Success Criteria | Failure Recovery |
|------------|------------|------------------|------------------|
| Food Security | Day 3 | No starvation | Emergency aid system |
| Basic Tools | Day 5 | 80% have copper+ tools | Shared tool library |
| Town Founded | Day 15 | 3+ citizens, constitution | Auto-town formation |
| Industrial Base | Day 25 | Iron production running | AI assistance boost |
| Meteor Defeated | Day 30 | 60%+ success rate | Reduced difficulty |
| Recovery | Day 40 | 80% infrastructure restored | Accelerated repair |
| Space Ready | Day 90 | Rocketry researched | Extended timeline |

---

## Sheet 3: Skill Progression

| Skill Level | XP Required | Total XP | Time to Achieve | Capability |
|-------------|-------------|----------|-----------------|------------|
| Beginner | 0 | 0 | 0 hours | Basic tasks, 50% efficiency |
| Novice | 100 | 100 | 2-3 hours | Standard tasks, 75% efficiency |
| Apprentice | 250 | 350 | 6-8 hours | All basic tasks, 90% efficiency |
| Journeyman | 500 | 850 | 15-20 hours | Complex tasks, 100% efficiency |
| Expert | 1000 | 1850 | 40-50 hours | Advanced tasks, 125% efficiency |
| Master | 2000 | 3850 | 80-100 hours | Innovation, 150% efficiency, can teach |

### Skill Categories

| Category | Specializations | Crossover Skills |
|----------|----------------|------------------|
| Gathering | Mining, Forestry, Farming, Hunting | All use Tool Skills |
| Crafting | Smithing, Masonry, Carpentry, Cooking | All use Crafting base |
| Social | Trading, Leadership, Teaching, Diplomacy | All use Charisma |
| Knowledge | Research, Medicine, Engineering, Science | All use Intellect |

### Catch-up Mechanics

| Situation | Bonus XP | Mechanism |
|-----------|----------|-----------|
| Learning from Master | +50% | Must be within 10 levels |
| Group Training | +25% | 3+ students |
| Tutorial Mode | +100% | First time only |
| Late Joiner | +200% for first week | Server age > 30 days |
| Mentorship Program | +30% | Both mentor and student |

---

## Sheet 4: Threat Timeline

| Threat | Trigger Day | Preparation Window | Success Rate Target | Failure Consequence |
|--------|-------------|-------------------|---------------------|---------------------|
| Meteor | 30 | Days 25-30 | 60% | 50% destruction, 20-40% population loss |
| Environmental Reckoning | 30-60 (gradual) | Days 1-30 (prevention) | N/A | Crop failures, species loss |
| Resource Depletion | 45+ | Ongoing | N/A | Scarcity, price spikes |
| Pandemic | 60-90 | Days 50-60 | 70% | 10-30% population, productivity loss |
| Climate Shift | 80+ | Days 1-80 (mitigation) | N/A | Biome changes, infrastructure damage |
| External Invasion | 100+ | Days 90-100 | 65% | Conflict, resource drain |

### Threat Difficulty Scaling

| Server Size | Meteor HP | Resource Multiplier | AI Agents | Difficulty Modifier |
|-------------|-----------|---------------------|-----------|---------------------|
| Tiny (1-5) | 50% | 2.0x | 50 | -30% |
| Small (6-15) | 75% | 1.5x | 100 | -15% |
| Medium (16-30) | 100% | 1.0x | 150 | 0% |
| Large (31-50) | 125% | 0.8x | 200 | +15% |
| Massive (50+) | 150% | 0.6x | 250 | +30% |

---

## Sheet 5: Population Scaling

### Population Growth Formula

```
Daily Growth = BaseGrowth + EconomicStimulus + PlayerBonus

Where:
- BaseGrowth = 1 agent per day
- EconomicStimulus = GDP_Growth_Rate × 10
- PlayerBonus = NewPlayers × 2
- Cap = 200 agents (performance limit)
```

### Population by Phase

| Phase | Days | Base Population | With Elasticity | Human Players | Total |
|-------|------|----------------|-----------------|---------------|-------|
| Spawn | 1 | 20 | 20 | 5 | 25 |
| Week 1 | 7 | 26 | 30 | 8 | 38 |
| Week 2 | 14 | 33 | 45 | 12 | 57 |
| Week 3 | 21 | 41 | 65 | 15 | 80 |
| Pre-Meteor | 30 | 50 | 90 | 20 | 110 |
| Post-Meteor | 35 | 45 | 70 | 18 | 88 |
| Recovery | 45 | 51 | 95 | 22 | 117 |
| Mid Game | 60 | 66 | 120 | 28 | 148 |
| Late Game | 90 | 96 | 155 | 35 | 190 |
| Endgame | 120+ | 126+ | 180+ | 40+ | 220+ |

### Population Pressure Indicators

| Indicator | Normal | Warning | Critical |
|-----------|--------|---------|----------|
| Food per capita | >20 units/day | 10-20 units/day | <10 units/day |
| Housing ratio | >0.8 | 0.6-0.8 | <0.6 |
| Employment rate | >90% | 70-90% | <70% |
| Resource scarcity | <20% | 20-40% | >40% |
| Migration rate | <1% | 1-5% | >5% |

---

## Sheet 6: Economic Progression

### Wealth Targets by Phase

| Phase | Days | Avg Wealth per Capita | Wealth Distribution | Currency in Circulation |
|-------|------|----------------------|---------------------|------------------------|
| Early | 1-7 | 200-500 | Balanced | 10,000 |
| Growth | 8-30 | 500-2000 | Emerging gap | 50,000 |
| Industrial | 31-60 | 2000-10000 | Moderate inequality | 200,000 |
| Advanced | 61-120 | 10000-50000 | Higher inequality | 1,000,000 |
| Endgame | 120+ | 50000+ | Variable | 5,000,000+ |

### Market Maturity Stages

| Stage | Days | Characteristics | Price Stability |
|-------|------|----------------|-----------------|
| Barter | 1-3 | Direct exchange | N/A |
| Early Market | 4-15 | Simple currency, limited goods | High volatility |
| Growth Market | 16-45 | Diverse goods, active trading | Moderate volatility |
| Mature Market | 46-90 | Complex economy, specialization | Low volatility |
| Advanced Economy | 90+ | Financial instruments, futures | Very stable |

---

## Sheet 7: Configuration Options

### Server Settings

| Setting | Casual | Normal | Hardcore | Custom Range |
|---------|--------|--------|----------|--------------|
| Meteor Day | 45 | 30 | 20 | 15-60 |
| Resource Abundance | 2.0x | 1.0x | 0.5x | 0.3-3.0x |
| AI Difficulty | Passive | Balanced | Aggressive | Configurable |
| Death Penalty | None | Inventory loss | Character death | Selectable |
| Research Speed | 2.0x | 1.0x | 0.5x | 0.25-2.0x |
| Pollution Rate | 0.5x | 1.0x | 2.0x | 0.25-3.0x |
| XP Gain | 2.0x | 1.0x | 0.5x | 0.25-2.0x |
| Permadeath | Off | Off | On | Toggle |

### Game Mode Presets

| Mode | Description | Best For |
|------|-------------|----------|
| **Tutorial** | Extended timers, guidance, forgiving | New players |
| **Casual** | Relaxed pace, abundant resources, low stakes | Exploration |
| **Normal** | Balanced challenge, intended experience | Most players |
| **Hardcore** | Fast threats, scarce resources, permadeath | Veterans |
| **Sandbox** | No threats, infinite resources, creative | Builders |
| **Speed Run** | Compressed timeline, leaderboard | Competition |

---

## Excel Implementation Notes

### Key Formulas

1. **Tech Total Cost**: `=SUM(Prerequisites)+ResearchCost+UnlockCost`
2. **Time to Master**: `=SUM(XP_Required_Range)/XP_Per_Hour`
3. **Population Growth**: `=PreviousDay+BaseGrowth+(GDP_Growth*10)+(NewPlayers*2)`
4. **Threat HP**: `=BaseHP*DifficultyModifier*(ServerSizeMultiplier)`
5. **Days to Tech**: `=ResearchTime/ResearchSpeedSetting`

### Charts to Create

1. **Tech Tree**: Network diagram showing dependencies
2. **Timeline**: Gantt chart of phases and events
3. **Population Growth**: Exponential curve with caps
4. **Skill Progression**: S-curve showing diminishing returns
5. **Threat Calendar**: Calendar view with warning indicators
6. **Wealth Distribution**: Lorenz curve showing inequality

### Conditional Formatting

- **Red**: Critical phases (Meteor, Crisis)
- **Yellow**: Warning phases (Prep periods)
- **Green**: Safe/Growth phases
- **Blue**: Recovery phases

### Data Validation

- Days: Integer 1-365
- XP: Integer 0-10000
- Population: Integer 0-250
- Difficulty: Decimal 0.3-3.0
- Percentages: 0-100%

---

**Instructions for Excel**:
1. Create workbook: "progression-timeline.xlsx"
2. Create sheets: "TechTree", "Lifecycle", "Skills", "Threats", "Population", "Economy", "Config"
3. Implement formulas as specified
4. Add dropdown lists for configuration options
5. Create timeline visualization with conditional formatting
6. Add summary dashboard showing key milestones
7. Create pivot tables for analysis
