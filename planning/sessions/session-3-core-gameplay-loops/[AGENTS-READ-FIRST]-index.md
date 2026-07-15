# Session 3: Core Gameplay Loops

**Status**: COMPLETE - Compartmentalized  
**Last Updated**: 2026-02-01  

> **Canonical alignment (2026-07-14):** Aspirational gameplay reference. Current delivery is governed by [`planning/active/`](../../active/) and [`CURRENT_BUILD.md`](../../../CURRENT_BUILD.md); see [PRODUCT-THESIS.md](../../PRODUCT-THESIS.md).

---

## Quick Access

### Core Documents

| Document | Description | Lines | Focus |
|----------|-------------|-------|-------|
| [01-moment-to-moment-gameplay.md](./01-moment-to-moment-gameplay.md) | 5-15 minute gameplay loop | ~200 | Real-time actions |
| [02-session-gameplay.md](./02-session-gameplay.md) | 30 min - 2 hour sessions | ~250 | Session structure |
| [03-multi-session-arcs.md](./03-multi-session-arcs.md) | Days to weeks progression | ~220 | Long-term arcs |
| [04-player-archetypes.md](./04-player-archetypes.md) | 6 player types | ~300 | Play styles |
| [05-progression-feel.md](./05-progression-feel.md) | Emotional journey | ~180 | Growth pacing |
| [06-return-triggers.md](./06-return-triggers.md) | Engagement mechanics | ~200 | Retention |
| [07-ui-ux-paths.md](./07-ui-ux-paths.md) | Interface design | ~200 | User experience |
| [RESEARCH-INDEX.md](./RESEARCH-INDEX.md) | Research sources | ~50 | References |

### Detailed System Specifications (01-Series)

| Document | Description | Lines | Dependencies |
|----------|-------------|-------|--------------|
| [01-gameplay-systems-architecture.md](./01-gameplay-systems-architecture.md) | Technical architecture foundation | ~250 | Core systems design |
| [01b-inventory-crafting-recipes.md](./01b-inventory-crafting-recipes.md) | Crafting recipes and item definitions | ~300 | Inventory system |
| [01c-movement-interaction-spec.md](./01c-movement-interaction-spec.md) | Player movement and interaction mechanics | ~250 | Core gameplay |
| [01d-tool-system-spec.md](./01d-tool-system-spec.md) | Tool durability, progression, and usage | ~280 | Gathering system |
| [01e-inventory-system-spec.md](./01e-inventory-system-spec.md) | Inventory management and storage | ~320 | UI/UX |
| [01f-voxel-interaction-spec.md](./01f-voxel-interaction-spec.md) | Mining, building, block placement | ~350 | World modification |
| [01g-entity-block-system.md](./01g-entity-block-system.md) | Custom entities: carts, workshops, vehicles | ~400 | Hybrid block/entity system |

### Support Systems

| Document | Description | Lines | Integration |
|----------|-------------|-------|-------------|
| [02b-economic-system-spec.md](./02b-economic-system-spec.md) | Economic mechanics and markets | ~300 | Session gameplay |
| [02c-governance-mechanics-detail.md](./02c-governance-mechanics-detail.md) | Political system details | ~280 | Governance |
| [07b-screen-specifications.md](./07b-screen-specifications.md) | UI screen layouts and flows | ~250 | Interface design |
| [03-weight-carrying-system.md](./03-weight-carrying-system.md) | Weight and encumbrance mechanics | ~280 | Movement, logistics |
| [04-entity-catalog.md](./04-entity-catalog.md) | Complete game entity reference | ~500 | All entity systems |

---

## Session Overview

Session 3 defines what players actually *do* across different time scales:

1. **Moment-to-Moment** (5-15 min): Gathering, crafting, building
2. **Session Level** (30 min - 2 hours): Projects, economic, political activities
3. **Multi-Session** (Days to weeks): Progression arcs, server lifecycle

### Key Questions Answered

