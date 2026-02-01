# Day 1: Technical Architecture - Agent Navigation Hub

> **[AGENTS-READ-FIRST]**: This is the navigation hub for all Day 1 Technical Architecture documentation. Start here to understand what each file contains and how to navigate between them.

---

## Quick Navigation

| File | Contents | Lines | Key Topics |
|------|----------|-------|------------|
| **[01-architecture-overview](01-architecture-overview.md)** | Core Architecture Foundation | ~600 | System Overview, Biomes, Research Summary, Dependencies |
| **[02-client-server-architecture](02-client-server-architecture.md)** | Client & Server Implementation | ~1200 | Godot Client, Headless Server, Offline Mode, Tick Loop |
| **[03-data-persistence](03-data-persistence.md)** | Data Layer | ~630 | Save/Replay System, PostgreSQL/SQLite, Schema Evolution |
| **[04-performance-scalability](04-performance-scalability.md)** | Performance Engineering | ~350 | Performance Budgets, Scalability Strategy, MVP Targets |
| **[05-technology-testing](05-technology-testing.md)** | Dev Infrastructure | ~370 | Technology Stack, Testing Architecture, CI/CD |
| **[06-risk-management](06-risk-management.md)** | Project Governance | ~380 | Risk Assessment, Decisions Log, Success Criteria |
| **[07-appendices](07-appendices.md)** | Reference Materials | ~800 | Skills Development, Network Resilience, Bibliography |

---

## Document Purpose

This Day 1 Technical Architecture document establishes the complete technical foundation for Societies, a multiplayer ecosystem simulation game. Key architectural decisions validated through comprehensive research:

**Technology Stack**: Godot 4.x + C# selected over Unity/Unreal for MIT licensing, production-ready MultiplayerAPI, and 2-5x performance advantage over GDScript.

**Networking**: ENet state synchronization chosen over lockstep to enable variable tick rates (10-30 TPS), time acceleration (2x-10x), and AI agent randomness.

**Performance Targets**:
- **MVP Target**: 25 AI agents + 8 concurrent players at 20 TPS
- **Stretch Goal**: 100+ AI agents + 20+ players at 20 TPS
- **Bandwidth**: 32 KB/s per player (MVP), scaling to 112 KB/s

**Database**: PostgreSQL JSONB with GIN indexes for production (<1ms queries), SQLite for development/single-player, avoiding Eco's LiteDB disaster.

---

## When to Read Each File

### For Core Understanding
- **Start with**: [01-architecture-overview](01-architecture-overview.md) - Read the Executive Summary, System Architecture Overview, and World/Biome sections
- **Then**: [02-client-server-architecture](02-client-server-architecture.md) - Understand how the client and server work

### For Implementation Details
- **[03-data-persistence](03-data-persistence.md)** - Database schema, save/replay system, PostgreSQL JSONB details
- **[04-performance-scalability](04-performance-scalability.md)** - Performance budgets, entity limits, bandwidth calculations, scalability strategy

### For Development Setup
- **[05-technology-testing](05-technology-testing.md)** - Technology stack decisions, testing architecture, CI/CD setup, xUnit configuration

### For Risk Management & Decisions
- **[06-risk-management](06-risk-management.md)** - Technical risk assessment, open questions, decisions log with research validation

### For Reference
- **[07-appendices](07-appendices.md)** - Technical skills needed, network resilience patterns, research summary, bibliography

---

## Key Decisions Documented

All 9 major architectural decisions are in [06-risk-management.md](06-risk-management.md):

1. **Godot 4.x + C#** - Selected for multiplayer support and performance
2. **ENet Networking** - UDP-based networking with state sync
3. **PostgreSQL + SQLite** - Dual database strategy for production/dev
4. **Offline = Local Server** - Single-player runs headless server on localhost
5. **Event-Sourced Save System** - Snapshots + replay capability for debugging
6. **Comprehensive Testing** - xUnit + CI/CD from day one
7. **State Sync over Lockstep** - Variable tick rates and time acceleration
8. **Utility AI + Behavior Trees** - Scalable AI architecture
9. **Spatial Partitioning** - 100m chunks for entity management

---

## Performance Budgets

Critical numbers from [04-performance-scalability](04-performance-scalability.md):

| Metric | MVP Target | Stretch Goal |
|--------|-----------|--------------|
| AI Agents | 25 | 100 |
| Concurrent Players | 8 | 20 |
| Tick Rate | 20 TPS | 30 TPS |
| Bandwidth/Player | 32 KB/s | 112 KB/s |
| Server RAM | 8 GB | 16 GB |
| Max Entities | 2,000 | 10,000 |

