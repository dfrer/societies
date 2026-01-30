# Session 3: Core Gameplay Loops - Deep Planning Document

**Planning Session**: 3 of 7  
**Status**: Draft  
**Date Started**: [Not Started]  
**Date Completed**: [Not Completed]

---

## Purpose

Define what players actually *do* moment-to-moment, hour-to-hour, session-to-session. This document maps the complete player experience across different time scales and player archetypes.

---

## Key Questions Addressed

1. What does a typical 15-minute play session look like?
2. What does a 2-hour session look like?
3. What does week 1 vs. week 4 feel like?
4. How do different player types experience the game?
5. What makes logging in tomorrow compelling?
6. What's fun moment-to-moment vs. satisfying long-term?

---

## Research Summary
**Tier 1 Sources**: [To be filled during research phase]
**Key Insights**: [Major learnings from research]

---

## Dependencies

- **Requires**: Session 2 (AI System Design) - Agent behavior informs player interactions
- **Informs**: Session 4 (Progression), Session 5 (Governance UX), Session 6 (Prototype scope)

---

## 1. Moment-to-Moment Gameplay (5-15 minutes)

### Core Activity Loop

```mermaid
graph LR
    A[Gather Resources] --> B[Craft Items]
    B --> C[Build/Upgrade]
    C --> D[Trade/Sell]
    D --> E[Check Goals]
    E --> A
    
    style A fill:#bfb,stroke:#333,stroke-width:2px
```

### Gathering Resources

**Activities**:
- Chop trees (wood)
- Mine stone/ore (minerals)
- Harvest plants (food, fiber)
- Hunt animals (meat, hides)
- Fish (food)
- Collect water

**Feedback Loops**:
- Resource counter increases
- Tool durability decreases (maintenance loop)
- Skill XP gain (progression)
- Inventory management decisions

**Fun Factors**:
- Visual/audio feedback (satisfying chop sounds)
- Resource rarity (excitement finding rare ore)
- Efficiency optimization (better tools = faster)

### Crafting Items

**Activities**:
- Open crafting menu
- Select recipe
- Ensure materials available
- Craft item
- Quality/variance based on skill

**Feedback Loops**:
- Inventory changes
- Skill XP gain
- New capabilities unlocked
- Quality rating (pride in workmanship)

### Building Structures

**Activities**:
- Select building type
- Place foundation
- Add materials progressively
- See construction progress
- Finished structure provides benefit

**Satisfaction Sources**:
- Visual transformation (empty lot → house)
- Functional benefit (shelter, storage)
- Aesthetic expression (design choices)
- Permanent impact on world

---

## 2. Session Gameplay (30 minutes - 2 hours)

### Session Arc Flow

```mermaid
graph TD
    A[Login] --> B[Assess World State]
    B --> C[Choose Focus Area]
    
    C --> D[Project Work]
    C --> E[Economic Activities]
    C --> F[Political Activities]
    C --> G[Social Activities]
    C --> H[Exploration]
    
    D --> I[Complete Project]
    E --> I
    F --> I
    G --> I
    H --> I
    
    I --> J[Review Progress]
    J --> K[Plan Tomorrow]
    K --> L[Logout]
```

### Project-Based Gameplay

**Example: Build a Workshop**

```mermaid
graph LR
    A[Gather Stone] --> B[Craft Tools]
    C[Gather Wood] --> D[Make Planks]
    B --> E[Build Foundation]
    D --> E
    E --> F[Add Walls]
    F --> G[Add Roof]
    G --> H[Install Crafting Stations]
    H --> I[Workshop Complete!]
```

**Session Distribution**:
- Small project: 15-30 minutes (craft better tools)
- Medium project: 1-2 hours (build workshop)
- Large project: Multiple sessions (build town center)

### Economic Activities

**Running a Store**:
1. Check inventory levels
2. Set prices based on market
3. Open store for business
4. AI/human customers visit
5. Manage stock, adjust prices
6. Close up, count profits

**Fulfilling Contracts**:
1. Browse contract board
2. Accept delivery contract
3. Gather/produce required items
4. Deliver to recipient
5. Receive payment + reputation