- What does a typical 15-minute play session look like?
- What does a 2-hour session look like?
- What does week 1 vs. week 4 feel like?
- How do different player types experience the game?
- What makes logging in tomorrow compelling?
- What's fun moment-to-moment vs. satisfying long-term?

---

## Dependencies

- **Requires**: 
  - [Session 1](../session-1-technical-architecture/) - Technical constraints (20 TPS, agent limits, bandwidth)
  - [Session 2](../session-2-ai-system-design/) - AI behaviors, economic systems, political systems
- **Informs**: 
  - [Session 4](../session-4-progression-and-balance/) - Progression systems
  - [Session 5](../session-5-governance-mechanics/) - Governance Mechanics implementation
  - [Session 6](../session-6-prototyping-roadmap/) - Prototype scope

---

## Player Archetypes

This session defines 6 core player types:

1. **Builder** - Construction, aesthetics, megaprojects
2. **Economist** - Trading, markets, optimization
3. **Politician** - Governance, leadership, influence
4. **Environmentalist** - Stewardship, sustainability
5. **Engineer** - Automation, efficiency, systems
6. **Socializer** - Community, relationships, events

See [04-player-archetypes.md](./04-player-archetypes.md) for full profiles.

---

## System Architecture

### Core Systems (01-Series Documents)

The 01-series provides detailed technical specifications for core gameplay systems:

**Foundation & Architecture:**
- **[01-gameplay-systems-architecture.md](./01-gameplay-systems-architecture.md)** - System relationships, tick architecture, state management

**Player Capabilities:**
- **[01c-movement-interaction-spec.md](./01c-movement-interaction-spec.md)** - Movement, camera, interaction modes
- **[01d-tool-system-spec.md](./01d-tool-system-spec.md)** - Tool types, durability, progression

**World Interaction:**
- **[01f-voxel-interaction-spec.md](./01f-voxel-interaction-spec.md)** - Mining, building, block placement/removal
  - Block interaction mechanics (left/right click)
  - Mining with tool integration and timing
  - Building with placement validation
  - Multiplayer synchronization
  - Weight/encumbrance integration

**Entity Systems:**
- **[01g-entity-block-system.md](./01g-entity-block-system.md)** - Hybrid static/dynamic object system
  - When to use blocks vs entities (decision matrix)
  - Entity types: vehicles, containers, workshops, furniture
  - Physics-based interactions (RigidBody3D)
  - State persistence and ownership

**Inventory & Logistics:**
- **[01b-inventory-crafting-recipes.md](./01b-inventory-crafting-recipes.md)** - Item definitions and crafting recipes
- **[01e-inventory-system-spec.md](./01e-inventory-system-spec.md)** - Inventory management UI and mechanics
- **[03-weight-carrying-system.md](./03-weight-carrying-system.md)** - Weight and encumbrance system
  - Realistic kg/m³ densities (70+ block types)
  - Player capacity (100 kg base)
  - Movement speed impact (0-100% encumbrance)
  - Vehicle transport (200 kg - 10,000 kg capacity)
  - Strategic settlement location consequences

### Entity Catalog

**[04-entity-catalog.md](./04-entity-catalog.md)** - Complete reference for all game entities:
- Transportation entities (minecarts, carts, vehicles)
- Crafting/production entities (workshops, furnaces)
- Storage entities (chests, crates, containers)
- Furniture/decorative (beds, chairs, tables)
- Infrastructure (doors, mechanisms, platforms)
- Development priorities (MVP vs Post-MVP)
- Art and technical specifications

---

## Technical Validation

All gameplay loops validated against Session 1 constraints:

| Constraint | Value | Status |
|------------|-------|--------|
| Tick Rate | 20 TPS (50ms) | ✅ All actions fit |
| Agent Limit | 25-100 | ✅ Respected |
| Per-Agent Budget | <2ms | ✅ Within budget |
| Bandwidth | 32 KB/s | ✅ ~4-20 KB/s |

See individual documents for detailed technical integration.

---

## Success Criteria

All criteria met:

### Core Gameplay Documents
- [x] Clear minute-to-minute activity flow ([01](./01-moment-to-moment-gameplay.md))
- [x] Session goals defined for different player types ([02](./02-session-gameplay.md))
- [x] Progression feel articulated across timeline ([05](./05-progression-feel.md))
- [x] Return triggers identified ([06](./06-return-triggers.md))
- [x] Critical UI/UX paths mapped ([07](./07-ui-ux-paths.md))
- [x] Information architecture specified ([07](./07-ui-ux-paths.md))
- [x] Player archetypes fully defined ([04](./04-player-archetypes.md))

### Detailed System Specifications
- [x] Gameplay systems architecture defined ([01a](./01-gameplay-systems-architecture.md))
- [x] Movement and interaction mechanics specified ([01c](./01c-movement-interaction-spec.md))
- [x] Tool system with durability and progression ([01d](./01d-tool-system-spec.md))
- [x] Inventory system with UI/UX ([01e](./01e-inventory-system-spec.md))
- [x] Voxel interaction (mining/building) documented ([01f](./01f-voxel-interaction-spec.md))
- [x] Entity-block system hybrid approach ([01g](./01g-entity-block-system.md))
- [x] Weight and encumbrance mechanics ([weight-carrying](./03-weight-carrying-system.md))
- [x] Complete entity catalog ([entity-catalog](./04-entity-catalog.md))

---

## Status Explanation

- **READY FOR REVIEW**: Document is complete and awaiting review
- **IN PROGRESS**: Currently being edited or updated
- **COMPLETED**: Finalized and approved
- **COMPARTMENTALIZED**: Broken into focused sub-documents (this session)

---

## Document Navigation

### Sequential Reading Path
1. Start: [01-gameplay-systems-architecture.md](./01-gameplay-systems-architecture.md)
2. Foundation: [01c-movement-interaction-spec.md](./01c-movement-interaction-spec.md)
3. Tools: [01d-tool-system-spec.md](./01d-tool-system-spec.md)
4. World Interaction: [01f-voxel-interaction-spec.md](./01f-voxel-interaction-spec.md)
5. Hybrid System: [01g-entity-block-system.md](./01g-entity-block-system.md)
6. Logistics: [03-weight-carrying-system.md](./03-weight-carrying-system.md)
7. Reference: [04-entity-catalog.md](./04-entity-catalog.md)

### Cross-Reference Map

**Voxel Interaction System (01f)** references:
- [01c-movement-interaction-spec.md](./01c-movement-interaction-spec.md) - Interaction modes
- [01d-tool-system-spec.md](./01d-tool-system-spec.md) - Tool integration
- [01e-inventory-system-spec.md](./01e-inventory-system-spec.md) - Weight/encumbrance
- [03-weight-carrying-system.md](./03-weight-carrying-system.md) - Block weights

**Entity-Block System (01g)** references:
- [01e-inventory-system-spec.md](./01e-inventory-system-spec.md) - Container entities
- [04-entity-catalog.md](./04-entity-catalog.md) - Full entity specifications
- [01f-voxel-interaction-spec.md](./01f-voxel-interaction-spec.md) - Block interaction

**Weight System** references:
- [01f-voxel-interaction-spec.md](./01f-voxel-interaction-spec.md) - Mining/building weight
- [01g-entity-block-system.md](./01g-entity-block-system.md) - Vehicle transport
- [04-entity-catalog.md](./04-entity-catalog.md) - Entity weights

---

## Session Navigation

- **Previous:** [Session 2: AI System Design](../session-2-ai-system-design/[AGENTS-READ-FIRST]-index.md)
- **Next:** [Session 4: Progression and Balance](../session-4-progression-and-balance/[AGENTS-READ-FIRST]-index.md)
- **Master Index:** [Planning Directory](../[AGENTS-READ-FIRST]-index.md)

---

## Legacy Document

The original single comprehensive document has been compartmentalized into the documents above and moved to the archives for reference.

- **Archived**: [../../archives/00-day3-legacy.md](../../archives/00-day3-legacy.md) (~1,050 lines, deprecated)
- **Current**: Use the numbered documents above
