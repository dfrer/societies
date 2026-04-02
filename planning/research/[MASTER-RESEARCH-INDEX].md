# Master Research Index

**Comprehensive catalog of all Societies research documents organized by session relevance**

**Last Updated**: February 1, 2026  
**Total Research Volume**: ~430 KB | ~35,000+ words | 23 documents

---

## Session 1: Technical Architecture Research

Research validating server architecture, networking, database design, and performance optimization decisions.

### Core R1 Technical Sources (8 files, ~164 KB)

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r1-research-summary.md](completed/r1-research-summary.md) | Consolidated findings from all R1 research with decision validation matrix | **Primary** - Architecture overview and validated decisions |
| [r1-godot-multiplayer-research.md](completed/r1-godot-multiplayer-research.md) | Godot 4.x MultiplayerAPI deep dive with 25+ code examples | **Primary** - RPC patterns, MultiplayerSynchronizer, ENet integration |
| [r1-godot-headless-research.md](completed/r1-godot-headless-research.md) | Dedicated server optimization (40-60% CPU reduction, 70-80% memory savings) | **Primary** - Server deployment strategy |
| [r1-enet-protocol-research.md](completed/r1-enet-protocol-research.md) | Reliable UDP mechanics, 112 KB/s bandwidth, 255 channel strategies | **Primary** - Protocol selection and capacity planning |
| [r1-network-sync-research.md](completed/r1-network-sync-research.md) | State sync (0.6 KB/s) vs lockstep (76 KB/s), priority accumulator algorithm | **Primary** - Synchronization architecture decision |
| [r1-postgresql-jsonb-research.md](completed/r1-postgresql-jsonb-research.md) | GIN indexing, 20% read penalty, hybrid schema recommendations | **Primary** - Database design and query optimization |
| [r1-factorio-case-study.md](completed/r1-factorio-case-study.md) | Deterministic lockstep analysis, tick closures, CRC desync detection | **Supporting** - Event sourcing pattern validation |
| [r1-eco-performance-research.md](completed/r1-eco-performance-research.md) | Spatial partitioning, CPU throttling, 100+ player support architecture | **Supporting** - Performance optimization strategies |

### Cross-Session Technical Research

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r3-eco-technical-postmortem.md](completed/r3-eco-technical-postmortem.md) | Database I/O bottlenecks, UNET deprecation lessons, server scaling | **Primary** - Critical warnings (LiteDB disaster, UNET debt) |
| [r6-multiplayer-simulation-tech.md](completed/r6-multiplayer-simulation-tech.md) | Factorio/Space Engineers/RimWorld timeline analysis (4+ years) | **Primary** - Timeline reality check, architecture patterns |
| [r8-pdf-synthesis.md](completed/r8-pdf-synthesis.md) | Spatial partitioning confirmation, performance budget validation | **Supporting** - Final technical validation |

### Session 1 Supplemental Files

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r1-technical-constraints.md](r1-technical-constraints.md) | Performance budgets for AI systems, 20 TPS rationale, 2ms/agent budget | **Supporting** - Cross-session technical constraints reference |

---

## Session 2: AI System Design Research

Research on AI architectures, agent behaviors, decision-making systems, and memory models for human-AI coexistence.

### Core AI Research (4 files, ~100 KB)

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r4-dwarf-fortress-agents.md](completed/r4-dwarf-fortress-agents.md) | 50 personality facets, 28 needs, 3-tier memory, stress mechanics | **Primary** - Personality model, memory architecture, emergent storytelling |
| [r7-ai-systems-games.md](completed/r7-ai-systems-games.md) | Utility AI vs GOAP vs Behavior Trees scalability analysis | **Primary** - Hybrid architecture selection (Utility + BT) |
| [r7-utility-ai-systems.md](r7-utility-ai-systems.md) | Curvature/IAUS implementation, consideration curves, Guild Wars 2 case study | **Primary** - Utility AI technical implementation guide |
| [r8-pdf-synthesis.md](completed/r8-pdf-synthesis.md) | AI-native design philosophy, hybrid AI recommendations | **Supporting** - Confirms two-layer AI approach |

### AI-Related Cross-Research

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r1-technical-constraints.md](r1-technical-constraints.md) | AI performance budgets, bucketing strategies, 100-200 agent targets | **Primary** - Technical constraints on agent count |
| [r2-eco-game-analysis.md](completed/r2-eco-game-analysis.md) | Agent population patterns, AI market participation, economic behaviors | **Supporting** - AI economic behaviors reference |
| [r8-dredge-atmosphere-design.md](r8-dredge-atmosphere-design.md) | Atmosphere and narrative tension, environmental storytelling | **Supporting** - AI behavior juxtaposition strategies |

---

## Sessions 3-5: Game Systems Research

Research on gameplay mechanics, progression systems, balance, and governance.

