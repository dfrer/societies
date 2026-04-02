> 📚 **Complete Research Catalog**: See [Master Research Index](../../research/[MASTER-RESEARCH-INDEX].md)

---

# Session 2: AI System Design - Research Index

Research documents focused on AI architectures, agent behaviors, decision-making systems, and memory models for human-AI coexistence.

## Core AI Research (Session 2)

| Document | Key Findings | Architecture Impact |
|----------|--------------|---------------------|
| **r4-dwarf-fortress-agents.md** | 50 personality facets, 28 needs, 3-tier memory system, stress mechanics | Personality model, memory architecture, emergent storytelling |
| **r7-ai-systems-games.md** | Utility AI vs GOAP vs Behavior Trees scalability analysis | Hybrid architecture: Utility + BT selected |
| **r8-pdf-synthesis.md** | AI-native design philosophy, hybrid AI recommendations | Confirmed two-layer AI approach |

## AI Architecture Decision

**Selected**: **Utility AI + Behavior Trees** (proven in RimWorld)
- **Strategic Layer**: Utility AI for economic/political decisions
- **Execution Layer**: Behavior Trees for movement/actions
- **Scalability**: 100-500 agents (vs GOAP's 10-20 limit)
- **Performance**: 0.01-0.1ms per agent per decision

### Memory System (from Dwarf Fortress)

Three-tier memory architecture:
- **Short-term**: 8 slots (recent events, quick decay)
- **Long-term**: 8 slots (important memories, slower decay)
- **Core**: Permanent (values, traumas, identity)

*Competition between memories creates emergent narrative*

### Performance Targets

| Metric | Target | Limit |
|--------|--------|-------|
| Decision time | 0.01-0.1ms | <2ms to maintain 20 TPS |
| Agent count (MVP) | 20 | - |
| Agent count (post-MVP) | 100-200 | With bucketing & spatial partitioning |

## Supporting Research

| Document | Relevance to Session 2 |
|----------|------------------------|
| **r1-technical-constraints.md** | Performance limits affecting AI agent count |
| **r2-eco-game-analysis.md** | Agent population patterns, economic behaviors, AI market participation |

## Open Questions Resolved

1. ✅ **Considerations**: 15-20 considerations per decision
2. ✅ **Personality traits**: 50-facet model from Dwarf Fortress
3. 🔄 **AI voting behavior**: Under development
4. 🔄 **Population elasticity**: Algorithm design needed

## AI System Specifications

### Needs System (28 needs from DF)
Key needs: Socialization, creativity, purpose, security, comfort, entertainment

### Personality Facets (sample)
- Cooperation vs Independence
- Curiosity vs Traditionalism
- Emotional Stability vs Reactivity
- Empathy vs Self-Interest

### Stress & Coping
- Stress affects decision quality
- Coping mechanisms: meditation, socializing, destruction
- Breakdown behaviors at high stress

## Session Documents

- [01-ai-system-overview.md](01-ai-system-overview.md) - Executive summary
- [02-agent-architecture.md](02-agent-architecture.md) - AI layers and components
- [03-memory-system.md](03-memory-system.md) - Memory models and forgetting
- [04-decision-making.md](04-decision-making.md) - Utility AI implementation
- [05-behavior-execution.md](05-behavior-execution.md) - Behavior trees and actions
- [06-ai-economy.md](06-ai-economy.md) - AI economic behaviors
- [07-ai-governance.md](07-ai-governance.md) - AI political participation

---

## Navigation

- [← Previous: Session 1 Technical Research](../session-1-technical-architecture/RESEARCH-INDEX.md)
- [→ Next: Session 3 Gameplay Research](../session-3-core-gameplay-loops/RESEARCH-INDEX.md)
- [Session 1: Technical Architecture](../session-1-technical-architecture/)
- [Session 3: Core Gameplay Loops](../session-3-core-gameplay-loops/)
- [Research Folder](../../research/completed/)
