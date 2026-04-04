# Day 1: Architecture Overview

> **Navigation**: [Index]([AGENTS-READ-FIRST]-index.md) | [Next: Client & Server Architecture](02-client-server-architecture.md)
> 
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

# Session 1: Technical Architecture - Deep Planning Document

**Planning Session**: 1 of 7  
**Status**: âœ… Complete  
**Date Started**: 2026-01-30  
**Date Completed**: 2026-01-30

---

## Executive Summary

This document establishes the complete technical foundation for Societies, a multiplayer ecosystem simulation game. Key architectural decisions validated through comprehensive research:


**Networking**: ENet state synchronization chosen over lockstep to enable variable tick rates (10-30 TPS), time acceleration (2x-10x), and AI agent randomness. State sync uses 0.6 KB/s baseline (scales to 32-112 KB/s with agents) and allows mid-game joining without replay catchup. *Note: State sync uses 4x more bandwidth than lockstep (0.6 vs 0.14 KB/s) but avoids determinism complexity; 99% reduction is vs naive snapshots (76 KB/s)*.

**Performance Targets**: 
- **MVP Target**: 20 AI agents + 8 concurrent players at 20 TPS, requiring 32 KB/s bandwidth per player (256 KB/s server upload)
- **Post-MVP Scale**: 50-100 AI agents + 20+ players at 20 TPS, scaling to 112 KB/s per player
- **Technical Foundation**: Headless server mode (40-60% CPU reduction, 70-80% memory savings) validated for all scales

**Database**: PostgreSQL JSONB with GIN indexes for large-scale production servers like Eco's 50-100 player servers (<1ms queries), SQLite for development/single-player (MVP: 8 players, 20 agents), avoiding Eco's LiteDB disaster at production scale.

**Architecture**: Server-authoritative with event-sourced saves enabling replay debugging, continuous world simulation with time acceleration (2x-10x), offline-to-multiplayer migration possible via localhost headless server with world export/import.