### Session 3: Core Gameplay Loops

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r2-eco-game-analysis.md](completed/r2-eco-game-analysis.md) | Three-pillar design, visible consequence chains, player-driven systems | **Primary** - Core loop structure, environmental feedback systems |
| [r5-paradox-games-politics.md](completed/r5-paradox-games-politics.md) | Progressive disclosure, predictive feedback, 12 UI patterns catalogued | **Primary** - UI/UX design, information architecture |
| [r4-dwarf-fortress-agents.md](completed/r4-dwarf-fortress-agents.md) | Labor vs needs tension, emergent storytelling from agent behaviors | **Supporting** - Human-AI interaction dynamics |
| [r7-ai-systems-games.md](completed/r7-ai-systems-games.md) | How AI behaviors create gameplay opportunities | **Supporting** - AI gameplay integration |

### Session 4: Progression & Balance

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r2-eco-game-analysis.md](completed/r2-eco-game-analysis.md) | Exponential skill costs (Level 8 = 970 SP), player-driven pricing | **Primary** - Tech tree structure, economic balance |
| [r3-eco-technical-postmortem.md](completed/r3-eco-technical-postmortem.md) | Economic transaction volume (100k tx/min), scaling limits | **Primary** - Performance-informed balance decisions |
| [r6-multiplayer-simulation-tech.md](completed/r6-multiplayer-simulation-tech.md) | Entity optimization, scaling curves | **Supporting** - Performance constraints on balance |
| [r1-technical-constraints.md](r1-technical-constraints.md) | Performance limits affecting economy design | **Supporting** - Technical balance constraints |

### Session 5: Governance Mechanics

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r2-eco-game-analysis.md](completed/r2-eco-game-analysis.md) | Law structure (triggers/conditions/actions), voting systems | **Primary** - Core law system architecture |
| [r3-eco-technical-postmortem.md](completed/r3-eco-technical-postmortem.md) | Law system evolution, Constitution update challenges | **Primary** - Implementation roadmap and warnings |
| [r5-paradox-games-politics.md](completed/r5-paradox-games-politics.md) | Nested tooltips, predictive feedback, faction systems | **Primary** - Political UI/UX design, governance complexity |
| [r4-dwarf-fortress-agents.md](completed/r4-dwarf-fortress-agents.md) | Memory/competition systems for political memory | **Supporting** - AI political participation |
| [r7-ai-systems-games.md](completed/r7-ai-systems-games.md) | AI voting behavior considerations | **Supporting** - AI governance integration |

---

## Sessions 6-7: Implementation Research

Research on prototyping, timelines, risk assessment, and integration planning.

### Session 6: Prototyping Roadmap

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r6-multiplayer-simulation-tech.md](completed/r6-multiplayer-simulation-tech.md) | Timeline realities (4+ years), critical unknowns, risk assessment | **Primary** - Schedule expectations, go/no-go criteria |
| [r3-eco-technical-postmortem.md](completed/r3-eco-technical-postmortem.md) | Development timeline, Early Access strategy, phased release | **Primary** - Release planning lessons |
| [r8-pdf-synthesis.md](completed/r8-pdf-synthesis.md) | Risk assessment, critical unknowns identification | **Primary** - Validation and risk mitigation |
| **All R1-R8** | All research applies to prototyping decisions | **Supporting** - Technical constraints, AI, gameplay assumptions |

### Session 7: Integration Master Plan

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| **All R1-R8** | Complete research inventory integrated into master plan | **Primary** - Cross-session validation and synthesis |
| [RESEARCH_COMPLETION_REPORT.md](RESEARCH_COMPLETION_REPORT.md) | Research phase summary, 87.5% complete status | **Primary** - Research completion tracking |

---

## Reference Materials (PDFs)

Original source documents and reference PDFs.

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [r1-societies-breakdown.pdf](reference-materials/r1-societies-breakdown.pdf) (1.8 MB) | Societies game design vision and feature breakdown | **All Sessions** - Source material for R8 synthesis |
| [r2-eco-breakdown.pdf](reference-materials/r2-eco-breakdown.pdf) (584 KB) | Eco game comprehensive analysis | **Sessions 3, 4, 5** - Source material for R2/R3 research |
| [r3-scalable-ecosystem-sim.pdf](reference-materials/r3-scalable-ecosystem-sim.pdf) (1.1 MB) | Technical architecture for scalable AI simulation | **Sessions 1, 2, 6** - Source material for R1, R6, R7 |

---

## Meta Research

Documentation about the research process itself.

| Document | Description | Session Relevance |
|----------|-------------|-------------------|
| [RESEARCH_COMPLETION_REPORT.md](RESEARCH_COMPLETION_REPORT.md) | Master research index with completion status, statistics, key findings | **All Sessions** - Research phase completion report |
| [agent-research-prompts.md](agent-research-prompts.md) | Master research prompts and agent assignments (R1-R8) | **Meta** - Research methodology documentation |
| [game-analysis-research-guide.md](game-analysis-research-guide.md) | Guide for conducting game analysis research | **Meta** - Research methodology for R2, R4, R5, R7 |
| [technical-postmortems-research-guide.md](technical-postmortems-research-guide.md) | Guide for analyzing technical postmortems | **Meta** - Research methodology for R3, R6 |

