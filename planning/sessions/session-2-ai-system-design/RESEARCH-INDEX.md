# Session 2: AI System Design - Research Index

## Relevant Research Files

### Primary Research (Must Read)
- **R4 - Dwarf Fortress Agent Analysis**: 50 personality facets, 28 needs, memory systems, stress mechanics
- **R7 - AI Systems in Games**: Utility AI vs GOAP vs Behavior Trees, scalability analysis
- **R8 - PDF Synthesis**: AI-native design philosophy, hybrid architecture recommendations

### Supporting Research
- **R1 - Technical Research**: Performance constraints that affect AI agent count
- **R2 - Eco Game Analysis**: Agent population patterns, economic behaviors

## Key Research Findings

### AI Architecture Decision
- **Utility AI + Behavior Trees**: Proven in RimWorld, scales to 100-500 agents
- **GOAP**: Only 10-20 agents (insufficient for Societies)
- **Strategic layer**: Utility AI for economic/political decisions
- **Execution layer**: Behavior Trees for movement/actions

### Memory System (from DF)
- 3-tier memory: Short-term (8 slots), Long-term (8 slots), Core (permanent)
- Competition between memories creates emergent narrative
- Stress system affects decision-making

### Performance Targets
- Utility AI: 0.01-0.1ms per agent per decision
- Target: 100-200 agents with bucketing and spatial partitioning
- CPU budget: Must stay under 2ms per agent to maintain 20 TPS

## Open Questions for This Session
1. How many considerations (15-20)?
2. Personality trait system details?
3. AI voting behavior model?
4. Population elasticity algorithm?

## Links
- [Session 1: Technical Architecture](../session-1-technical-architecture/)
- [Research Folder](../../research/completed/)