**Key Risks Mitigated**: AI performance (Utility AI scales to 20 agents (MVP), 50-100 agents post-MVP vs GOAP's 10-20 per [r7-ai-systems-games.md]), database bottlenecks (PostgreSQL with batching for production scale), memory usage (headless 70-80% savings), multiplayer sync (state sync proven).

---

## Table of Contents

1. [Purpose](#purpose)
2. [Key Questions Addressed](#key-questions-addressed)
3. [Research Summary](#research-summary)
4. [Dependencies](#dependencies)
5. [System Architecture Overview](#1-system-architecture-overview)
6. [World & Biome System Architecture](#2-world--biome-system-architecture)
7. [Client Architecture](02-client-server-architecture.md#3-client-architecture)
8. [Server Architecture](02-client-server-architecture.md#4-server-architecture)
9. [Offline Mode Architecture](03-offline-save-database.md#5-offline-mode-architecture)
10. [Save/Replay System](03-offline-save-database.md#6-savereplay-system)
11. [Database Architecture](03-offline-save-database.md#7-database-architecture)
12. [Performance Budgets](04-performance-technology.md#8-performance-budgets)
13. [Technology Stack Decision](04-performance-technology.md#9-technology-stack-decision)
14. [Testing Architecture](05-testing-scalability.md#10-testing-architecture)
15. [Scalability Strategy](05-testing-scalability.md#11-scalability-strategy)
16. [Technical Risk Assessment](06-risks-research.md#12-technical-risk-assessment)
17. [Open Questions & Future Research](06-risks-research.md#13-open-questions--future-research)
18. [Decisions Log](07-decisions-skills.md#14-decisions-log)
19. [Cross-References](08-cross-references-integration.md#15-cross-references)
20. [Success Criteria](07-decisions-skills.md#16-success-criteria)
21. [Technical Skills Development](07-decisions-skills.md#17-technical-skills-development)
22. [Network Resilience & Error Handling](09-network-database-integration.md#18-network-resilience--error-handling)
23. [Database Schema Evolution](09-network-database-integration.md#19-database-schema-evolution)
24. [Integration Map](09-network-database-integration.md#20-integration-map)
25. [Research Summary](10-research-changes.md#21-research-summary)
26. [Bibliography & Sources](10-research-changes.md#22-bibliography--sources)

---

## Purpose

Define the technical foundation that makes Societies possible. This document establishes the system architecture, performance budgets, technology stack decisions, and technical risk assessment for a multiplayer ecosystem simulation game built in Godot 4.x with C#.

---

## Key Questions Addressed

1. What's the overall system architecture (client/server, database, simulation engine)?
2. How do we handle continuous simulation (even when no players online)?
3. What are the performance constraints (world size, agent count, tick rate)?
4. How does offline mode work? (Single-player = local server)
5. What's the save/replay system architecture?
6. What are the hard technical limitations we must design within?

---

## Research Summary

### Tier 1 Sources - Critical Research

#### 1. Godot 4.x Multiplayer Architecture
- **Source**: Godot Engine 4.3 Official Documentation - Networking & MultiplayerAPI
- **URL**: https://docs.godotengine.org/en/4.3/tutorials/networking/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: Godot 4.x provides native MultiplayerAPI with RPC support (@rpc annotation), scene replication via MultiplayerSynchronizer, and built-in ENet integration. Authoritative server pattern is fully supported with `IsMultiplayerAuthority()` checks.

#### 2. ENet Networking Protocol
- **Source**: ENet Official Documentation (enet.bespin.org)
- **URL**: http://enet.bespin.org/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: ENet provides reliable UDP with optional in-order delivery, designed specifically for multiplayer games (originally for FPS Cube). Supports both reliable and unreliable channels, critical for separating game state (unreliable) from critical events (reliable).

#### 3. Network Synchronization Patterns
- **Source**: Gaffer On Games - "State Synchronization" and "Deterministic Lockstep"
- **URL**: https://gafferongames.com/post/state_synchronization/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: State synchronization (sending world state deltas) is more appropriate than lockstep for our use case because: (1) we don't need 100% deterministic simulation for gameplay, (2) supports joining mid-game, (3) easier to handle variable tick rates, (4) better for continuous world simulation.

#### 4. PostgreSQL JSONB Performance
- **Source**: Multiple sources - AWS Database Blog, ScaleGrid, Prateek Codes
- **URLs**: https://aws.amazon.com/blogs/database/postgresql-as-a-json-database/, https://scalegrid.io/blog/using-jsonb-in-postgresql/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: PostgreSQL JSONB with proper GIN indexing performs within 10-20% of normalized tables for read operations, with significant flexibility benefits. Write performance penalty acceptable for our use case. GIN indexes on JSONB fields provide fast containment queries (`@>`, `?`, `?&`).

#### 5. Factorio Multiplayer Architecture (Case Study)
- **Source**: Factorio Friday Facts - Multiplayer articles (FFF #30, #188, #302)
- **URL**: https://www.factorio.com/blog/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: Factorio uses deterministic lockstep with CRC checks to detect desyncs. Their "megapacket" approach (batching many updates) reduced bandwidth by 90%. Desync debugging requires deterministic replay capability, validating our event-sourced save system approach.

#### 6. Eco Game Performance Optimization (Reference Game)
- **Source**: Strange Loop Games - Eco Server Performance Updates
- **URL**: https://eco-servers.org/blog/174/update-97-focus-on-performance/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: Eco faced similar challenges: simulation performance with many entities, database persistence bottlenecks, server memory usage. Their optimizations focused on: (1) spatial partitioning, (2) dirty tracking for state updates, (3) database batching, (4) multi-threading where thread-safe.

#### 7. Godot Headless Server Performance
- **Source**: Godot Benchmarks & GitHub Issues
- **URL**: https://benchmarks.godotengine.org/
- **Last Reviewed**: 2026-01-30
- **Key Finding**: Godot headless mode eliminates rendering overhead, providing ~60-80% CPU reduction compared to graphical mode. .NET/C# integration shows minimal overhead (<5%) compared to GDScript for non-GUI code, with significant performance benefits for compute-intensive operations.

### Key Insights - Technical Decisions Justified

#### 1. Godot 4.x + C# Selection Validated
- **Finding**: Godot 4.3's MultiplayerAPI provides production-ready authoritative server support with ENet integration
- **Impact**: Confirms our Decision 1 (Godot 4.x + C#) as technically sound
- **Risk Mitigation**: Headless mode performance is sufficient for 100-agent simulation target

#### 2. State Sync Over Lockstep
- **Finding**: State synchronization better fits our requirements than deterministic lockstep
- **Reasoning**: 
  - Lockstep requires perfect determinism across all clients - difficult with floating-point physics
  - State sync allows variable tick rates (10-30 TPS) and time acceleration when players offline
  - Easier to implement continuous world simulation with state sync
- **Impact**: Influences [System Architecture](#1-system-architecture-overview) - we use state sync with periodic snapshots

#### 3. ENet Bandwidth Considerations
- **Finding**: ENet's reliable + unreliable channel separation is critical for our use case
- **Application**:
  - Unreliable: Position updates, animation states (can miss occasionally)
  - Reliable: Inventory changes, law votes, economic transactions (must arrive)
- **Impact**: [Network Layer](02-client-server-architecture.md#network-layer) - we need channel configuration strategy

#### 4. JSONB Database Strategy Validated
- **Finding**: PostgreSQL JSONB with GIN indexes provides flexibility + performance balance
- **Performance**: Within acceptable range for entity/agent data that changes frequently
- **Migration**: Schema evolution easier with JSONB for experimental features
- **Impact**: Confirms Decision 3 (Dual database strategy) - PostgreSQL JSONB for large-scale production (50+ players), SQLite for dev/single-player (MVP scale)

#### 5. Event Sourcing for Debugging
- **Finding**: Factorio's desync debugging approach validates our event-sourced save system
- **Benefits**: 
  - Can replay exact world state at any point in time
  - Debug "what happened at tick X?" questions
  - Supports branching worlds (save at point A, diverge to B and C)
- **Impact**: Confirms Decision 5 (Event-sourced save system) with snapshots + event log

#### 6. Performance Optimization Priorities
- **Finding**: Based on Eco and Factorio learnings, our optimization priorities should be:
  1. Spatial partitioning (grid-based entity culling) - biggest impact
  2. Dirty tracking (only sync changed entities) - reduces bandwidth 60-80%
  3. Delta compression (state differences vs full state) - reduces bandwidth 50-70%
  4. Tick budgeting (priority system for AI processing) - maintains 20 TPS target
- **Impact**: Informs [Performance Budgets](04-performance-technology.md#8-performance-budgets) optimization strategies

### Open Questions from Research
- [ ] What is Godot's actual entity limit before performance degradation? (Needs prototyping)
- [ ] How much bandwidth does 20 agents at 20 TPS actually use? (Needs measurement - 100 agents is post-MVP scale)
- [ ] What is the optimal snapshot frequency for our use case? (Factorio: every 2 seconds)

## Dependencies

- **Requires**: Comprehensive game design document (societies-comprehensive-breakdown.md)
- **Informs**: 
  - [Session 2: AI System Design](../session-2-ai-system-design/[AGENTS-READ-FIRST]-index.md) - Performance budgets and technical constraints for AI processing
  - [Session 3: Core Gameplay Loops](../session-3-core-gameplay-loops/[AGENTS-READ-FIRST]-index.md) - Technical constraints determine feasible interaction density and pacing
  - [Session 4: Progression & Balance](../session-4-progression-and-balance/[AGENTS-READ-FIRST]-index.md) - Entity limits and performance budgets constrain population scaling
  - [Session 5: Governance Mechanics](../session-5-governance-mechanics/[AGENTS-READ-FIRST]-index.md) - Server-authoritative architecture enables secure law enforcement and vote validation
  - [Session 6: Prototyping Roadmap](../session-6-prototyping-roadmap/[AGENTS-READ-FIRST]-index.md) - Technical prototypes and testing milestones
  - [Session 7: Master Plan](../session-7-integration-master-plan/[AGENTS-READ-FIRST]-index.md) - Architecture dependencies, Week 2 testing setup tasks

---

## 1. System Architecture Overview

### Executive Summary

Societies is a multiplayer ecosystem simulation game built on a **server-authoritative architecture** using Godot 4.x with C#. The architecture supports **20 AI agents** and up to **8 concurrent players** at **20 TPS (ticks per second)** for the MVP, requiring approximately **32 KB/s bandwidth per player** and **256 KB/s total server upload** [r1-research-summary.md, Key Finding 4]. The system is architected to scale to 50-100 agents and 20+ players post-MVP. The system is designed to maintain **5,000-10,000 active entities** in headless server mode (theoretical capacity based on hardware specs; requires prototyping validation) through aggressive spatial partitioning and LOD systems [r1-godot-headless-research.md]. 

Key architectural decisions validated by research:
- **State synchronization** over deterministic lockstep (99% bandwidth reduction: 0.6 KB/s vs 76 KB/s) [r1-network-sync-research.md]
- **Headless Godot servers** providing 40-60% CPU reduction and 70-80% memory savings [r1-godot-headless-research.md]
- **PostgreSQL JSONB** with GIN indexes (0.5-0.8ms query times) for flexible agent data persistence [r1-postgresql-jsonb-research.md]
- **Event-sourced save system** enabling replay debugging and branching world timelines [r1-factorio-case-study.md]
- **Seamless offline-to-multiplayer transition** via localhost headless server (no code duplication) [r1-research-summary.md, Decision 4]

The architecture prioritizes **continuous world simulation**â€”the ecosystem evolves even without players online, with support for time acceleration when players are away. This is achieved through a tick-based simulation loop running at 20 TPS, with priority-based CPU budgeting (25-75% utilization) to maintain performance under load [r1-eco-performance-research.md].

### High-Level Architecture

```mermaid
graph TB
    subgraph "Client Layer [Godot 4.x + .NET 8.0]"
        C1[Player Client<br/>Godot 4.x + C#<br/>~1-3GB RAM]
        C2[Player Client<br/>Godot 4.x + C#<br/>~1-3GB RAM]
        C3[Headless Local Server<br/>Single-Player Mode<br/>~270MB RAM]
    end

    subgraph "Network Layer [ENet UDP]"
        NL[ENet MultiplayerPeer<br/>32 KB/s per player (MVP)<br/>Scales to 112 KB/s<br/>255 channels max]
    end

    subgraph "Server Layer [Headless Godot 4.x]"
        SS[Simulation Server<br/>Headless Mode<br/>40-60% CPU savings<br/>70-80% memory savings]
        SE[Entity Manager<br/>5,000-10,000 entities max]
        SA[Agent AI System<br/>20 agents @ 20 TPS (MVP)]
        SG[Governance Engine<br/>Server-authoritative]
        EC[Ecosystem Simulation<br/>100m chunk partitioning]
    end

    subgraph "Persistence Layer"
        PDB[(PostgreSQL 15+<br/>Production<br/>GIN indexed JSONB)]
        SDB[(SQLite<br/>Dev/Single-player<br/>Zero-config)]
        RS[Replay System<br/>Event-sourced<br/>Snapshots + Event Log]
    end

    C1 <-->|ENet<br/>0.6 KB/s state sync| NL
    C2 <-->|ENet<br/>0.6 KB/s state sync| NL
    C3 -.->|Localhost<br/>0ms latency| SS
    NL <-->|State Sync<br/>20 TPS| SS
    SS <-->|Async<br/>Batch every 5s| PDB
    SS <-->|Direct| SDB
    SS -->|Append-only| RS
    
    style C3 fill:#f9f,stroke:#333,stroke-width:2px
    style SS fill:#bbf,stroke:#333,stroke-width:2px
```

**Diagram Notes**:
- **Godot 4.x** required for stable MultiplayerAPI and ENet integration [r1-godot-multiplayer-research.md]
- **Headless mode** eliminates rendering overhead (~40-60% CPU reduction) [r1-godot-headless-research.md]
- **ENet** provides 255 channels per connection; Channel 0 = reliable critical events, Channel 1 = unreliable ordered position updates, Channel 2 = unreliable effects [r1-enet-protocol-research.md]
- **Bandwidth**: 112 KB/s per player = 80 KB/s positions (nearby) + 0.5 KB/s state + 10 KB/s snapshots + 20 KB/s overhead [r1-enet-protocol-research.md]
- **Entity limits**: 5,000-10,000 in headless mode (theoretical capacity; requires prototyping validation); CPU-bound by AI calculations, not entity count [r1-godot-headless-research.md]

### Architecture Principles

#### Principle 1: Server-Authoritative
**Statement**: All simulation logic runs on server; clients are dumb terminals with prediction

**Research Validation**:
- Godot's `IsMultiplayerAuthority()` method enables authoritative server pattern with RPC validation [r1-godot-multiplayer-research.md]
- Server must validate every client action; never trust client for economic/governance state [r1-godot-multiplayer-research.md]
- Eco's authoritative server model prevents client desync by design [r3-eco-technical-postmortem.md, Section 1.3]
- Space Engineers moved from P2P to client-server because P2P couldn't handle complex physics interactions [r6-multiplayer-simulation-tech.md, Section 1]

**Implementation**: Use `[RPC(TransferMode = TransferModeEnum.Reliable)]` with `IsMultiplayerAuthority()` checks for all state mutations [r1-godot-multiplayer-research.md]

#### Principle 2: Offline = Local Server
**Statement**: Single-player mode runs headless server locally (no separate code path)

**Research Validation**:
- Headless Godot server runs efficiently on localhost with `--headless` flag [r1-godot-headless-research.md]
- `CallLocal = true` RPC parameter reduces latency for single-player mode (localhost) [r1-godot-multiplayer-research.md]
- Same code paths = no maintenance burden or feature divergence [r3-eco-technical-postmortem.md]
- SQLite file-based database provides zero-setup persistence for local play [r1-postgresql-jsonb-research.md]

**Implementation**: `godot --headless --script server.gd` with ENet connection to 127.0.0.1 [r1-godot-headless-research.md]

#### Principle 3: Continuous Simulation
**Statement**: World evolves even without players (time acceleration possible)

**Research Validation**:
- State synchronization supports variable tick rates (10-30 TPS) and time acceleration [r1-network-sync-research.md]
- Lockstep (deterministic) approach would prevent time acceleration; state sync allows it [r1-network-sync-research.md, Section 4]
- Eco implements continuous ecosystem simulation with 20-30 Hz tick rate [r3-eco-technical-postmortem.md, Section 1.3]
- Simulation can run faster than real-time when no players present (configurable 2x, 5x, 10x) [r1-research-summary.md]

**Implementation**: Server maintains tick loop independent of player connections; configurable `TimeScale` multiplier [r1-research-summary.md]

#### Principle 4: Deterministic for Debugging Only
**Statement**: Reproducible results for debugging and replay systemâ€”NOT for lockstep gameplay

**Research Validation**:
- **CRITICAL DISTINCTION**: We use state sync (not lockstep) for gameplay; determinism is only for replay/debugging [r1-network-sync-research.md, Section 4]
- Lockstep requires perfect bit-level determinism across all clientsâ€”difficult with floating-point economy and AI randomness [r1-factorio-case-study.md, Section 3]
- State sync bandwidth: 0.6 KB/s vs 76 KB/s for snapshots vs 2.8 KB/s for lockstep (with input overhead) [r1-network-sync-research.md]
- Factorio spent years perfecting deterministic simulation; we avoid this complexity by using authoritative state sync [r1-factorio-case-study.md]
- Deterministic replay requires: same random seeds, same tick rate, same initial conditions [r1-factorio-case-study.md, Section 4]

**Implementation**: Use seeded RNG for replay capability; server authoritative for gameplay state; CRC checks optional for debug [r1-factorio-case-study.md]

### Architectural Philosophy: Technical Decisions Serving Game Ethos

This section explicitly connects our technical architecture to the core design philosophy outlined in `societies-comprehensive-breakdown.md`. Each architectural principle embodies a game design value:

#### Principle 1: Server-Authoritative â†’ **Equivalence Principle**

**Game Philosophy**: "AI agents and human players are the same type of entity, differing only in their controller."

**Technical Embodiment**:
- Whether a network packet originates from a human player's keyboard or an AI agent's Utility AI decision tree, the server processes it through **identical validation paths**
- The `IsMultiplayerAuthority()` check doesn't distinguish between human and AI sources
- Both use the same database schema (`AGENTS` table can represent either)
- Economic transactions, governance votes, and social interactions all flow through the same server logic

**Why This Matters**: There is no "NPC subsystem" or second-class AI economy. The architecture enforces genuine equality by making it technically impossible to treat AI differently.

#### Principle 2: Offline = Local Server â†’ **Persistent World Vision**

**Game Philosophy**: "The simulation continues whether humans are online or not, creating a living world rather than a session-based experience."

**Technical Embodiment**:
- Single-player mode runs the **exact same simulation** as multiplayer via localhost headless server
- No "single-player code path" that diverges from multiplayer behavior
- Event-sourced saves enable world continuity across sessions
- Time acceleration (2x-10x) allows world evolution during human absence

**Why This Matters**: The world doesn't reset when you log off. The architecture treats player absence as a network disconnection, not a game session end.

#### Principle 3: Continuous Simulation â†’ **Simulation-First Approach**

**Game Philosophy**: "The world exists as a simulation first, with humans being one type of participant among many."

**Technical Embodiment**:
- Server tick loop runs at 20 TPS **independent of player connections**
- Ecosystem, economy, and AI agents continue processing when player count = 0
- State synchronization (vs lockstep) enables variable tick rates and time acceleration
- World state is authoritative, not player-centric

**Why This Matters**: You're joining a living world, not initializing a game state. The architecture puts the simulation at the center, with players as participants.

#### Principle 4: State Sync Over Lockstep â†’ **No Artificial Constraints**

**Game Philosophy**: "Societies avoids gamey/artificial constraints that break immersion in the simulation."

**Technical Embodiment**:
- **State sync allows**: Variable tick rates (10-30 TPS), floating-point economy, AI randomness, time acceleration
- **Lockstep would require**: Fixed tick rates, fixed-point math (no decimals), deterministic RNG (no true randomness), identical simulation on all clients
- We rejected lockstep specifically to avoid these artificial constraints

**Why This Matters**: Real economies use floating-point numbers. Real AI makes non-deterministic decisions. The architecture serves simulation realism over technical convenience.

#### Governance Through Code â†’ **Non-Violent Conflict Resolution**

**Game Philosophy**: "Conflicts are resolved through negotiation, voting, trade, and lawâ€”not through violence."

**Technical Embodiment**:
- **Server-authoritative law enforcement**: Laws are validated and enforced by the server, not optional
- **No combat networking code**: Unlike most multiplayer games, our architecture contains no PvP combat validation, hit detection, or damage systems
- **Governance state is authoritative**: Law changes are server-state, not suggestions
- **RPC security**: Economic and governance actions use reliable, validated RPCs

**Why This Matters**: The technical choice to make laws server-enforced (not player-enforced) makes governance meaningful. You can't "opt out" of laws through PvP combat.

#### Trade-offs Made in Service of Vision

| Technical Trade-off | Convenience Cost | Principle Served |
|--------------------|------------------|------------------|
| **State sync over lockstep** | 4x bandwidth, more complex interpolation | Simulation flexibility, AI randomness |
| **Headless server for single-player** | Deployment complexity | Persistent world, equivalence |
| **Event-sourced saves** | 216 MB/month storage, replay complexity | Debugging, world continuity |
| **PostgreSQL over SQLite** | Higher ops cost | Reliability, avoiding Eco's LiteDB issues |
| **C# over GDScript** | Learning curve | AI performance (enables 20+ agents) |
| **Server-authoritative everything** | Latency for all actions | Security, equivalence |

**Conclusion**: Every major technical decision in this architecture serves the game's core philosophy. We did not choose convenience; we chose alignment with the vision of AI-human equivalence in a persistent, non-violent simulation.

### Performance Characteristics

**Bandwidth Budget** (MVP: 25 agents, 8 players):
| Component | Calculation | Per Player |
|-----------|-------------|------------|
| Agent positions (nearby) | 20 TPS Ã— 25 agents Ã— 0.04 KB | 20 KB/s |
| State updates (batched) | Reliable RPC overhead | 0.5 KB/s |
| World snapshots | Every 2 seconds, 5 KB each | 2.5 KB/s avg |
| Chat/commands | Text + protocol overhead | 2 KB/s |
| Protocol overhead | ENet headers + acks (~10%) | 7 KB/s |
| **Total** | | **32 KB/s** |

**Scaling Architecture**: Bandwidth scales linearly with agent density. At full scale (50-100 agents, 20 players): 112 KB/s per player, 2.24 MB/s total server upload.

**Total Server Upload for MVP**: 32 KB/s Ã— 8 = **256 KB/s** [r1-research-summary.md, Key Finding 4; r1-enet-protocol-research.md]

**Tick Rate Budget**:
- **Target**: 20 TPS (50ms per tick) [r1-research-summary.md, Key Finding 5]
- **Variable Range**: 10-30 TPS depending on server load [r3-eco-technical-postmortem.md, Section 1.3]
- **Time Acceleration**: Up to 10x when no players online [r1-research-summary.md]
- **Eco Validation**: 20-30 Hz simulation tick achievable with proper CPU throttling [r3-eco-technical-postmortem.md]

**Entity Limits by Type** [r1-godot-headless-research.md]:

| Entity Type | MVP Limit | Stretch Limit | CPU Impact | Notes |
|-------------|-----------|---------------|------------|-------|
| **Static Entities** (buildings, resources) | 3,000 | 10,000-50,000 | Minimal | No AI, minimal processing |
| **Simple Dynamic** (plants, basic animals) | 1,500 | 5,000-10,000 | Low | Basic growth/behavior |
| **Complex AI Agents** | 20 | 50-100 | **High** | CPU-bound by decision-making |
| **Total MVP Target** | **5,000** | **N/A** | Mixed | Balanced mix of above |

**Important Clarification**: The 5,000-10,000 limit applies to the **combined total** of mostly static and simple entities. Complex AI agents with full Utility AI decision-making have a separate, lower limit of 50-100 agents due to CPU costs.

- **Headless Mode**: 5,000-10,000 total entities (mix of static + dynamic + ~20-50 AI agents) on modest hardware (4-core CPU, 8GB RAM)
- **Godot Scene Tree**: 10,000+ nodes per second add/delete capability
- **CPU Bottleneck**: AI calculations, not raw entity count

**CPU/Memory Targets**:
- **Server CPU**: 25% default utilization, 75% maximum recommended [r1-eco-performance-research.md]
- **Headless Memory**: <200MB for 20 agents (vs 1-3GB graphical mode) [r1-godot-headless-research.md]
- **Client Memory**: 1-3GB for graphical mode with 20-50 visible agents [r1-godot-headless-research.md]
- **C# Performance**: 2-5x faster than GDScript for AI/economy calculations [r1-godot-headless-research.md]

---

## 2. World & Biome System Architecture

### Biome Design Philosophy

Societies features a **dynamic biome system** where elevation creates emergent sub-biomes, enabling rich environmental diversity within a compact 0.5 kmÂ² world. This approach maximizes gameplay variety while maintaining technical feasibility for the MVP scope.

### Core Biomes (MVP)

| Biome | Climate | Key Resources | Elevation Range | Unique Features |
|-------|---------|---------------|-----------------|-----------------|
| **Boreal Forest** | Cold, seasonal | Wood, stone, berries, game animals | Sea level to 800m | Snow coverage in winter, coniferous trees, wolf packs |
| **Subtropical Desert** | Hot, arid | Sand, cacti, minerals, oases | Sea level to 600m | Extreme day/night temperature swings, scarce water, buried artifacts |
| **Jungle** | Hot, humid | Hardwood, medicinal plants, exotic fauna | Sea level to 1000m | Dense canopy, river systems, rapid plant growth |

### Elevation-Based Sub-Biome System

**Core Mechanic**: Elevation thresholds trigger environmental transitions within each base biome, creating natural diversity without requiring separate terrain types.

#### Boreal Forest Sub-Biomes

| Elevation | Sub-Biome | Characteristics | Resource Modifiers |
|-----------|-----------|-----------------|-------------------|
| 0-200m | **Lowland Taiga** | Dense pine/spruce, marshy areas | +20% wood growth, -10% movement speed |
| 200-500m | **Mid-Elevation Forest** | Mixed coniferous, clearings | Standard resource rates |
| 500-800m | **Montane Forest** | Stunted trees, rocky outcrops | +15% stone, -20% wood quality |
| 800m+ | **Alpine Tundra** | Permanent snow, no trees | Stone, ice, cold-weather herbs only |

#### Desert Sub-Biomes

| Elevation | Sub-Biome | Characteristics | Resource Modifiers |
|-----------|-----------|-----------------|-------------------|
| 0-100m | **Salt Flats** | Cracked earth, salt deposits | Salt, minerals, extreme heat |
| 100-300m | **Sand Dunes** | Shifting sands, buried ruins | Sand, artifacts, rare water pockets |
| 300-500m | **Rocky Desert** | Canyon formations, caves | Stone, minerals, shelter from heat |
| 500m+ | **Desert Mountains** | Cooler temperatures, sparse vegetation | Stone, hardy cacti, predator dens |

#### Jungle Sub-Biomes

| Elevation | Sub-Biome | Characteristics | Resource Modifiers |
|-----------|-----------|-----------------|-------------------|
| 0-150m | **Riverine Jungle** | Dense vegetation, abundant water | +30% plant growth, disease risk |
| 150-400m | **Lowland Rainforest** | Canopy layers, diverse fauna | Standard rates, rare hardwoods |
| 400-700m | **Cloud Forest** | Permanent mist, epiphytes | Medicinal plants, reduced visibility |
| 700m+ | **Jungle Peaks** | Cooler, pine-oak transition zone | Mixed resources, predator territory |

### Technical Implementation

**Elevation Data Structure**:
```csharp
public struct BiomeCell
{
    public float Elevation;           // 0-1500m range
    public BiomeType BaseBiome;       // Boreal, Desert, Jungle
    public float Temperature;         // Derived from elevation + climate
    public float Precipitation;       // Biome-specific base + elevation modifiers
    public float ResourceDensity;     // 0.0-1.0 multiplier
}
```

**Sub-Biome Determination**:
```csharp
public SubBiomeType GetSubBiome(BiomeCell cell)
{
    return cell.BaseBiome switch
    {
        BiomeType.Boreal => cell.Elevation switch {
            < 200f => SubBiomeType.LowlandTaiga,
            < 500f => SubBiomeType.MidElevationForest,
            < 800f => SubBiomeType.MontaneForest,
            _ => SubBiomeType.AlpineTundra
        },
        BiomeType.Desert => cell.Elevation switch {
            < 100f => SubBiomeType.SaltFlats,
            < 300f => SubBiomeType.SandDunes,
            < 500f => SubBiomeType.RockyDesert,
            _ => SubBiomeType.DesertMountains
        },
        BiomeType.Jungle => cell.Elevation switch {
            < 150f => SubBiomeType.RiverineJungle,
            < 400f => SubBiomeType.LowlandRainforest,
            < 700f => SubBiomeType.CloudForest,
            _ => SubBiomeType.JunglePeaks
        },
        _ => SubBiomeType.Unknown
    };
}
```

### Climate Simulation

**Temperature Calculation**:
- Base temperature determined by biome type (Boreal: -10Â°C to 15Â°C, Desert: 15Â°C to 45Â°C, Jungle: 20Â°C to 35Â°C)
- Elevation modifier: -6.5Â°C per 1000m (realistic lapse rate)
- Seasonal variation: Â±15Â°C amplitude with offset by hemisphere
- Daily variation: Desert (+20Â°C day/night swing), Jungle (+5Â°C), Boreal (+10Â°C)

**Precipitation Model**:
- Jungle: High base (2000-4000mm/year), orographic increase at elevation
- Desert: Low base (50-250mm/year), occasional flash floods
- Boreal: Moderate (300-800mm/year), snow in winter months

### Resource Distribution

Each sub-biome modifies base resource availability through density multipliers. This creates natural trade opportunities as players/agents must travel to different elevations to access specialized resources.

**Example Distribution Pattern**:
- **High-elevation stone** (Montane Forest, Rocky Desert): Required for advanced construction
- **Low-elevation water** (Riverine Jungle, Salt Flats): Essential for all settlements
- **Mid-elevation balanced resources**: Best for general settlement locations

### Migration & Settlement AI

AI agents consider sub-biome characteristics when choosing settlement locations:
- Temperature tolerance (based on clothing/technology level)
- Resource requirements (current needs vs. local availability)
- Elevation preferences (some agents prefer high ground for defense)
- Seasonal migration patterns (alpine tundra uninhabitable in winter)

### MVP Scope Constraints

- **3 base biomes** with **4 sub-biomes each** = 12 distinct environmental zones
- **0.5 kmÂ² world** provides sufficient elevation range (0-1000m) for meaningful sub-biome diversity
- **Simplified climate**: 4-season cycle, no complex weather simulation (deferred to post-MVP)
- **Static resource nodes**: Resources don't regenerate dynamically (deferred to ecosystem simulation)

### Post-MVP Expansion

- Additional base biomes: Grassland, Tundra, Temperate Forest, Savannah
- Dynamic weather systems: Storms, droughts, temperature extremes
- Resource regeneration: Ecosystem-driven resource renewal rates
- Volcanic/seismic activity: Geological events reshaping sub-biomes

---

## Cross-References

### Documents Referenced
- `planning/meta/societies-comprehensive-breakdown.md` - Core game design document (5000+ lines)
- `planning/meta/societies-meta-planning.md` - Planning methodology and session structure
- `README.md` - Project overview and quick start guide

### Documents Informed by This Session
- `planning/sessions/session-2-ai-system-design/` - AI performance budgets and technical constraints
- `planning/sessions/session-6-prototyping-roadmap/` - Technical prototypes and testing milestones
- `planning/sessions/session-7-integration-master-plan/` - Architecture dependencies, Week 2 testing setup tasks

### Files Created/Updated Based on This Document
- `.github/workflows/tests.yml` - CI/CD workflow implementation (see [Testing Architecture](05-testing-scalability.md#10-testing-architecture))
- `src/societies/Societies.csproj` - Godot project configuration with C# support
- `tests/Societies.Core.Tests/` - xUnit test project structure
- `.opencode/skills/` - Technical skills for development team

### Research File Cross-Reference

| Document Section | Research Files Used | Key Contribution |
|-----------------|-------------------|------------------|
| Sections 1-6 (Foundation) | R1, R3, R6 | Core architecture, performance |
| Sections 7-10 (Performance) | R1, R3, R6 | Bandwidth, risks, scalability |
| Session 2 (AI System Design) | R4, R7 | Agent behavior, AI systems (see [Session 2: AI System Design](../session-2-ai-system-design/[AGENTS-READ-FIRST]-index.md)) |
| Section 17 (Network) | R1, R6 | Resilience, error handling |
| Section 18 (Database) | R1, R3 | Schema evolution |
| Session 2 (AI Design) | Will use R4, R7 | Agent specification |
| Session 3 (Gameplay) | Will use R2, R5 | Core loops, UI patterns |
| Session 5 (Governance) | Will use R2, R5 | Law systems, complexity |
| Session 6 (Prototyping) | Will use R1, R3, R6 | Validation priorities |

### Quick Reference Guide

**Find Performance Numbers** â†’ [Performance Budgets](04-performance-technology.md#8-performance-budgets)
**Find AI Architecture** â†’ Session 2 (AI System Design document) - detailed AI behavior specification  
**Find Database Details** â†’ [Database Architecture](03-offline-save-database.md#7-database-architecture)
**Find Testing Setup** â†’ [Testing Architecture](05-testing-scalability.md#10-testing-architecture)
**Find Risk Assessment** â†’ [Technical Risk Assessment](06-risks-research.md#12-technical-risk-assessment)
**Find Decisions** â†’ [Decisions Log](07-decisions-skills.md#14-decisions-log)
**Find Bandwidth Calculations** â†’ [Performance Budgets - Bandwidth Section](04-performance-technology.md#bandwidth-budget-breakdown)
**Find AI Scaling Data** â†’ [Performance Budgets](04-performance-technology.md#8-performance-budgets) - agent and entity limits

---

**Next**: [Client & Server Architecture â†’](02-client-server-architecture.md)