---

## Quick Reference by Research ID

| ID | Document | Size | Primary Session |
|----|----------|------|----------------|
| R1 | r1-research-summary.md | 13 KB | Session 1 |
| R1a | r1-godot-multiplayer-research.md | 15 KB | Session 1 |
| R1b | r1-godot-headless-research.md | 23 KB | Session 1 |
| R1c | r1-enet-protocol-research.md | 19 KB | Session 1 |
| R1d | r1-network-sync-research.md | 24 KB | Session 1 |
| R1e | r1-postgresql-jsonb-research.md | 22 KB | Session 1 |
| R1f | r1-factorio-case-study.md | 23 KB | Session 1 |
| R1g | r1-eco-performance-research.md | 24 KB | Session 1 |
| R1x | r1-technical-constraints.md | ~15 KB | Sessions 1-2 |
| R2 | r2-eco-game-analysis.md | 54 KB | Sessions 3, 4, 5 |
| R3 | r3-eco-technical-postmortem.md | 49 KB | Sessions 1, 4, 5, 6 |
| R4 | r4-dwarf-fortress-agents.md | 47 KB | Session 2 |
| R5 | r5-paradox-games-politics.md | 43 KB | Sessions 3, 5 |
| R6 | r6-multiplayer-simulation-tech.md | 40 KB | Sessions 1, 6 |
| R7 | r7-ai-systems-games.md | 43 KB | Session 2 |
| R7x | r7-utility-ai-systems.md | ~20 KB | Session 2 |
| R8 | r8-pdf-synthesis.md | ~20 KB | Sessions 1, 2, 6, 7 |
| R8x | r8-dredge-atmosphere-design.md | ~15 KB | Session 2 |

---

## Navigation

### By Session
- [Session 1: Technical Architecture](../sessions/session-1-technical-architecture/RESEARCH-INDEX.md)
- [Session 2: AI System Design](../sessions/session-2-ai-system-design/RESEARCH-INDEX.md)
- [Session 3: Core Gameplay Loops](../sessions/session-3-core-gameplay-loops/RESEARCH-INDEX.md)
- [Session 4: Progression & Balance](../sessions/session-4-progression-and-balance/RESEARCH-INDEX.md)
- [Session 5: Governance Mechanics](../sessions/session-5-governance-mechanics/RESEARCH-INDEX.md)
- [Session 6: Prototyping Roadmap](../sessions/session-6-prototyping-roadmap/RESEARCH-INDEX.md)
- [Session 7: Integration Master Plan](../sessions/session-7-integration-master-plan/RESEARCH-INDEX.md)

### Key Research Documents
- [R1 Summary](completed/r1-research-summary.md) - Start here for technical validation
- [R4 Dwarf Fortress](completed/r4-dwarf-fortress-agents.md) - Start here for AI behavior
- [R2 Eco Analysis](completed/r2-eco-game-analysis.md) - Start here for gameplay systems
- [Completion Report](RESEARCH_COMPLETION_REPORT.md) - Overall research status

---

## File Locations Summary

```
planning/research/
├── [MASTER-RESEARCH-INDEX].md          (This file)
├── RESEARCH_COMPLETION_REPORT.md        (Phase completion status)
├── r1-technical-constraints.md          (Cross-session technical limits)
├── r7-utility-ai-systems.md             (Utility AI deep dive)
├── r8-dredge-atmosphere-design.md       (Atmosphere/tension design)
├── agent-research-prompts.md            (Research methodology)
├── game-analysis-research-guide.md      (Analysis methodology)
├── technical-postmortems-research-guide.md (Postmortem methodology)
│
├── completed/                           (16 completed research files)
│   ├── r1-research-summary.md
│   ├── r1-godot-multiplayer-research.md
│   ├── r1-godot-headless-research.md
│   ├── r1-enet-protocol-research.md
│   ├── r1-network-sync-research.md
│   ├── r1-postgresql-jsonb-research.md
│   ├── r1-factorio-case-study.md
│   ├── r1-eco-performance-research.md
│   ├── r2-eco-game-analysis.md
│   ├── r3-eco-technical-postmortem.md
│   ├── r4-dwarf-fortress-agents.md
│   ├── r5-paradox-games-politics.md
│   ├── r6-multiplayer-simulation-tech.md
│   ├── r7-ai-systems-games.md
│   └── r8-pdf-synthesis.md
│
└── reference-materials/                 (3 source PDFs)
    ├── r1-societies-breakdown.pdf
    ├── r2-eco-breakdown.pdf
    └── r3-scalable-ecosystem-sim.pdf
```

---

**Research Status**: 100% Complete (R1-R8 + Supplemental)  
**Total Documents**: 23 files  
**Total Size**: ~430 KB  
**Total Words**: ~35,000+  
**Games Analyzed**: 12+  
**Code Examples**: 25+  
**Performance Metrics**: 40+ data points
