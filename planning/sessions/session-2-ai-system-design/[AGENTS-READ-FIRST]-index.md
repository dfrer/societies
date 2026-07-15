# Session 2: AI System Design - Deep Planning Document

**Session**: 2 of 7  
**Status**: COMPLETE - Compartmentalized into 7 files  
**Date Started**: January 31, 2026  
**Date Completed**: January 31, 2026  
**Location**: planning/sessions/session-2-ai-system-design/

> **Canonical alignment (2026-07-14):** Aspirational AI design reference. Read [PRODUCT-THESIS.md](../../PRODUCT-THESIS.md), [`planning/active/`](../../active/), and [`CURRENT_BUILD.md`](../../../CURRENT_BUILD.md) first; LLM-mediated expression is not current simulation authority.

---

## Quick Navigation Table

| File | Contents | Lines | Key Topics |
|------|----------|-------|------------|
| 01-core-ai-architecture.md | Core AI Systems | ~1,400 | Architecture, Goals, Memory |
| 02-economic-behavior.md | Economic Behavior | ~1,100 | Price beliefs, Trading, Careers |
| 03-political-social-behavior.md | Political & Social | ~1,800 | Voting, Factions, Relationships |
| 04-population-personality.md | Population & Personality | ~1,100 | Elasticity, 19 facets |
| 05-narrative-debugging.md | Narrative & Debug | ~1,200 | Gossip, Debug tools, Brain configs |
| 06-ai-skills-reference.md | Reference Materials | ~700 | Decisions, Questions, Skills |
| (this file) | Navigation Hub | ~300 | Overview, quick links |

---

## Document Purpose

This navigation hub provides a roadmap through the AI system design session, which defines the behavioral architecture, decision-making systems, and personality models that will bring the game's societies to life. Use this index to quickly locate specific AI features and understand how the systems interconnect.

---

## When to Read Each File

### Core Understanding
Start with [01-core-ai-architecture.md](01-core-ai-architecture.md) to understand the fundamental AI architecture, goal-driven behavior, memory systems, and the perception-to-action pipeline.

### Economic Systems
Read [02-economic-behavior.md](02-economic-behavior.md) for price belief systems, trading behaviors, career selection, and supply-demand elasticity modeling.

### Social Dynamics
Read [03-political-social-behavior.md](03-political-social-behavior.md) to understand voting systems, political factions, relationship networks, and social influence mechanics.

### Population Management
Read [04-population-personality.md](04-population-personality.md) for population elasticity models, the 19-facet personality system, and cultural adaptation mechanics.

### Development Tools
Read [05-narrative-debugging.md](05-narrative-debugging.md) for gossip systems, debugging tools, brain visualization configs, and narrative generation capabilities.

### Project Management
Read [06-ai-skills-reference.md](06-ai-skills-reference.md) for decision frameworks, question templates, skill implementation guides, and cross-session dependencies.

---

## Key Decisions Documented

The following 8 major architectural decisions were finalized during this session:

1. **Need-Based Architecture**: Selected over action-based architecture with Maslow-style hierarchy and dynamic need balancing
2. **Belief-Driven Economics**: Beliefs drive all economic behaviors - prices, careers, and trading decisions
3. **Need-Goal Mapping**: Each need maps to 2-5 goal types with automatic conversion when needs change
4. **Goal-Based Action Generation**: Actions generated via templates based on current active goals
5. **Elastic Population Model**: Population scales dynamically based on economic conditions and migration flows
6. **19-Facet Personality System**: Five Traits × three Facets each + 4 fixed facets for consistent character depth
7. **Gossip as Primary Social Mechanism**: Social information propagates through gossip, enabling emergent narratives
8. **Debug Visualization Tools**: Brain visualization, action logging, and time scaling for development

---

## Performance Budgets

| Metric | MVP Target | Stretch Target |
|--------|------------|----------------|
| Agents | 25 | 100+ |
| Tick Rate | 20 TPS | 20 TPS |
| Per Agent | <2ms | <2ms |
| Memory | ~8KB/agent | ~8KB/agent |

**Note**: These budgets inform implementation decisions across all AI systems.

---

## Cross-Reference Map

