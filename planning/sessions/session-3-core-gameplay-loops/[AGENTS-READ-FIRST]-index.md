# Session 3: Core Gameplay Loops

**Status**: COMPLETE - Compartmentalized  
**Last Updated**: 2026-02-01  

---

## Quick Access

| Document | Description | Lines | Focus |
|----------|-------------|-------|-------|
| [01-moment-to-moment-gameplay.md](./01-moment-to-moment-gameplay.md) | 5-15 minute gameplay loop | ~200 | Real-time actions |
| [02-session-gameplay.md](./02-session-gameplay.md) | 30 min - 2 hour sessions | ~250 | Session structure |
| [03-multi-session-arcs.md](./03-multi-session-arcs.md) | Days to weeks progression | ~220 | Long-term arcs |
| [04-player-archetypes.md](./04-player-archetypes.md) | 6 player types | ~300 | Play styles |
| [05-progression-feel.md](./05-progression-feel.md) | Emotional journey | ~180 | Growth pacing |
| [06-return-triggers.md](./06-return-triggers.md) | Engagement mechanics | ~200 | Retention |
| [07-ui-ux-paths.md](./07-ui-ux-paths.md) | Interface design | ~200 | User experience |
| [RESEARCH-INDEX.md](./RESEARCH-INDEX.md) | Research sources | ~50 | References |

---

## Session Overview

Session 3 defines what players actually *do* across different time scales:

1. **Moment-to-Moment** (5-15 min): Gathering, crafting, building
2. **Session Level** (30 min - 2 hours): Projects, economic, political activities
3. **Multi-Session** (Days to weeks): Progression arcs, server lifecycle

### Key Questions Answered

- What does a typical 15-minute play session look like?
- What does a 2-hour session look like?
- What does week 1 vs. week 4 feel like?
- How do different player types experience the game?
- What makes logging in tomorrow compelling?
- What's fun moment-to-moment vs. satisfying long-term?

---

## Dependencies

- **Requires**: 
  - [Session 1](../session-1-technical-architecture/) - Technical constraints (20 TPS, agent limits, bandwidth)
  - [Session 2](../session-2-ai-system-design/) - AI behaviors, economic systems, political systems
- **Informs**: 
  - [Session 4](../session-4-progression-and-balance/) - Progression systems
  - [Session 5](../session-5-governance-ux/) - Governance UX implementation
  - [Session 6](../session-6-prototyping/) - Prototype scope

---

## Player Archetypes

This session defines 6 core player types:

1. **Builder** - Construction, aesthetics, megaprojects
2. **Economist** - Trading, markets, optimization
3. **Politician** - Governance, leadership, influence
4. **Environmentalist** - Stewardship, sustainability
5. **Engineer** - Automation, efficiency, systems
6. **Socializer** - Community, relationships, events

See [04-player-archetypes.md](./04-player-archetypes.md) for full profiles.

---

## Technical Validation

All gameplay loops validated against Session 1 constraints:

| Constraint | Value | Status |
|------------|-------|--------|
| Tick Rate | 20 TPS (50ms) | ✅ All actions fit |
| Agent Limit | 25-100 | ✅ Respected |
| Per-Agent Budget | <2ms | ✅ Within budget |
| Bandwidth | 32 KB/s | ✅ ~4-20 KB/s |

See individual documents for detailed technical integration.

---

## Success Criteria

All criteria met:

- [x] Clear minute-to-minute activity flow ([01](./01-moment-to-moment-gameplay.md))
- [x] Session goals defined for different player types ([02](./02-session-gameplay.md))
- [x] Progression feel articulated across timeline ([05](./05-progression-feel.md))
- [x] Return triggers identified ([06](./06-return-triggers.md))
- [x] Critical UI/UX paths mapped ([07](./07-ui-ux-paths.md))
- [x] Information architecture specified ([07](./07-ui-ux-paths.md))
- [x] Player archetypes fully defined ([04](./04-player-archetypes.md))

---

## Status Explanation

- **READY FOR REVIEW**: Document is complete and awaiting review
- **IN PROGRESS**: Currently being edited or updated
- **COMPLETED**: Finalized and approved
- **COMPARTMENTALIZED**: Broken into focused sub-documents (this session)

---

## Navigation

- **Previous:** [Session 2: AI System Design](../session-2-ai-system-design/[AGENTS-READ-FIRST]-index.md)
- **Next:** [Session 4: Progression and Balance](../session-4-progression-and-balance/[AGENTS-READ-FIRST]-index.md)
- **Master Index:** [Planning Directory](../[AGENTS-READ-FIRST]-index.md)

---

## Legacy Document

The original single comprehensive document (`day3-core-gameplay-loops.md`, ~1,050 lines) has been compartmentalized into the documents above. It is preserved for reference but should not be used for active planning.

- **Deprecated**: `day3-core-gameplay-loops.md` (kept for reference)
- **Current**: Use the numbered documents above
