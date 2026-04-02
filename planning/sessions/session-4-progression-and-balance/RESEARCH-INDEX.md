> 📚 **Complete Research Catalog**: See [Master Research Index](../../research/[MASTER-RESEARCH-INDEX].md)

---

# Session 4: Progression & Balance - Research Index

Research documents covering skill systems, economic balance, difficulty scaling, and long-term progression curves.

## Core Progression Research (Session 4)

| Document | Key Findings | Balance Applications |
|----------|--------------|---------------------|
| **r2-eco-game-analysis.md** | Skill progression curves, exponential costs, player-driven pricing | Tech tree structure, economic balance |
| **r3-eco-technical-postmortem.md** | Economic transaction volume, performance bottlenecks | Scaling limits, optimization needs |
| **r6-multiplayer-simulation-tech.md** | Entity optimization, scaling curves | Performance-informed balance |

## Skill Progression System (from Eco)

### Exponential Cost Curve

| Level | SP Cost | Cumulative | Design Purpose |
|-------|---------|------------|----------------|
| 1 | 0 | 0 | Immediate gratification |
| 2 | 0 | 0 | Early specialization |
| 3 | 5 | 5 | First real choice |
| 4 | 15 | 20 | Building investment |
| 5 | 50 | 70 | Deep specialization |
| 6 | 100 | 170 | Expert territory |
| 7 | 300 | 470 | Master level |
| 8 | 500 | 970 | Legendary |

*Exponential costs create natural interdependence between players*

### Specialization vs Generalization

- **Generalist**: Can do everything at basic level
- **Specialist**: Few skills at maximum efficiency
- **Hybrid**: 2-3 core skills + supporting abilities
- **Group interdependence** required for advanced tech

## Economic Balance

### Currency Progression

1. **Personal Credit** (barter, trust-based)
2. **Backed Currency** (resource-backed, player-defined)
3. ** Fiat Currency** (government-issued, law-backed)

### Market Mechanics

| Element | Implementation | Research Source |
|---------|---------------|-----------------|
| **Player-driven pricing** | No NPC shops, pure supply/demand | Eco analysis |
| **Market makers** | AI agents stabilize small populations | AI research |
| **Price discovery** | Historical data, trend visualization | Paradox UI patterns |
| **Transaction limits** | Performance-based (from Eco's 100k tx/min limit) | R3 postmortem |

## Threat Timeline & Difficulty Scaling

### Server Lifecycle Phases

| Days | Phase | Threat Level | Player Focus |
|------|-------|--------------|--------------|
| 1-10 | **Establishment** | Low | Survival, learning |
| 10-30 | **Industrialization** | Medium | Infrastructure, meteor prep |
| 30-60 | **Environmental** | High | Pollution, ecosystem |
| 60-120 | **Advanced** | Very High | Space, politics, crisis |
| 120+ | **Expansion** | Variable | Endgame content |

### Scaling Factors

- **Server size**: 10 vs 50 vs 100 players
- **Resource scarcity**: Depletion curves
- **AI difficulty**: Agent intelligence settings
- **Threat intensity**: Meteor frequency, pollution speed

## Supporting Research

| Document | Relevance to Session 4 |
|----------|------------------------|
| **r1-technical-constraints.md** | Performance limits affecting balance decisions |
| **r5-paradox-games-politics.md** | Difficulty scaling patterns, crisis management |

## Open Questions

1. **Complete tech tree**: Full dependency graph needed
2. **Resource rates**: Generation vs consumption balancing
3. **Wealth distribution**: Gini coefficient targets
4. **Server lifecycle**: Reset vs persistence decisions

## Balance Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Time to max skill | 40-60 hours | Playtesting |
| Economic equilibrium | 10-15% daily transaction volume | Analytics |
| Threat appropriateness | 60% success rate | Event tracking |
| Player retention | 30-day retention >40% | Cohort analysis |

## Session Documents

- [01-progression-overview.md](01-progression-overview.md) - Executive summary
- [02-skill-system.md](02-skill-system.md) - Tech tree and advancement
- [03-economy-balance.md](03-economy-balance.md) - Market and pricing
- [04-difficulty-scaling.md](04-difficulty-scaling.md) - Threat curves
- [05-server-lifecycle.md](05-server-lifecycle.md) - Persistence and reset

---

## Navigation

- [← Previous: Session 3 Gameplay Research](../session-3-core-gameplay-loops/RESEARCH-INDEX.md)
- [→ Next: Session 5 Governance Research](../session-5-governance-mechanics/RESEARCH-INDEX.md)
- [Session 3: Core Gameplay Loops](../session-3-core-gameplay-loops/)
- [Session 5: Governance Mechanics](../session-5-governance-mechanics/)
- [Research Folder](../../research/completed/)