```
01-core-ai-architecture.md
├── Architecture Overview
│   └── Need-Based Decision Architecture
├── Needs System
│   └── [→ 04-population-personality.md: Population needs]
├── Goal-Based Generation
│   └── [→ 02-economic-behavior.md: Economic goals]
│   └── [→ 03-political-social-behavior.md: Social goals]
├── Memory & Knowledge
│   └── [→ 03-political-social-behavior.md: Relationship memory]
└── Perception Pipeline

02-economic-behavior.md
├── Beliefs System
│   └── Price beliefs [→ 06-ai-skills-reference.md: Pricing]
├── Trading Behavior
│   └── [→ 03-political-social-behavior.md: Trade relationships]
├── Career Systems
│   └── [→ 04-population-personality.md: Career preferences]
└── Market Dynamics

03-political-social-behavior.md
├── Voting Systems
├── Factions & Politics
│   └── [→ 05-narrative-debugging.md: Political gossip]
├── Social Networks
│   └── [→ 05-narrative-debugging.md: Relationship tracking]
└── Influence Mechanics

04-population-personality.md
├── Population Elasticity
│   └── [→ 02-economic-behavior.md: Migration economics]
├── Personality System
│   └── 19 facets [→ 01-core-ai-architecture.md: Need priorities]
└── Cultural Adaptation

05-narrative-debugging.md
├── Gossip System
│   └── [→ 03-political-social-behavior.md: Social propagation]
├── Debug Tools
│   └── Brain visualization [→ 01-core-ai-architecture.md: Needs display]
└── Narrative Generation

06-ai-skills-reference.md
├── Decision Frameworks
├── Cross-Session Dependencies
│   ├── [→ Session 1: Technical foundation]
│   └── [→ Session 3: Combat/goals integration]
└── Implementation Questions
```

---

## Research File References

Key research documents that informed this session's design:

- [r4-dwarf-fortress-agents.md](../../research/r4-dwarf-fortress-agents.md) - Agent behavior patterns, happiness metrics, and goal prioritization
- [r7-utility-ai-systems.md](../../research/r7-utility-ai-systems.md) - Utility AI systems, consideration curves, and decision-making algorithms
- [r8-dredge-atmosphere-design.md](../../research/r8-dredge-atmosphere-design.md) - Narrative tension mechanics and atmospheric AI behaviors

**Citation Format**: Use `[rX-filename.md]` to reference research in implementation files.

---

## Important Notes for Agents

### Cross-Reference Format
When linking between files, use: `[Section Name](XX-file.md#anchor)`

Example: `[Belief System](02-economic-behavior.md#belief-system)`

### Citation Format
When referencing research, use: `[r4-dwarf-fortress-agents.md]` or similar.

### Original File Archive
The original single-file planning document has been archived at:
`planning/archive/day2-ai-system-design.legacy.md`

**Do not edit the archived file** - all updates should be made to the compartmentalized files in this directory.

### File Structure
- Each `.md` file is self-contained but references others
- Use the Quick Navigation Table above for orientation
- Anchor links use kebab-case section titles (e.g., `#need-based-architecture`)

---

## Quick Lookup Guide

**Looking for specific AI features?** Find them here:

| What You Need | Where to Find It |
|---------------|------------------|
| Price beliefs and trading | [02-economic-behavior.md](02-economic-behavior.md) |
| Career selection logic | [02-economic-behavior.md](02-economic-behavior.md#career-systems) |
| Voting and factions | [03-political-social-behavior.md](03-political-social-behavior.md) |
| Relationship networks | [03-political-social-behavior.md](03-political-social-behavior.md#social-networks) |
| Population migration | [04-population-personality.md](04-population-personality.md#population-elasticity) |
| 19-facet personality | [04-population-personality.md](04-population-personality.md#personality-system) |
| Gossip propagation | [05-narrative-debugging.md](05-narrative-debugging.md#gossip-system) |
| Debug visualization | [05-narrative-debugging.md](05-narrative-debugging.md#debug-tools) |
| Need-based architecture | [01-core-ai-architecture.md](01-core-ai-architecture.md) |
| Goal generation | [01-core-ai-architecture.md](01-core-ai-architecture.md#goal-based-generation) |
| Performance budgets | See table above or [01-core-ai-architecture.md](01-core-ai-architecture.md) |
| Decision frameworks | [06-ai-skills-reference.md](06-ai-skills-reference.md) |
| Open questions | [06-ai-skills-reference.md](06-ai-skills-reference.md#open-questions) |

---

## Session Status

**This session has been compartmentalized from original single file.**

The original planning document (`day2-ai-system-design.md`) has been split into 6 focused files + this navigation hub to improve maintainability and readability. Each file covers a specific domain of AI behavior and can be read independently.

### Compartmentalization Date
January 31, 2026

### Files Created
1. `01-core-ai-architecture.md`
2. `02-economic-behavior.md`
3. `03-political-social-behavior.md`
4. `04-population-personality.md`
5. `05-narrative-debugging.md`
6. `06-ai-skills-reference.md`
7. `[AGENTS-READ-FIRST]-index.md` (this file)

---

## Next Steps

**Proceed to Session 3**: Core Gameplay Loops

The next planning session will integrate the AI behavior systems from Session 2 with core gameplay mechanics, creating unified goal hierarchies that include both social/economic and gameplay objectives.

**Session 3 Location**: `planning/sessions/session-3-core-gameplay-loops/`

**Handoff Notes**:
- AI goal system from Session 2 provides foundation for combat goals
- Economic behaviors inform resource management in combat
- Social relationships will impact combat cooperation and faction warfare
- Performance budgets remain consistent across sessions

---

*Last Updated: January 31, 2026*
*Session Owner: AI Planning Team*
*Review Cycle: As needed during implementation*