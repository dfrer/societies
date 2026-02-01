# 01: Moment-to-Moment Gameplay

**Time Scale**: 5-15 minutes  
**Focus**: Real-time actions and immediate player decisions  

---

## Overview

This document defines what players actually *do* during short gameplay bursts. These are the atomic actions that form the foundation of all longer gameplay loops.

---

## Core Activity Loop

```mermaid
graph LR
    A[Gather Resources] --> B[Craft Items]
    B --> C[Build/Upgrade]
    C --> D[Trade/Sell]
    D --> E[Check Goals]
    E --> A
    
    style A fill:#bfb,stroke:#333,stroke-width:2px
```

---

## Gathering Resources

### Activities

| Resource | Action | Tool Required | Rate (per min) |
|----------|--------|---------------|----------------|
| Wood | Chop trees | Axe | 10/min |
| Stone | Mine rocks | Pickaxe | 8/min |
| Ore | Deep mining | Pickaxe | 5/min |
| Food | Harvest plants | None | 12/min |
| Meat | Hunt animals | Bow/Spear | 6/min |
| Fish | Fishing | Rod | 8/min |
| Water | Collect from source | Bucket | 15/min |

### Feedback Loops

- **Resource counter increases**: Visual + audio feedback (satisfying sounds)
- **Tool durability decreases**: Maintenance loop creates planning depth
- **Skill XP gain**: Progression reinforcement
- **Inventory management decisions**: Weight/capacity constraints

### Fun Factors

- **Visual/audio feedback**: Satisfying chop sounds, particle effects
- **Resource rarity**: Excitement finding rare ore or special wood types
- **Efficiency optimization**: Better tools = faster gathering, skills improve speed
- **Environmental discovery**: Finding new resource patches

---

## Crafting Items

### Activities

1. Open crafting menu
2. Select recipe (filtered by available materials)
3. Ensure materials available
4. Execute craft action
5. Quality/variance based on skill level

### Feedback Loops

- **Inventory changes**: Immediate visual update
- **Skill XP gain**: Clear progression feedback
- **New capabilities unlocked**: Tool quality affects future possibilities
- **Quality rating**: Pride in workmanship (visible on crafted item)

### Crafting Categories

| Category | Examples | Time Required |
|----------|----------|---------------|
| Tools | Axe, Pickaxe, Hammer | 2-5 seconds |
| Materials | Planks, Ingots, Cloth | 3-8 seconds |
| Consumables | Food, Bandages | 1-3 seconds |
| Components | Gears, Circuits | 5-10 seconds |

---

## Building Structures

### Activities

1. Select building type from construction menu
2. Choose placement location (with preview)
3. Place foundation (requires materials)
4. Add materials progressively
5. See construction progress visual
6. Finished structure provides benefit

### Satisfaction Sources

- **Visual transformation**: Empty lot → house/workshop (permanent change)
- **Functional benefit**: Shelter, storage, crafting stations
- **Aesthetic expression**: Design choices (colors, materials)
- **Permanent impact on world**: Others can see and use your buildings

### Build Types by Time

| Structure | Time Investment | Materials |
|-----------|----------------|-----------|
| Small storage chest | 30 seconds | 10 wood |
| Basic shelter | 5-10 minutes | 50 wood, 20 stone |
| Workshop | 30-60 minutes | 100 wood, 50 stone, 20 metal |
| Town building | 2-4 hours | 500+ resources |

---

## Technical Integration

### Performance Compliance

All moment-to-moment actions respect **Session 1 Technical Constraints**:

| Constraint | Value | Impact |
|------------|-------|--------|
| Tick Rate | 20 TPS (50ms) | All actions complete within 50ms tick |
| Resource Gathering | 1 unit per 6 seconds | 1 unit per 120 ticks (intentionally slow) |
| Crafting Actions | Immediate | Single tick completion |
| Building Placement | 1-2 ticks | Position check + placement validation |

**Bandwidth Usage**: ~4 KB/s for all moment-to-moment updates (within 32 KB/s budget)

### Session 2 AI Integration

- **Resource competition**: AI agents also gather resources (Session 2 pathfinding)
- **Trade opportunities**: AI agents evaluate player prices during gathering breaks
- **Environmental reaction**: AI agents respond to resource depletion

---

## Navigation

- [Session 3 Index](./[AGENTS-READ-FIRST]-index.md)
- [02: Session Gameplay](./02-session-gameplay.md) → (30 min - 2 hour loops)
- [RESEARCH-INDEX.md](./RESEARCH-INDEX.md) - Research sources

---

## Cross-References

- **Technical Constraints**: See [Session 1: 04-performance-scalability.md](../session-1-technical-architecture/04-performance-scalability.md)
- **AI Resource Behavior**: See [Session 2: 02-economic-behavior.md](../session-2-ai-system-design/02-economic-behavior.md)
- **Building Systems**: See [Session 4: Construction Systems](../session-4-progression-and-balance/)