### Political Activities

**Proposing a Law**:
1. Identify problem/need
2. Draft law (with UI help)
3. Gather support (campaign)
4. Proposal submitted
5. Voting period (24-48 hours)
6. Result announced
7. If passed: Law enacted

**Campaigning**:
- Talk to AI agents about issues
- Post announcements
- Participate in debates
- Build coalition

---

## 3. Multi-Session Arcs (Days to Weeks)

### Week 1: Foundation

```mermaid
gantt
    title Week 1 Player Journey
    dateFormat  X
    axisFormat %d
    
    section Survival
    Basic Shelter       :a1, 0, 2
    Food Security       :a2, 1, 3
    Tool Creation       :a3, 2, 4
    
    section Discovery
    Explore Area        :b1, 2, 5
    Meet Neighbors      :b2, 3, 6
    Find Resources      :b3, 4, 7
    
    section Specialization
    Choose Career       :c1, 5, 7
    Skill Development   :c2, 6, 7
```

**Week 1 Feel**: Overwhelming but exciting. Learning systems. Meeting neighbors. Basic survival achieved.

### Week 2: Community

**Activities**:
- Join/form neighborhood
- Begin specialization
- First trades with AI
- Basic infrastructure (paths, shared storage)
- Participate in first election

**Week 2 Feel**: Social connections form. Economic specialization begins. First political experiences.

### Week 3: Industry

**Activities**:
- Town formation (if 3+ players)
- Industrial production begins
- First laws enacted
- Meteor preparation awareness
- Skill mastery in chosen path

**Week 3 Feel**: Collaborative projects. Governance complexity. Urgency building.

### Week 4: Crisis & Advancement

**Activities**:
- Meteor preparation (if day 30 approaching)
- Advanced technology unlocked
- Complex political situations
- Environmental challenges emerge
- Long-term planning required

**Week 4 Feel**: High stakes. Cooperation essential. Satisfaction from progress.

---

## 4. Player Archetypes & Their Loops

### The Builder

```mermaid
graph TD
    A[Design Vision] --> B[Gather Materials]
    B --> C[Build Structure]
    C --> D[Decorate/Refine]
    D --> E[Share/Achievement]
    E --> A
```

**Core Loop**: Design → Build → Admire → Share
**Session Goals**: Complete construction projects
**Multi-Session**: Megaprojects, town design
**Motivation**: Aesthetic expression, permanent impact

### The Economist

```mermaid
graph TD
    A[Market Analysis] --> B[Identify Opportunity]
    B --> C[Production/Acquisition]
    C --> D[Sell at Profit]
    D --> E[Wealth Growth]
    E --> A
```

**Core Loop**: Analyze → Produce → Trade → Profit
**Session Goals**: Execute trades, optimize supply chains
**Multi-Session**: Build business empire, corner markets
**Motivation**: Optimization, wealth accumulation

### The Politician

```mermaid
graph TD
    A[Identify Issue] --> B[Build Coalition]
    B --> C[Propose Solution]
    C --> D[Campaign/Vote]
    D --> E[Implement Policy]
    E --> A
```

**Core Loop**: Observe → Organize → Propose → Influence
**Session Goals**: Pass legislation, win elections
**Multi-Session**: Rise through government ranks
**Motivation**: Power, social impact, leadership

### The Environmentalist

```mermaid
graph TD
    A[Monitor Ecosystem] --> B[Identify Threat]
    B --> C[Raise Awareness]
    C --> D[Propose Laws]
    D --> E[See Impact]
    E --> A
```

**Core Loop**: Monitor → Alert → Protect → Restore
**Session Goals**: Environmental projects, conservation
**Multi-Session**: Restore damaged ecosystems
**Motivation**: Stewardship, sustainability

### The Engineer

```mermaid
graph TD
    A[Identify Inefficiency] --> B[Design Solution]
    B --> C[Build Automation]
    C --> D[Optimize Further]
    D --> E[Share Design]
    E --> A
```

