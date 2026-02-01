# Session 1: Technical Architecture - Research Index

## Relevant Research Files

### Primary Research (Must Read)
- **R1-Research-Summary.md**: 9 validated architectural decisions
- **R1-Godot-Multiplayer-Research.md**: RPC patterns, MultiplayerSynchronizer, ENet
- **R1-Godot-Headless-Research.md**: 40-60% CPU reduction, 70-80% memory savings
- **R1-ENet-Protocol-Research.md**: 112 KB/s bandwidth, 255 channels
- **R1-Network-Sync-Research.md**: State sync vs lockstep, 0.6 KB/s vs 76 KB/s
- **R1-PostgreSQL-JSONB-Research.md**: 0.5-0.8ms queries with GIN indexes
- **R1-Factorio-Case-Study.md**: Event sourcing, megapackets, replay system
- **R1-Eco-Performance-Research.md**: Spatial partitioning, CPU throttling

### Supporting Research
- **R3-Eco-Technical-Postmortem.md**: LiteDB disaster, UNET deprecation lessons
- **R6-Multiplayer-Simulation-Tech.md**: Factorio lockstep, Space Engineers netcode
- **R8-PDF-Synthesis.md**: Spatial partitioning confirmation, validated decisions

## Key Research Findings

### Technology Stack Decisions (Validated)
1. **Godot 4.x + C#** over Unity/Unreal (MIT license, production-ready MultiplayerAPI)
2. **ENet** networking (proven in games, 112 KB/s per player capable)
3. **PostgreSQL JSONB** over Eco's LiteDB (avoids I/O bottlenecks)
4. **State sync** over lockstep (enables variable tick rates, time acceleration)
5. **Headless server mode** (40-60% CPU reduction, 70-80% memory savings)

### Performance Targets
- 20 TPS tick rate (50ms per tick)
- CPU budget: 25% default, 60% max recommended
- 20 AI agents (MVP) → 50-100 agents (post-MVP)
- 8 players (MVP) → 20+ players (stretch)
- 32 KB/s per player (MVP) → 112 KB/s (full scale)

### Critical Warnings from Research
- Eco's LiteDB caused server lag and timeouts (avoid embedded databases)
- Unity UNET deprecation created 7+ years technical debt
- Factorio spent years perfecting deterministic simulation
- Single-threaded core is main scaling bottleneck

## Documents in This Session
- [01-architecture-overview.md](01-architecture-overview.md) - Executive summary
- [02-client-server-architecture.md](02-client-server-architecture.md) - Networking & tick loop
- [03-data-persistence.md](03-data-persistence.md) - Database & event sourcing
- [04-performance-scalability.md](04-performance-scalability.md) - Performance budgets
- [05-technology-testing.md](05-technology-testing.md) - Testing strategy
- [06-risk-management.md](06-risk-management.md) - Risk analysis
- [07-appendices.md](07-appendices.md) - Reference tables

## Links
- [Session 2: AI System Design](../session-2-ai-system-design/)
- [Research Folder](../../research/completed/)