---

## Cross-Reference Map

### Dependencies Between Files

```
01-architecture-overview
    ↓ (references implementation details)
02-client-server-architecture
    ↓ (references data storage)
03-data-persistence
    ↓ (references performance targets)
04-performance-scalability
    ↓ (references technology choices)
05-technology-testing
    ↓ (references risks and decisions)
06-risk-management
    ↓ (references skills and bibliography)
07-appendices
```

### Research File References

All files reference research completed in `planning/research/completed/`:

- **[r1-*.md]** - Godot headless, multiplayer, PostgreSQL, network sync, Eco performance
- **[r2-*.md]** - Eco game analysis
- **[r3-*.md]** - Eco technical postmortem (critical warnings)
- **[r4-*.md]** - Dwarf Fortress agent systems
- **[r5-*.md]** - Paradox games politics
- **[r6-*.md]** - Multiplayer simulation tech
- **[r7-*.md]** - AI systems in games
- **[r8-*.md]** - PDF synthesis

---

## Important Notes for Agents

### Original File Status
The original `day1-technical-architecture.md` has been renamed to `day1-technical-architecture.legacy.md` and should not be used for new work. All updates should be made to the compartmentalized files.

### Cross-Reference Format
Internal links use this format:
```markdown
[Section Name](XX-filename.md#section-anchor)
```

### Research Citations
External research citations use this format:
```markdown
[r1-research-summary.md], [r3-eco-technical-postmortem.md]
```

### Table of Contents
Each file has its own table of contents. The comprehensive TOC from the original document has been distributed appropriately across all 7 files.

---

## Quick Lookup Guide

| Looking For | Go To |
|-------------|-------|
| Executive Summary | [01-architecture-overview](01-architecture-overview.md) |
| System Architecture Diagram | [01-architecture-overview](01-architecture-overview.md) |
| Biome System Design | [01-architecture-overview](01-architecture-overview.md) |
| Client-Server Architecture | [02-client-server-architecture](02-client-server-architecture.md) |
| Tick Loop & CPU Budgeting | [02-client-server-architecture](02-client-server-architecture.md) |
| AI Population Elasticity | [02-client-server-architecture](02-client-server-architecture.md) |
| AI Social Systems | [02-client-server-architecture](02-client-server-architecture.md) |
| Offline Mode Design | [02-client-server-architecture](02-client-server-architecture.md) |
| Save/Replay System | [03-data-persistence](03-data-persistence.md) |
| PostgreSQL Schema | [03-data-persistence](03-data-persistence.md) |
| JSONB Performance | [03-data-persistence](03-data-persistence.md) |
| Database Schema Evolution | [03-data-persistence](03-data-persistence.md) |
| Performance Budgets | [04-performance-scalability](04-performance-scalability.md) |
| Bandwidth Calculations | [04-performance-scalability](04-performance-scalability.md) |
| Scalability Strategy | [04-performance-scalability](04-performance-scalability.md) |
| Technology Stack | [05-technology-testing](05-technology-testing.md) |
| Testing Strategy | [05-technology-testing](05-technology-testing.md) |
| CI/CD Setup | [05-technology-testing](05-technology-testing.md) |
| Risk Assessment | [06-risk-management](06-risk-management.md) |
| Decisions Log | [06-risk-management](06-risk-management.md) |
| Open Questions | [06-risk-management](06-risk-management.md) |
| Success Criteria | [06-risk-management](06-risk-management.md) |
| Technical Skills | [07-appendices](07-appendices.md) |
| Network Resilience | [07-appendices](07-appendices.md) |
| Bibliography | [07-appendices](07-appendices.md) |

---

## Status

✅ **Compartmentalization Complete**: 7 files created from original 4,506-line document
- Total lines distributed: ~4,506
- Average file size: ~640 lines
- All cross-references maintained
- All formatting preserved
- Original file renamed to `.legacy.md`

---

## Next Steps for Other Day Planning Documents

If this compartmentalization approach works well, consider applying it to:
- `day2-ai-system-design.md` (likely also large)
- `day3-core-gameplay-loops.md`
- `day4-progression-and-balance.md`
- `day5-governance-mechanics.md`
- `day6-prototyping-roadmap.md`
- `day7-master-development-plan.md`

---

*Last Updated: 2026-01-30*
*Compartmentalized by: Agent Swarm*