**Core Loop**: Problem → Design → Build → Optimize
**Session Goals**: Create automated systems
**Multi-Session**: Complex infrastructure networks
**Motivation**: Efficiency, problem-solving

### The Socializer

```mermaid
graph TD
    A[Connect with Players] --> B[Organize Events]
    B --> C[Facilitate Trade]
    C --> D[Mediate Conflicts]
    D --> E[Build Community]
    E --> A
```

**Core Loop**: Connect → Organize → Facilitate → Unite
**Session Goals**: Social events, community building
**Multi-Session**: Town culture, traditions
**Motivation**: Social bonds, community impact

---

## 5. Progression Feel Over Time

### Experience Timeline

```mermaid
graph LR
    subgraph "Week 1"
        A1[Survival<br/>Learning Curves] 
        A2[Basic Needs Met]
    end
    
    subgraph "Week 2"
        B1[Community<br/>First Connections]
        B2[Specialization]
    end
    
    subgraph "Week 3"
        C1[Industry<br/>Collaboration]
        C2[First Governance]
    end
    
    subgraph "Week 4"
        D1[Crisis<br/>Meteor Prep]
        D2[Advanced Tech]
    end
    
    subgraph "Week 5+"
        E1[Endgame<br/>Existential Threats]
        E2[Space Expansion]
    end
    
    A2 --> B1
    B2 --> C1
    C2 --> D1
    D2 --> E1
```

### Emotional Journey

| Week | Primary Emotion | Secondary | Challenge Level |
|------|----------------|-----------|----------------|
| 1 | Curiosity | Anxiety | Medium |
| 2 | Connection | Competition | Medium |
| 3 | Pride | Pressure | High |
| 4 | Urgency | Accomplishment | Very High |
| 5+ | Determination | Legacy | Extreme |

---

## 6. Compelling Return Triggers

### Why Log In Tomorrow?

