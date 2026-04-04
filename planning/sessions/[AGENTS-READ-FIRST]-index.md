# Societies Planning - Master Sessions Index

> Prototype reality note (2026-04-03): the authoritative implementation is currently the Godot prototype under `src/societies/`. The planning documents below include long-term and aspirational material that is not yet implemented in code.

> **PROJECT**: Societies - AI-Powered Society Simulation Game  
> **STATUS**: 🟢 Active Development  
> **LAST UPDATED**: 2026-02-01  
> **VERSION**: v1.1.0 (Voxel World Integration Update)

---

## 📋 Quick Navigation

| Session | Title | Status | Content Type | Line Count | Last Updated |
|---------|-------|--------|--------------|------------|--------------|
| [Session 1](#session-1-technical-architecture) | Technical Architecture | ✅ COMPLETE | Compartmentalized | **~2,400** | 2026-02-01 |
| [Session 2](#session-2-ai-system-design) | AI System Design | ✅ COMPLETE | Compartmentalized | ~1,200 | 2026-01-25 |
| [Session 3](#session-3-core-gameplay-loops) | Core Gameplay Loops | ✅ COMPLETE | Compartmentalized | **~1,850** | 2026-02-01 |
| [Session 4](#session-4-progression--balance) | Progression & Balance | ✅ COMPLETE | Mixed | **~1,350** | 2026-02-01 |
| [Session 5](#session-5-governance-mechanics) | Governance Mechanics | ✅ COMPLETE | Mixed | **~1,350** | 2026-02-01 |
| [Session 6](#session-6-prototyping-roadmap) | Prototyping Roadmap | 📝 CONTENT READY | Single File | ~880 | 2026-01-30 |
| [Session 7](#session-7-integration-master-plan) | Integration Master Plan | 📝 CONTENT READY | Single File | ~1,020 | 2026-01-31 |

**TOTAL PLANNING CONTENT**: **~9,050 lines** across 7 comprehensive sessions (including voxel world documentation)

---

## 🏗️ Core Technical Pillars

Societies is built on four interconnected technical pillars that form the foundation of the simulation:

```
┌─────────────────────────────────────────────────────────────┐
│                    SOCIETIES ARCHITECTURE                    │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│   │   VOXEL      │  │      AI      │  │   ECONOMY    │     │
│   │   WORLD      │  │   SYSTEMS    │  │   & TRADE    │     │
│   │              │  │              │  │              │     │
│   │ • 1m³ blocks │  │ • GOAP/      │  │ • Weight-    │     │
│   │ • Terrain    │  │   Utility AI │  │   based      │     │
│   │ • Mining     │  │ • 25-100     │  │   logistics  │     │
│   │ • Building   │  │   agents     │  │ • Dynamic    │     │
│   │ • 0.5-2 km²  │  │ • Personality│  │   markets    │     │
│   │   worlds     │  │ • BDI arch   │  │ • Crafting   │     │
│   │              │  │              │  │              │     │
│   └──────┬───────┘  └──────┬───────┘  └──────┬───────┘     │
│          │                 │                 │              │
│          └─────────────────┼─────────────────┘              │
│                            │                                │
│                   ┌────────┴────────┐                       │
│                   │    GOVERNANCE   │                       │
│                   │                 │                       │
│                   │ • 3D Land Claims│                       │
│                   │ • Voting        │                       │
│                   │ • Laws          │                       │
│                   │ • Jurisdiction  │                       │
│                   │                 │                       │
│                   └─────────────────┘                       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Voxel World System (NEW)

The **Voxel World System** is the foundational spatial layer enabling block-based gameplay for resource gathering, construction, and terrain modification.

**Key Specifications**:
- **Block Size**: 1m³ (real-world scale matching Eco)
- **Chunk Size**: 16×16×256 blocks (65,536 blocks per chunk, 256 KB memory)
- **World Height**: -200 to +56 (256 total blocks, bedrock at bottom)
- **World Size**: 0.5 km² (MVP) → 2 km² (post-MVP)
- **Block Format**: 4 bytes (2-byte ID + metadata + lighting)

**Voxel Documentation**:
- **Session 1**: Core technical implementation (13-18)
- **Session 3**: Player interaction and entities (01f, 01g)
- **Session 4**: Resource distribution and gathering
- **Session 5**: 3D jurisdiction and land claims

---

## 📖 Session Status Legend

| Symbol | Status | Description |
|--------|--------|-------------|
| ✅ | **COMPLETE** | Fully compartmentalized, indexed, and ready for implementation. All files follow naming conventions with internal indices. |
| 📝 | **CONTENT READY** | Single consolidated file with comprehensive content. Ready for implementation but not yet compartmentalized into sub-files. |
| 🔄 | **IN PROGRESS** | Currently being actively developed or reviewed. |
| ⏳ | **PLANNED** | Scheduled for future development. |
| 📦 | **ARCHIVED** | Legacy content moved to archive folder. |

---

## 🚀 Getting Started

### For Implementation Teams
1. **Start Here**: Read Session 1 (Technical Architecture) for system foundations
2. **Voxel World**: Reference new docs 13-18 for world generation and modification
3. **Core Logic**: Review Session 3 (Gameplay Loops) for mechanics implementation
4. **AI Systems**: Reference Session 2 (AI Design) for agent behavior algorithms
5. **Balance**: Check Session 4 for progression formulas and tuning values
6. **Roadmap**: Follow Session 6 for phased implementation priorities

### For New Team Members
1. Read this index file completely
2. Check the [Session Dependencies](#session-dependencies) diagram
3. Review Session 1's architecture overview (especially docs 13-18 for voxel world)
4. Read Session 7 for the complete integration picture
5. Consult individual session indices for detailed navigation

---

## 🔄 Session Dependencies

```
                    ┌─────────────────────────────────────┐
                    │     Session 1: Technical Arch       │
                    │         (Foundation Layer)          │
                    │    ┌──────────────────────────┐    │
                    │    │  Voxel World System      │    │
                    │    │  (Docs 13-18)            │    │
                    │    └──────────────────────────┘    │
                    └──────────────┬──────────────────────┘
                                   │
                                   ▼
                    ┌─────────────────────────────────────┐
                    │    Session 2: AI System Design      │
                    │        (Intelligence Layer)         │
                    └──────────────┬──────────────────────┘
                                   │
                                   ▼
        ┌──────────────────────────┼──────────────────────────┐
        │                          │                          │
        ▼                          ▼                          ▼
┌───────────────┐      ┌─────────────────────┐      ┌───────────────────┐
│ Session 3:    │      │ Session 4:          │      │ Session 5:        │
│ Core Gameplay │      │ Progression &       │      │ Governance        │
│ Loops         │      │ Balance             │      │ Mechanics         │
│(Mechanics)    │      │(Systems)            │      │(Meta-Game)        │
│ • Voxel       │      │ • World Resources   │      │ • 3D Jurisdiction │
│   Interaction │      │ • Resource Tiers    │      │ • Land Claims     │
│ • Weight      │      │ • Progression       │      │ • Vertical Space  │
│   Carrying    │      │   Curves            │      │   Governance      │
└───────┬───────┘      └──────────┬──────────┘      └─────────┬─────────┘
        │                         │                         │
        └─────────────────────────┼─────────────────────────┘
                                  ▼
                    ┌─────────────────────────────────────┐
                    │  Session 6: Prototyping Roadmap     │
                    │      (Implementation Plan)          │
                    └──────────────┬──────────────────────┘
                                   │
                                   ▼
                    ┌─────────────────────────────────────┐
                    │ Session 7: Integration Master Plan  │
                    │       (Integration & Delivery)      │
                    └─────────────────────────────────────┘
```

### Dependency Summary

| Session | Depends On | Provides To |
|---------|------------|-------------|
| Session 1 | None (Foundation) | Sessions 2, 3, 4, 5, 6, 7 |
| Session 2 | Session 1 | Sessions 3, 5, 6, 7 |
| Session 3 | Sessions 1, 2 | Sessions 4, 6, 7 |
| Session 4 | Sessions 1, 2, 3 | Sessions 6, 7 |
| Session 5 | Sessions 1, 2 | Sessions 6, 7 |
| Session 6 | Sessions 1, 2, 3, 4, 5 | Session 7 |
| Session 7 | Sessions 1-6 | Implementation Phase |

### Voxel World Cross-Session Integration

The Voxel World System spans multiple sessions with specific dependencies:

| Component | Session | Document | Depends On | Used By |
|-----------|---------|----------|------------|---------|
| Core Voxel System | 1 | 13-voxel-world-system.md | — | All gameplay |
| Terrain Generation | 1 | 14-terrain-generation.md | 13 | World creation |
| Terrain Modification | 1 | 15-terrain-modification.md | 13, 14 | Gameplay loops |
| World Persistence | 1 | 16-world-persistence.md | 13 | Save/Load |
| Rendering & Meshing | 1 | 17-rendering-meshing.md | 13 | Visuals |
| Physics & Collision | 1 | 18-physics-collision.md | 13 | Movement |
| Voxel Interaction | 3 | 01f-voxel-interaction-spec.md | 13, 18 | Mining/Building |
| Entity-Block System | 3 | 01g-entity-block-system.md | 13, 18 | Complex objects |
| Weight Carrying | 3 | weight-carrying-system.md | 13 | Logistics |
| Entity Catalog | 3 | comprehensive-entity-catalog.md | 01g | Art/Design |
| World Resources | 4 | 03-world-resources.md | 13, 14 | Economy |
| 3D Jurisdiction | 5 | 02d-3d-jurisdiction.md | 13 | Governance |

---

## 📂 Session Details

### Session 1: Technical Architecture
**Folder**: `session-1-technical-architecture/`  
**Status**: ✅ COMPLETE (Compartmentalized)  
**Total Documents**: 18 (8 original + 10 voxel world additions)

#### Core Architecture Documents

| File | Description |
|------|-------------|
| `01-architecture-overview.md` | System design principles and technology stack |
| `02-client-server-architecture.md` | Network architecture and communication patterns |
| `03-data-persistence.md` | Database design and save systems |
| `04-performance-scalability.md` | Optimization strategies and benchmarks |
| `05-technology-testing.md` | Proof-of-concepts and tech validation |
| `06-risk-management.md` | Technical risks and mitigation strategies |
| `07-appendices.md` | Reference materials and diagrams |

#### Voxel World System Documents (NEW)

| File | Description | Key Topics |
|------|-------------|------------|
| `13-voxel-world-system.md` | Core voxel system specification | 1m³ blocks, chunk architecture, 4-byte format, block registry |
| `14-terrain-generation.md` | Procedural terrain generation | FastNoiseLite, 7-layer generation, 3 biomes, ore distribution |
| `15-terrain-modification.md` | Mining and building mechanics | Tool-block matrix, weight system, debris physics |
| `16-world-persistence.md` | Voxel world storage | RLE+LZ4 compression, delta storage, SQLite/PostgreSQL |
| `17-rendering-meshing.md` | Voxel rendering system | Greedy meshing, face culling, LOD, texture atlasing |
| `18-physics-collision.md` | Collision detection | Per-chunk collision, ConcavePolygonShape3D, physics LOD |

#### Supporting Documents

| File | Description |
|------|-------------|
| `09-rpc-protocol.md` | Network protocol specifications |
| `10-event-sourcing.md` | Event sourcing architecture |
| `11-error-handling.md` | Error handling and logging |
| `12-security-spec.md` | Security and authentication |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `minecraft-voxel-research.md` | Minecraft technical research |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |

**Key Topics**: Client-server model, WebSocket communication, PostgreSQL (production), SQLite (dev), **Voxel World System**, **1m³ blocks**, **16×16×256 chunks**, **25 agents (MVP) to 50-100 agents (post-MVP)**, **20 TPS tick rate**, <2ms per-agent processing budget

---

### Session 2: AI System Design
**Folder**: `session-2-ai-system-design/`  
**Status**: ✅ COMPLETE (Compartmentalized)

| File | Description |
|------|-------------|
| `01-core-ai-architecture.md` | Agent simulation framework and tick system |
| `02-economic-behavior.md` | Economic decision-making and labor systems |
| `03-political-social-behavior.md` | Political participation and social dynamics |
| `04-population-personality.md` | Personality generation and diversity |
| `05-narrative-debugging.md` | Narrative systems and debugging tools |
| `06-ai-skills-reference.md` | Technical skills and reference guide |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `SESSION-3-HANDOFF.md` | Transition notes for gameplay implementation |
| `VERIFICATION-PROMPT.md` | AI verification checklist |
| `VERIFICATION-REPORT.md` | Completion verification report |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |
| `archive/` | Legacy backup files |

**Key Topics**: GOAP/Utility AI hybrid, BDI architecture, personality simulation, economic modeling, political behavior, **25 agents (MVP) to 50-100 agents (post-MVP)**, **<2ms per-agent decision budget**

---

### Session 3: Core Gameplay Loops
**Folder**: `session-3-core-gameplay-loops/`  
**Status**: ✅ COMPLETE (Compartmentalized)  
**Total Documents**: 14 (consolidated from single file)

| File | Description | Key Topics |
|------|-------------|------------|
| `day3-core-gameplay-loops.md` | Original comprehensive gameplay systems (~1,050 lines) |
| `01-moment-to-moment-gameplay.md` | Core moment-to-moment mechanics |
| `01b-inventory-crafting-recipes.md` | Inventory and crafting recipes |
| `01c-movement-interaction-spec.md` | Movement and interaction systems |
| `01d-tool-system-spec.md` | Tool system specifications |
| `01e-inventory-system-spec.md` | Inventory system with weight/encumbrance |
| `01f-voxel-interaction-spec.md` | **NEW** - Voxel interaction (mining/building) | Raycasting, mining mechanics, placement |
| `01g-entity-block-system.md` | **NEW** - Hybrid entity-block system | Minecarts, workshops, furniture, doors |
| `02-session-gameplay.md` | Session-level gameplay loops |
| `02b-economic-system-spec.md` | Economic system specifications |
| `02c-governance-mechanics-detail.md` | Detailed governance mechanics |
| `03-multi-session-arcs.md` | Multi-session narrative arcs |
| `04-player-archetypes.md` | Player archetypes and motivations |
| `05-progression-feel.md` | Progression feel and pacing |
| `06-return-triggers.md` | Player return triggers |
| `07-ui-ux-paths.md` | UI/UX design paths |
| `07b-screen-specifications.md` | Screen specifications |
| `weight-carrying-system.md` | **NEW** - Weight carrying system | Carrying capacity, strength modifiers, vehicles |
| `comprehensive-entity-catalog.md` | **NEW** - Entity catalog | Transportation, crafting, storage, furniture |
| `01-gameplay-systems-architecture.md` | Gameplay systems architecture |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |

**Key Topics**: Game phases, core loops, **voxel interaction**, **weight/encumbrance**, **entity system**, economic simulation, player agency, social dynamics, competition mechanics, emergent systems

---

### Session 4: Progression & Balance
**Folder**: `session-4-progression-and-balance/`  
**Status**: ✅ COMPLETE (Mixed - Core + Voxel Resources)

| File | Description | Key Topics |
|------|-------------|------------|
| `day4-progression-and-balance.md` | Comprehensive progression systems (~1,200 lines) |
| `03-world-resources.md` | **NEW** - World resources specification | Surface resources, ore tiers, gathering mechanics |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |

**Key Topics**: Difficulty curves, economy balance, progression systems, **resource distribution by depth**, **renewable vs finite resources**, anti-frustration measures, exponential vs linear growth, tuning values

---

### Session 5: Governance Mechanics
**Folder**: `session-5-governance-mechanics/`  
**Status**: ✅ COMPLETE (Mixed - Core + 3D Jurisdiction)

| File | Description | Key Topics |
|------|-------------|------------|
| `day5-governance-mechanics.md` | Comprehensive governance systems (~1,200 lines) |
| `02d-3d-jurisdiction.md` | **NEW** - 3D jurisdiction and land claims | Volumetric ownership, mineral/air rights, claims |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |

**Key Topics**: Political systems, voting mechanics, policy implementation, **3D land claims**, **vertical jurisdiction**, power dynamics, government transitions, civic engagement

---

### Session 6: Prototyping Roadmap
**Folder**: `session-6-prototyping-roadmap/`  
**Status**: 📝 CONTENT READY (Single File)

| File | Description |
|------|-------------|
| `day6-prototyping-roadmap.md` | Implementation roadmap (~880 lines) |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |

**Key Topics**: MVP scope, phased development, prototype phases, vertical slices, sprint planning, risk mitigation, deliverables timeline

---

### Session 7: Integration Master Plan
**Folder**: `session-7-integration-master-plan/`  
**Status**: 📝 CONTENT READY (Single File)

| File | Description |
|------|-------------|
| `day7-master-development-plan.md` | Complete integration plan (~1,020 lines) |
| `RESEARCH-INDEX.md` | Research sources and citations |
| `[AGENTS-READ-FIRST]-index.md` | Session-specific navigation index |

**Key Topics**: System integration, data flow, API contracts, testing strategy, deployment pipeline, monitoring, final architecture

---

## ⚠️ Important Notes

### Content Organization Differences

**Sessions 1-2**: Compartmentalized Structure
- Multiple focused files (01-07, 13-18 naming convention for Session 1)
- Individual indices per session
- Better for detailed reference and parallel work
- Example: `session-1/13-voxel-world-system.md`

**Sessions 3-5**: Mixed Structure
- Core content in consolidated files
- NEW: Voxel-specific documents compartmentalized (01f, 01g, weight-carrying, entity-catalog, 03-world-resources, 02d-3d-jurisdiction)
- Balance between readability and reference

**Sessions 6-7**: Single File Structure
- One comprehensive file per session
- Faster to read complete context
- Simpler navigation

### Voxel World Integration (2026-02-01)

**Major Addition**: Complete voxel world technical specification added across Sessions 1, 3, 4, and 5.

**New Documentation**:
1. **Session 1** (6 documents, ~800 lines):
   - 13-voxel-world-system.md: Core technical spec
   - 14-terrain-generation.md: Procedural generation
   - 15-terrain-modification.md: Mining/building mechanics
   - 16-world-persistence.md: Storage and compression
   - 17-rendering-meshing.md: Rendering pipeline
   - 18-physics-collision.md: Collision detection

2. **Session 3** (4 documents, ~800 lines):
   - 01f-voxel-interaction-spec.md: Player interaction
   - 01g-entity-block-system.md: Hybrid entity system
   - weight-carrying-system.md: Logistics and carrying
   - comprehensive-entity-catalog.md: Entity specifications

3. **Session 4** (1 document, ~300 lines):
   - 03-world-resources.md: Resource distribution

4. **Session 5** (1 document, ~300 lines):
   - 02d-3d-jurisdiction.md: 3D land claims

**Technical Validation Updates**:
All voxel documentation aligns with Session 1 constraints:
- 20 TPS tick rate compliance
- <2ms per-agent processing budget
- <50ms chunk generation time
- 4-byte block format for memory efficiency
- 16×16×256 chunks for optimal performance

### Research Integration
- Every session includes a `RESEARCH-INDEX.md` file
- Citations follow academic and industry standards
- Research validates design decisions
- Links to papers, games, and real-world systems

### Legacy Files Archived
- Sessions 1-2 have `archive/` folders
- Legacy content preserved for historical reference
- Use current compartmentalized versions for implementation
- Archive files marked with `.legacy.md` or `.backup.md` extensions

---

## 🔗 Quick Links

### Voxel World Documentation
- [Core Voxel System](./session-1-technical-architecture/13-voxel-world-system.md)
- [Terrain Generation](./session-1-technical-architecture/14-terrain-generation.md)
- [Terrain Modification](./session-1-technical-architecture/15-terrain-modification.md)
- [World Persistence](./session-1-technical-architecture/16-world-persistence.md)
- [Rendering & Meshing](./session-1-technical-architecture/17-rendering-meshing.md)
- [Physics & Collision](./session-1-technical-architecture/18-physics-collision.md)
- [Voxel Interaction](./session-3-core-gameplay-loops/01f-voxel-interaction-spec.md)
- [Entity-Block System](./session-3-core-gameplay-loops/01g-entity-block-system.md)
- [Weight Carrying](./session-3-core-gameplay-loops/weight-carrying-system.md)
- [Entity Catalog](./session-3-core-gameplay-loops/comprehensive-entity-catalog.md)
- [World Resources](./session-4-progression-and-balance/03-world-resources.md)
- [3D Jurisdiction](./session-5-governance-mechanics/02d-3d-jurisdiction.md)

### Meta-Planning
- `../` - Parent planning directory
- `../planning-overview.md` - Project-wide planning overview
- `../research/` - Research materials and references

### Research Guide
- Each session contains `RESEARCH-INDEX.md`
- External research papers and citations
- Game design references and benchmarks
- Technical documentation sources

### Spreadsheets & Data
- Look for accompanying `.csv` or `.xlsx` files in session folders
- Balance spreadsheets, tuning values, and data tables
- Cost-benefit analyses and metrics

### Session Folders
- [Session 1](./session-1-technical-architecture/)
- [Session 2](./session-2-ai-system-design/)
- [Session 3](./session-3-core-gameplay-loops/)
- [Session 4](./session-4-progression-and-balance/)
- [Session 5](./session-5-governance-mechanics/)
- [Session 6](./session-6-prototyping-roadmap/)
- [Session 7](./session-7-integration-master-plan/)

---

## 📅 Next Steps

### Immediate (Next 2 Weeks)
1. [ ] Review voxel world documentation (Session 1 docs 13-18)
2. [ ] Begin technical prototyping based on Session 1 architecture
3. [ ] Set up development environment and CI/CD pipeline
4. [ ] Create core AI agent framework from Session 2 specs
5. [ ] Implement basic gameplay loop (Session 3 Phase 1)

### Short Term (1-2 Months)
1. [ ] Complete MVP prototype (Session 6 Phase 1 deliverables)
2. [ ] Implement voxel world core (chunks, generation, rendering)
3. [ ] Build agent behavior systems
4. [ ] Create basic UI/UX prototypes

### Medium Term (3-6 Months)
1. [ ] Implement terrain modification (mining/building)
2. [ ] Complete all vertical slices from Session 6
3. [ ] Full system integration per Session 7
4. [ ] Alpha testing with synthetic populations

### Long Term (6-12 Months)
1. [ ] Beta release with player testing
2. [ ] Balance tuning based on metrics
3. [ ] Content expansion and polish
4. [ ] Full release preparation

---

## 📊 Planning Statistics

| Metric | Value |
|--------|-------|
| Total Sessions | 7 |
| Complete Sessions | 5 (Sessions 1-5) |
| Content Ready Sessions | 2 (Sessions 6-7) |
| Total Planning Lines | **~9,050** |
| Research Citations | 75+ across all sessions |
| Compartmentalized Files | 35+ |
| Voxel World Documents | 12 |
| Archive Files | 2+ |
| Last Content Update | **2026-02-01** (Voxel World Integration Complete) |

---

## 🎯 Implementation Priority Matrix

| Priority | Session | Focus Area |
|----------|---------|------------|
| **P0 - Critical** | Session 1 | Core architecture and tech stack |
| **P0 - Critical** | Session 1 | **Voxel World System (13-18)** |
| **P0 - Critical** | Session 2 | AI agent framework |
| **P1 - High** | Session 6 | Prototype roadmap and MVP scope |
| **P1 - High** | Session 3 | Core gameplay mechanics |
| **P1 - High** | Session 3 | **Voxel interaction & weight system** |
| **P2 - Medium** | Session 4 | Balance and progression systems |
| **P2 - Medium** | Session 4 | **World resources** |
| **P2 - Medium** | Session 7 | Integration planning |
| **P3 - Low** | Session 5 | Governance mechanics (Phase 2+) |
| **P3 - Low** | Session 5 | **3D jurisdiction** |

---

> **AGENT/DEVELOPER NOTICE**: This index is your primary navigation hub. When working on any feature, start by reviewing the relevant session's content, then check dependencies. Update this file if session statuses change or new content is added.

**Document Owner**: AI Development Team  
**Review Cycle**: Weekly during active development  
**Questions?**: Check session-specific indices or consult project lead

---

*Generated: 2026-02-01 | Societies Planning v1.1.0 (Voxel World Integration)*
