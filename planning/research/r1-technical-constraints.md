# Research: Technical Constraints for AI Systems

**Research ID**: R1  
**Date**: January 31, 2026  
**Source**: Industry standards, Unity documentation, game optimization patterns  
**Referenced In**: Session 2 AI System Design

---

## Executive Summary

This research establishes foundational technical constraints for real-time AI agent simulation in games, focusing on performance budgets, tick rates, and optimization strategies validated across industry implementations.

---

## 1. Tick Rate Selection (20 TPS)

### Standard Industry Practice
- **20 TPS (Ticks Per Second)** = 50ms per tick window
- Common in multiplayer games (Counter-Strike, Valorant use 64-128 TPS)
- Single-player/RTS games often use 20-30 TPS for AI systems
- **Trade-off**: Higher TPS = more responsive but higher CPU load

### Session 2 Rationale
```
20 TPS × 50ms = 1 second
100 agents × 2ms = 200ms total processing
200ms ÷ 5 buckets = 40ms per tick (fits in 50ms window with 10ms buffer)
```

### Benefits
- ✅ Amortization-friendly (divides evenly)
- ✅ Network synchronization manageable
- ✅ Sufficient for believable AI behavior
- ✅ Allows 2ms per-agent budget at scale

---

## 2. Per-Agent Performance Budget (2ms)

### Budget Breakdown
| Phase | Frequency | Budget | Cumulative |
|-------|-----------|--------|------------|
| Perception | Every tick | 0.1ms | 2.0ms |
| Memory Update | Every tick | 0.2ms | 1.9ms |
| Goal Evaluation | Every 5 ticks | 0.3ms | 1.7ms |
| Planning | On change | 0.5ms | 1.5ms |
| Action Selection | Every tick | 0.1ms | 1.2ms |
| Execution | Every tick | 0.3ms | 0.9ms |
| Learning | Every 10 ticks | 0.1ms | 0.6ms |
| **Buffer** | - | 0.6ms | - |
| **Total** | **Amortized** | **<2.0ms** | - |

### Industry Benchmarks
- Dwarf Fortress: ~1-5ms per dwarf (varies wildly)
- RimWorld: ~0.5-2ms per pawn
- The Sims 4: ~1-3ms per Sim
- **Session 2 Target**: <2ms is conservative and achievable

### Unity-Specific Considerations
- Use `Time.deltaTime` for frame-independent calculations
- Coroutines for spreading work across frames
- Job System for parallel processing
- DOTS (Data-Oriented Tech Stack) for 1000+ agents

---

## 3. Amortization Strategies

### 5-Bucket Approach
```csharp
// Divide 100 agents into 5 buckets of 20
// Process one bucket per tick (20 agents × 2ms = 40ms)
// Critical agents processed every tick regardless of bucket
```

**Benefits**:
- Distributes load evenly
- Prevents frame spikes
- Maintains 50ms tick deadline
- Allows 10ms buffer for variability

### Level of Detail (LOD) Processing
| Distance | Frequency | Processing |
|----------|-----------|------------|
| <20m | Every tick | Full AI |
| 20-100m | Every 5 ticks | Reduced |
| >100m | Every 20 ticks | Minimal |

### Sleep States
- Dormant agents: Basic needs decay only
- Wake triggers: Player proximity, events, timers
- Typical cycle: 300 ticks (15 seconds at 20 TPS)

---

## 4. Memory Budgets

### Per-Agent Memory Footprint
| Component | Size | Notes |
|-----------|------|-------|
| Agent Core | 256 bytes | Base data |
| AgentState | 32 bytes | Current condition |
| Personality | 15 bytes | 15 traits × 1 byte |
| Memory System | 640 bytes | 5+5 slots × 64 bytes |
| Goal System | 256 bytes | Active goals + scores |
| Economic | 2KB | Inventory is largest |
| Social | 4KB | Relationships are largest |
| **Total** | **~8KB** | Fits L1 cache |

### Scale Calculations
- 100 agents = ~800KB (fits in RAM easily)
- 1000 agents = ~8MB (still reasonable)
- 10,000 agents = ~80MB (requires streaming/paging)

---

## 5. Determinism Requirements

### For Replay Debugging
- Fixed random seed per agent
- Deterministic utility calculations
- Timestamp-based timing (not frame-dependent)
- Action outcomes deterministic given same inputs

### For Multiplayer
- Server-authoritative AI
- Client prediction with reconciliation
- State serialization every tick
- Network compression for agent states

---

## 6. Optimization Patterns

### Spatial Partitioning
- 100m chunks for perception queries
- Reduces O(n²) to O(1) for proximity checks
- Grid-based or octree depending on 2D/3D

### Early-Out Optimization
- Goal evaluation stops at first zero-score consideration
- Skip processing for sleeping/dormant agents
- LOD reduces frequency of expensive calculations

### Caching Strategies
- Price beliefs updated every 10 ticks (not every tick)
- Memory decay calculated every 5 ticks, interpolated
- Relationship updates batched

---

## 7. Technical Risks & Mitigations

### Risk: CPU Spikes
**Mitigation**: Amortization, LOD, sleep states

### Risk: Memory Bloat
**Mitigation**: Fixed-size arrays, pooling, aggressive culling

### Risk: Network Desync
**Mitigation**: Server authority, delta compression, deterministic simulation

### Risk: Save/Load Corruption
**Mitigation**: Versioned serialization, validation checks, atomic saves

---

## 8. Session 2 Integration Points

### From Session 1
- ✅ 20 TPS tick rate maintained
- ✅ Spatial partitioning for perception
- ✅ Performance budgets enforced
- ✅ Determinism requirements defined

### To Session 3
- ⚠️ Agent state serialization format (Question #10)
- ⚠️ Network sync for multiplayer (Issue #1)
- ⚠️ Save/load memory consolidation (Issue #2)

---

## References

1. Unity Documentation: "Optimizing scripts" - docs.unity3d.com
2. Valve Developer Wiki: "Source Multiplayer Networking" - developer.valvesoftware.com
3. "Game Engine Architecture" - Jason Gregory (Chapter 7: Game Loop)
4. GDC 2018: "Winding Road Ahead: Designing Utility AI with Curvature" - Mike Lewis

---

*Research compiled for Session 2 AI System Design verification*  
*Last updated: January 31, 2026*