**In-Progress Projects**:
- Construction in progress (can't wait to see it finished)
- Crops growing (need to harvest)
- Crafting queue (items ready)
- Research completing

**Commitments**:
- Contracts to fulfill (reputation at stake)
- Political obligations (vote coming up)
- Social promises (meeting other players)
- Economic orders (customers waiting)

**Scheduled Events**:
- Elections (vote deadline)
- Town meetings (governance decisions)
- Market openings (trading opportunities)
- Disaster warnings (meteor preparation)

### FOMO (Fear of Missing Out)

**Creates Urgency**:
- Limited-time market opportunities
- Election deadlines
- Event windows (comets, weather)
- Resource scarcity phases

**Balance Needed**: FOMO creates engagement but too much creates anxiety

### Obligation vs. Choice

**Healthy Obligations**:
- Chosen contracts (voluntary commitment)
- Self-set projects (personal goals)
- Social bonds (friends playing)

**Avoid**:
- Mandatory daily tasks (chores)
- Punishment for absence
- FOMO-based manipulation

---

## 7. UI/UX Critical Paths

### Gathering → Crafting → Building

```mermaid
graph LR
    A[Resource HUD] --> B[Inventory]
    B --> C[Crafting Menu]
    C --> D[Recipe Selection]
    D --> E[Material Check]
    E --> F[Craft]
    F --> G[Place/Build]
```

**Key UI Elements**:
- Resource counter (always visible)
- Quick-access crafting
- Build preview
- Progress indicators

### Economic Loop

```mermaid
graph LR
    A[Market Browser] --> B[Price Comparison]
    B --> C[Inventory Check]
    C --> D[Execute Trade]
    D --> E[Wallet Update]
```

**Key UI Elements**:
- Price history graphs
- Market depth visualization
- Quick-buy/quick-sell
- Contract board

### Governance Loop

```mermaid
graph LR
    A[Law Browser] --> B[Proposal Drafting]
    B --> C[Support Building]
    C --> D[Voting Interface]
    D --> E[Results View]
```

**Key UI Elements**:
- Plain-language law summaries
- Impact prediction
- Voting reminders
- Election countdowns

### Stewardship Loop

```mermaid
graph LR
    A[Environmental Dashboard] --> B[Data Analysis]
    B --> C[Threat Identification]
    C --> D[Response Planning]
    D --> E[Implementation]
```

**Key UI Elements**:
- Pollution heat maps
- Population graphs
- Trend indicators
- Alert system

---

## 8. Information Architecture

### What to Show When

| Context | Priority Information | Secondary |
|---------|---------------------|-----------|
| **General Play** | Resources, Health, Current Goal | Weather, Time, Notifications |
| **Trading** | Prices, Inventory, Wallet | Market trends, Recent trades |
| **Building** | Materials needed, Preview | Durability, Skill bonuses |
| **Governance** | Active votes, Laws, Support | Historical data, Projections |
| **Crisis** | Time remaining, Preparation % | Resource locations, Team status |

### Notification Strategy

**Critical** (Immediate popup + sound):
- Election results
- Contract deadlines
- Disasters
- Direct messages

**Important** (Sidebar notification):
- Market price changes
- Skill level ups
- Project completions
- Law changes

**Background** (Log only):
- Routine agent activities
- Minor economic shifts
- Weather changes

---

## 9. Open Questions & Future Research

### Unresolved Questions

- [ ] What's the optimal session length for different player types?
- [ ] How do we prevent "analysis paralysis" in governance?
- [ ] What's the right balance of solo vs. group activities?
- [ ] How much UI complexity is too much?
- [ ] What creates the strongest "just one more thing" feeling?

### Research Needs

- [ ] Player session analysis from similar games
- [ ] UI/UX patterns in complex simulation games
- [ ] Engagement psychology in persistent worlds
- [ ] Onboarding best practices for complex games

---

## 10. Decisions Log

| Date | Decision | Rationale |
|------|----------|-----------|
| Day 0 | Project-based gameplay | Gives clear goals, sense of accomplishment |
| Day 0 | Multiple archetypes | Different players find different fun |
| Day 0 | Scheduled events | Create natural return triggers |
| Day 0 | Contextual UI | Reduce information overload |

---

## 11. Gameplay Design Skills & Player Psychology

### Overview

This section documents the game design skills required for creating engaging gameplay loops, session structures, and player experiences in Societies. These skills cover session design, player psychology, engagement mechanics, and UI/UX for complex simulations.

### 11.1 Core Gameplay Design Skills

#### Skill 1: Session-Based Game Design

**Research Sources:**
- **Foundational:** "The Art of Game Design" by Jesse Schell (book)
- **Psychology:** "A Theory of Fun for Game Design" by Raph Koster
- **Engagement:** GDC talks on engagement loops and retention
- **Psychology:** Flow theory by Mihaly Csikszentmihalyi
- **Behavioral:** Behavioral psychology (B.F. Skinner, operant conditioning)

**Key Competencies:**
- Flow state design (challenge matching player skill)
- Progression curves (power, complexity, difficulty over time)
- Session boundary design (natural stopping points vs compulsion)
- Return trigger mechanics (FOMO, obligation, curiosity)
- Activity loop design (core, secondary, meta loops)
- Time-scale mapping (5-minute, 2-hour, daily, weekly cycles)

**Creation Process:**
1. Document our session structures:
   - 5-15 minutes: Gather → Craft → Build → Trade
   - 30 min-2 hours: Project completion or economic/political activities
   - Days-weeks: Multi-session projects and progression
   - Week-by-week: Server lifecycle phases
2. Map emotional journey across sessions (frustration → engagement → satisfaction)
3. Research similar games' session design (Factorio, RimWorld, Stardew Valley)
4. Create playtest protocols for session validation
5. Design return triggers for each time scale

**Verification Steps:**
- [ ] Can map activities to different session lengths
- [ ] Can design natural stopping points
- [ ] Can create compelling return triggers
- [ ] Flow state maintained across different player skill levels
- [ ] Session length feels appropriate to activity type

---

#### Skill 2: Player Archetype Analysis

**Research Sources:**
- **Taxonomy:** Bartle Taxonomy of Player Types (Achiever, Explorer, Socializer, Killer)
- **Extended:** Hexad of Player Types (modified for modern games)
- **Motivations:** Yee's player motivations (immersion, achievement, social, etc.)
- **Psychology:** Psychographic segmentation in games
- **Design:** Player-centered design methodologies

**Key Competencies:**
- Archetype identification and profiling
- Loop design per archetype (distinct activities)
- Cross-archetype interaction design
- Feature prioritization by archetype impact
- Archetype migration (how players change over time)
- Motivation mapping to game mechanics

**Creation Process:**
1. Document our 6 archetypes with detailed profiles:
   - **Builder:** Create structures, perfect layouts, leave legacy
   - **Economist:** Optimize markets, maximize efficiency, wealth accumulation
   - **Politician:** Lead, debate, shape society, wield power
   - **Environmentalist:** Balance ecosystem, sustainability, nature harmony
   - **Engineer:** Solve problems, automation, complex systems
   - **Socializer:** Community building, relationships, collaborative projects
2. Create persona documents for each archetype
3. Map activities to archetype motivations
4. Research competitive games' archetype designs
5. Validate with actual player data when available
6. Document archetype overlap (players often blend types)

**Verification Steps:**
- [ ] Can identify which archetype a feature serves
- [ ] Each archetype has distinct gameplay loops
- [ ] Archetypes interact meaningfully
- [ ] All major features serve at least one archetype
- [ ] Can design for archetype migration

---

#### Skill 3: Engagement & Retention Design

**Research Sources:**
- **Behavioral:** "Hooked" by Nir Eyal (behavioral design framework)
- **Economics:** Behavioral economics (loss aversion, sunk cost, endowment effect)
- **Psychology:** Habit formation psychology (cue-routine-reward)
- **Games:** Variable reward schedules in games
- **Ethics:** Ethical engagement design (avoiding dark patterns)

**Key Competencies:**
- FOMO (Fear of Missing Out) mechanics (ethical implementation)
- Progress preservation systems
- Social obligation design (without exploitation)
- Curiosity-driven return triggers
- Variable reward schedules
- Habit formation loops
- Sunk cost utilization (ethical boundaries)

**Creation Process:**
1. Document FOMO systems in Societies:
   - World evolves while offline (meteor threat, resource changes)
   - Scheduled events (elections, markets, disasters)
   - Social dependencies (collaborative projects)
2. Create retention metric targets:
   - Day 1: 60%
   - Day 7: 30%
   - Day 30: 15%
3. Research ethical vs exploitative engagement
4. Test return trigger effectiveness through playtesting
5. Design progress preservation (catch-up mechanics)

**Verification Steps:**
- [ ] Can create ethical FOMO mechanics
- [ ] Return triggers are compelling but not manipulative
- [ ] Progress loss is minimized (or meaningful)
- [ ] Habit formation supports player goals
- [ ] Retention metrics meet targets without exploitation

---

#### Skill 4: UI/UX for Complex Simulations

**Research Sources:**
- **Architecture:** Information architecture principles (Morville & Rosenfeld)
- **Visualization:** Edward Tufte's principles (envisioning information)
- **Games:** Game UI design patterns and best practices
- **Accessibility:** WCAG guidelines adapted for games
- **Complex Systems:** Complex system visualization research

**Key Competencies:**
- Progressive disclosure strategies (complexity layering)
- Information hierarchy design (what to show when)
- Contextual UI systems (mode-appropriate displays)
- Notification priority management
- Complex data visualization
- Accessibility implementation
- Decision support interfaces

**Creation Process:**
1. Document our contextual UI approach:
   - Mode detection (General Play, Trading, Building, Governance, Crisis)
   - Priority-based information display
   - Progressive complexity disclosure
2. Create wireframes for critical paths (Gather→Craft→Build)
3. Design notification system (Critical, Important, Background)
4. Research complex game UIs (Paradox games, Dwarf Fortress)
5. Conduct usability testing protocols
6. Implement accessibility features

**Verification Steps:**
- [ ] Can reduce complex information to digestible chunks
- [ ] UI adapts appropriately to context
- [ ] Notifications are prioritized correctly
- [ ] Complex systems are visualized clearly
- [ ] New players can understand the interface
- [ ] Interface supports expert efficiency

---

### 11.2 Gameplay Skill Development Workflow

#### Skill Research Process

**For Each Gameplay Design Skill:**

**Step 1: Literature Review (3-4 hours)**
- Read foundational texts and theories
- Watch GDC talks on specific topics
- Analyze similar games' implementations
- Review academic research (psychology, behavioral economics)

**Step 2: Synthesis (2-3 hours)**
- Extract principles relevant to Societies
- Document how principles apply to our design
- Note deviations from standard practices
- Create design rationale statements
- Map theory to implementation

**Step 3: Prototyping (1-2 weeks)**
- Create minimal testable implementations
- Design playtest scenarios
- Define success metrics
- Prepare testing protocols

**Step 4: Validation (Ongoing)**
- Test with prototypes
- Gather playtest feedback
- Analyze metrics (retention, session length, activities)
- Iterate based on data
- Update skills with learnings

---

### 11.3 Skills to Create Priority List

**Immediate (Week 1-2):**
1. Core Activity Loop Design
2. Session Structure Patterns
3. Player Onboarding Flows
4. Contextual UI Systems

**Short-term (Month 1-2):**
5. Player Archetype Implementation
6. Engagement Loop Design
7. Return Trigger Mechanics
8. Information Architecture

**Medium-term (Month 2-3):**
9. Multi-Session Arc Design
10. Social Feature Integration
11. Accessibility Implementation
12. Complex Data Visualization

**Ongoing:**
13. Playtesting Methodologies
14. Metrics Analysis
15. Ethical Design Practices
16. Player Feedback Integration

---

### 11.4 Gameplay Research Resources

#### Design Theory
| Resource | Author | Focus |
|----------|--------|-------|
| The Art of Game Design | Jesse Schell | Comprehensive game design |
| A Theory of Fun | Raph Koster | Fun psychology |
| Hooked | Nir Eyal | Behavioral design |
| Flow | Mihaly Csikszentmihalyi | Optimal experience |

#### Player Psychology
| Resource | Type | Application |
|----------|------|-------------|
| Bartle Taxonomy | Player types | Archetype design |
| Yee's Motivations | Psychological | Engagement |
| Behavioral Economics | Academic | Economic design |
| Habit Research | Psychology | Retention |

#### UI/UX Design
| Resource | Focus | Application |
|----------|-------|-------------|
| Information Architecture | Structure | UI organization |
| Tufte's Principles | Visualization | Data display |
| WCAG Guidelines | Accessibility | Inclusive design |
| Game UI Patterns | Practical | Interface design |

#### Comparable Games
| Game | Study Focus | Relevance |
|------|-------------|-----------|
| Factorio | Session design | Similar complexity |
| RimWorld | Emergent stories | AI integration |
| Stardew Valley | Activity loops | Relaxing gameplay |
| Dwarf Fortress | Complex systems | Depth management |
| Civilization | Progression arcs | Long-term engagement |

---

## Success Criteria

- [ ] Clear minute-to-minute activity flow
- [ ] Session goals defined for different player types
- [ ] Progression feel articulated across timeline
- [ ] Return triggers identified
- [ ] Critical UI/UX paths mapped
- [ ] Information architecture specified
- [ ] Player archetypes fully defined
- [ ] Gameplay design skills documented
- [ ] Research sources catalogued
- [ ] Skill creation workflow defined

---

**Status**: COMPLETE - Ready for Day 3 Planning & Development

---

## Changes & Revisions Log

### [Date] - Session 3 Revision

**Trigger**: [What caused this revision]

**Changes Made**:
- [Section]: [What changed]

**Rationale**: [Why this revision was necessary]

**Impact**: [What other documents/systems are affected]

---

## Cross-Doc Issues

### Issue 1: [Brief Description]
**Discovered in**: Session 3
**Affects**: Session Y, Session Z
**Description**: [What contradicts what]
**Resolution**: [How/when it will be resolved]
**Status**: [Open/In Progress/Resolved]

---

**Status**: Template Updated - Ready for Session 3 Planning (Depth-Optimized Methodology)
