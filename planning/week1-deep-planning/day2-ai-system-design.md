# Day 2: AI System Design - Deep Planning Document

**Planning Day**: 2 of 7  
**Status**: Draft  
**Last Updated**: Day 0 (Template Created)

---

## Purpose

Specify how AI agents think, decide, and behave to create believable citizens. This document defines the AI architecture, decision-making processes, memory systems, and experimental brain configurations that make AI agents feel authentic rather than robotic.

---

## Key Questions Addressed

1. What's the AI decision-making architecture?
2. How do agents form goals and prioritize actions?
3. How do agents learn, remember, and form relationships?
4. How do we handle AI voting and political behavior?
5. How does the AI population elasticity system work?
6. What makes AI behavior feel authentic rather than robotic?
7. How do players learn about AI lives? (Emergent narrative)
8. How do we debug AI decisions? (Debuggability)

---

## Dependencies

- **Requires**: Day 1 (Technical Architecture) - Performance budgets, tick loop
- **Informs**: Day 3 (Gameplay Loops), Day 5 (Governance), Day 6 (Prototyping)

---

## 1. AI Agent Architecture

### Core Decision Loop

```mermaid
graph TD
    A[Perception<br/>Sense World] --> B[Memory Update<br/>Store Observations]
    B --> C[Goal Evaluation<br/>Assess Priorities]
    C --> D[Planning<br/>Generate Actions]
    D --> E[Action Selection<br/>Choose Best Action]
    E --> F[Execution<br/>Perform Action]
    F --> G[Learning<br/>Update Beliefs]
    G --> A
    
    style C fill:#bbf,stroke:#333,stroke-width:2px
    style E fill:#bfb,stroke:#333,stroke-width:2px
```

### Agent State Structure

```mermaid
classDiagram
    class Agent {
        +UUID id
        +String name
        +AgentProfile profile
        +AgentMemory memory
        +GoalSystem goals
        +BehaviorTree behavior
        +EconomicState economy
        +SocialState social
        +Vector3 position
        +tick()
    }
    
    class AgentProfile {
        +PersonalityTraits traits
        +SkillSet skills
        +ValueSystem values
        +Preferences preferences
    }
    
    class AgentMemory {
        +List~Memory~ shortTerm
        +List~Memory~ longTerm
        +Map~Agent, Relationship~ relationships
        +WorldModel worldModel
        +addMemory(event)
        +retrieveRelevant(context)
    }
    
    class GoalSystem {
        +List~Goal~ activeGoals
        +GoalHierarchy hierarchy
        +calculatePriorities()
        +selectCurrentGoal()
    }
    
    Agent --> AgentProfile
    Agent --> AgentMemory
    Agent --> GoalSystem
```

### Tick Processing

```mermaid
sequenceDiagram
    participant Tick as Server Tick
    participant Agent as Agent
    participant Perception as Perception
    participant Memory as Memory
    participant Goals as Goal System
    participant Planner as Planner
    participant Action as Action
    
    Tick->>Agent: Process()
    
    Agent->>Perception: Sense environment
    Perception-->>Agent: Observations
    
    Agent->>Memory: Update(observations)
    Memory->>Memory: Consolidate memories
    Memory-->>Agent: Context
    
    Agent->>Goals: Reevaluate priorities
    Goals->>Goals: Check goal completion
    Goals->>Goals: Calculate utilities
    Goals-->>Agent: Current goal
    
    Agent->>Planner: Plan(goal, context)
    Planner->>Planner: Generate actions
    Planner->>Planner: Score actions
    Planner-->>Agent: Action sequence
    
    Agent->>Action: Execute(action)
    Action-->>Agent: Result
    
    Agent->>Memory: Learn from result
```

---

## 2. Goal System Architecture

### Goal Hierarchy

```mermaid
graph TD
    subgraph "Goal Hierarchy"
        S[Survival<br/>Physiological] --> SR[Subsistence Resources]
        S --> SH[Shelter & Safety]
        
        SR --> F[Food Security]
        SR --> W[Water Access]
        SR --> I[Income Stability]
        
        SH --> ST[Shelter Quality]
        SH --> SF[Personal Safety]
        
        P[Prosperity<br/>Economic] --> WE[Wealth Accumulation]
        P --> SP[Skill Progression]
        P --> BU[Business Growth]
        
        SO[Social<br/>Belonging] --> RE[Relationships]
        SO --> ST2[Status & Reputation]
        SO --> CO[Community Participation]
        
        SE[Self-Actualization<br/>Fulfillment] --> CR[Creative Expression]
        SE --> PO[Political Influence]
        SE --> LE[Legacy Building]
    end
    
    style S fill:#f99,stroke:#333,stroke-width:2px
    style P fill:#ff9,stroke:#333,stroke-width:2px
    style SO fill:#9f9,stroke:#333,stroke-width:2px
    style SE fill:#99f,stroke:#333,stroke-width:2px
```

### Goal Priority Calculation

```mermaid
graph LR
    A[Base Priority] --> C[Final Priority]
    B[Urgency Modifier] --> C
    D[Personality Weight] --> C
    E[Current State] --> C
    F[Memory Influence] --> C
    
    style C fill:#bbf,stroke:#333,stroke-width:2px
```

**Factors in Priority**:
1. **Base Priority**: Maslow hierarchy weight
2. **Urgency**: Time pressure (starving > comfortable)
3. **Personality**: Individual goal preferences
4. **Current State**: What's already satisfied
5. **Memory**: Past experiences ("Last time I ignored hunger...")

---

## 3. Agent Memory System

### Memory Architecture

```mermaid
graph TB
    subgraph "Memory System"
        STM[Short-Term Memory<br/>Recent 24 hours]
        LTM[Long-Term Memory<br/>Persistent]
        WM[Working Memory<br/>Current Context]
    end
    
    subgraph "Memory Types"
        EP[Episodic<br/>Events]
        SEM[Semantic<br/>Facts]
        PR[Procedural<br/>Skills]
        SO[Social<br/>Relationships]
    end
    
    STM -->|Consolidation| LTM
    WM -->|Retrieval| STM
    WM -->|Retrieval| LTM
    
    LTM --> EP
    LTM --> SEM
    LTM --> PR
    LTM --> SO
```

### Memory Data Structure

```mermaid
classDiagram
    class Memory {
        +UUID id
        +DateTime timestamp
        +MemoryType type
        +float importance
        +float emotionalValence
        +String description
        +Map~String, Any~ data
        +List~String~ tags
        +decay(dt)
    }
    
    class EpisodicMemory {
        +Event event
        +Vector3 location
        +List~Agent~ participants
    }
    
    class RelationshipMemory {
        +Agent other
        +RelationshipType type
        +float trust
        +float respect
        +List~Interaction~ history
    }
    
    Memory <|-- EpisodicMemory
    Memory <|-- RelationshipMemory
```

### What Agents Remember

**Short-Term (24 hours)**:
- Recent conversations
- Current transactions
- Immediate threats/opportunities
- Active plans

**Long-Term (Persistent)**:
- Major life events
- Traumatic experiences
- Successful strategies
- Relationship histories
- World facts (prices, locations, laws)

**Decay Mechanics**:
- Unimportant memories fade
- Emotional memories persist longer
- Accessed memories strengthen
- Contradicting memories update beliefs

---

## 4. Economic Behavior Model

### Price Belief Formation

```mermaid
graph TD
    A[Observe Market] --> B[Update Belief]
    C[Memory of Past Prices] --> B
    D[Personality<br/>Optimist/Pessimist] --> B
    
    B --> E[Price Belief Range]
    E --> F[Buy if price < belief]
    E --> G[Sell if price > belief]
    
    style E fill:#bbf,stroke:#333,stroke-width:2px
```

**Price Belief Formula**:
```
Belief = (ObservedPrices * WeightRecent) + (MemoryPrices * WeightPast) + PersonalityBias
Range = MinPrice to MaxPrice (with uncertainty)
```

### Trading Strategy

```mermaid
graph LR
    A[Assess Needs] --> B[Check Inventory]
    B --> C{Can produce?}
    C -->|Yes| D[Gather Resources]
    C -->|No| E[Buy from Market]
    D --> F[Produce Goods]
    E --> G[Find Best Price]
    F --> H[Sell Surplus]
    G --> H
    
    style H fill:#bfb,stroke:#333,stroke-width:2px
```

### Career Specialization Decision

```mermaid
graph TD
    A[Evaluate Skills] --> B[Check Market Demand]
    C[Assess Personal Interest] --> D[Calculate Expected Value]
    B --> D
    D --> E{Better than current?}
    E -->|Yes| F[Consider Switching]
    E -->|No| G[Continue Current]
    F --> H[Switch Cost Analysis]
    H --> I{Worth it?}
    I -->|Yes| J[Switch Career]
    I -->|No| G
```

---

## 5. Political Behavior Model

### Voting Decision Process

```mermaid
graph TD
    A[Election Announced] --> B[Evaluate Candidates/Laws]
    B --> C[Personal Impact Analysis]
    D[Values Alignment] --> E[Preference Formation]
    C --> E
    F[Social Influence] --> E
    G[Past Performance] --> E
    E --> H[Vote Decision]
    
    style E fill:#bbf,stroke:#333,stroke-width:2px
```

**Voting Factors**:
1. **Personal Impact**: How does this affect my wealth/survival?
2. **Values Alignment**: Does this match my ideology?
3. **Social Influence**: What do trusted friends think?
4. **Past Performance**: Track record of candidates
5. **Information Quality**: How much do I know?

### Faction Formation

```mermaid
graph TB
    subgraph "Faction System"
        A[Similar Values] --> B[Communication]
        C[Shared Interests] --> B
        D[Social Connections] --> B
        
        B --> E[Faction Emergence]
        E --> F[Collective Action]
        
        F --> G[Voting Bloc]
        F --> H[Shared Agenda]
        F --> I[Mutual Support]
    end
```

---

## 6. Social Behavior Model

### Relationship Formation

```mermaid
graph LR
    A[Proximity] --> B[Interaction]
    C[Trade History] --> B
    D[Shared Goals] --> B
    
    B --> E[Trust Building]
    E --> F[Relationship Types]
    
    F --> G[Friend]
    F --> H[Business Partner]
    F --> I[Political Ally]
    F --> J[Rival]
```

### Migration Decision

```mermaid
graph TD
    A[Assess Current Location] --> B{Dissatisfied?}
    B -->|Yes| C[Gather Information]
    B -->|No| D[Stay]
    
    C --> E[Evaluate Options]
    E --> F{Better opportunity?}
    F -->|Yes| G[Calculate Migration Cost]
    F -->|No| D
    
    G --> H{Worth the cost?}
    H -->|Yes| I[Move]
    H -->|No| D
```

---

## 7. Population Elasticity System

### Elasticity Architecture

```mermaid
graph TB
    subgraph "Population Manager"
        A[Monitor Metrics] --> B[Analyze Gaps]
        
        B --> C[Economic Velocity]
        B --> D[Labor Coverage]
        B --> E[Geographic Balance]
        B --> F[Player Activity]
        
        C --> G[Decision Engine]
        D --> G
        E --> G
        F --> G
        
        G --> H{Add Agents?}
        G --> I{Remove Agents?}
        
        H -->|Yes| J[Spawn Agent]
        I -->|Yes| K[Despawn Agent]
    end
```

### Elasticity Triggers

| Metric | Low (Add Agents) | High (Reduce Agents) |
|--------|-----------------|---------------------|
| Economic Velocity | < 50% baseline | > 150% baseline |
| Labor Gaps | Critical roles empty | Human coverage good |
| Player Activity | Very low | High engagement |
| Geographic Balance | Abandoned regions | Well-distributed |

### Agent Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Spawning: Population Manager
    Spawning --> Active: Initialize
    Active --> Dormant: Player inactivity
    Dormant --> Active: Wake up
    Active --> Departing: Migration trigger
    Departing --> [*]: Leave world
    Active --> [*]: Retirement/death
```

---

## 8. Personality & Diversity System

### Personality Model

```mermaid
graph TD
    subgraph "Personality Dimensions"
        A[Openness<br/>Curious vs Cautious]
        B[Conscientiousness<br/>Organized vs Spontaneous]
        C[Extraversion<br/>Social vs Solitary]
        D[Agreeableness<br/>Cooperative vs Competitive]
        E[Neuroticism<br/>Anxious vs Stable]
    end
    
    subgraph "Behavior Impact"
        A --> F[Exploration behavior]
        B --> G[Work ethic]
        C --> H[Social participation]
        D --> I[Trading style]
        E --> J[Risk tolerance]
    end
```

### Value Diversity

```mermaid
graph LR
    subgraph "Political Values"
        A[Environmentalism<br/>Nature preservation]
        B[Industrialism<br/>Economic growth]
        C[Individualism<br/>Personal freedom]
        D[Collectivism<br/>Community welfare]
        E[Tradition<br/>Stability]
        F[Progress<br/>Innovation]
    end
```

---

## 9. Emergent Narrative System

### How Players Learn About AI Lives

```mermaid
graph TB
    subgraph "Narrative Sources"
        A[Direct Observation] --> N[Player Knowledge]
        B[Conversations] --> N
        C[Market Activity] --> N
        D[Gossip/Chatter] --> N
        E[Public Records] --> N
    end
    
    subgraph "Information Types"
        N --> F[Agent Biography]
        N --> G[Life Events]
        N --> H[Current Goals]
        N --> I[Relationships]
        N --> J[Success/Failure Stories]
    end
```

### Narrative Mechanics

**Direct Observation**:
- See agents working, building, trading
- Visual indicators of agent state (busy, idle, traveling)
- Overhead icons for notable activities

**Information UI**:
- "Agent Directory" - browse known agents
- "Life Stories" - notable agent biographies
- "Relationship Map" - social network visualization
- "Event Log" - significant agent actions

**Gossip System**:
- Agents share information with players
- News travels through social network
- Reputation based on information accuracy

**Public Records**:
- Census data, economic participation
- Political voting history (if public)
- Criminal records (if exists)
- Achievement/business registry

---

## 10. AI Debuggability Architecture

### Decision Tracing System

```mermaid
graph TB
    subgraph "Debug Interface"
        A[Select Agent] --> B[Decision Tree Viewer]
        A --> C[Memory Inspector]
        A --> D[Goal Monitor]
        A --> E[Action History]
    end
    
    subgraph "Trace Data"
        B --> F[Why did they choose X?]
        C --> G[What do they know?]
        D --> H[What are they trying to achieve?]
        E --> I[What have they done?]
    end
```

### Debug Features

**Decision Tree Viewer**:
- Visualize agent's current decision tree
- See scores for alternative actions
- Understand why an action was chosen

**Memory Inspector**:
- Browse agent's memory (short & long term)
- See memory importance scores
- View memory influence on current decisions

**Goal Monitor**:
- Current goal hierarchy
- Priority calculations
- Goal completion progress

**Action History**:
- Timeline of recent actions
- Success/failure outcomes
- Resource changes

**Simulation Replay**:
- Step through agent's past decisions
- See what they perceived
- Understand why they acted

### Debug UI Mockup

```
┌─────────────────────────────────────┐
│ Agent: Sarah the Farmer             │
├─────────────────────────────────────┤
│ [Overview] [Goals] [Memory] [Debug] │
├─────────────────────────────────────┤
│ Current Goal: Find Food (Urgency:   │
│ 8.5/10)                             │
│                                     │
│ Decision Trace:                     │
│ ├─ Goal: Find Food                  │
│ ├─ Options Considered:              │
│ │  ├─ Buy Bread (Score: 7.2)        │
│ │  ├─ Harvest Crops (Score: 6.8)    │
│ │  └─ Hunt (Score: 4.1)             │
│ └─ Selected: Buy Bread              │
│    ├─ Reason: Closest vendor        │
│    ├─ Price: Affordable             │
│    └─ Memory: Good past experience  │
└─────────────────────────────────────┘
```

---

## 11. Experimental Brain Configurations

### Configuration Variants

| Config | Rationality | Social Complexity | Goal Diversity | Information |
|--------|-------------|-------------------|----------------|-------------|
| **Realistic** | Bounded | High | High | Imperfect |
| **Optimal** | High | Low | Low | Perfect |
| **Chaotic** | Low | Medium | High | Imperfect |
| **Cooperative** | Medium | High | Low | Shared |

### Testing Metrics

- **Economic Efficiency**: Market clearing speed
- **Social Stability**: Conflict frequency
- **Political Engagement**: Voting participation
- **Player Satisfaction**: Survey results
- **Emergent Behavior**: Interesting events/minute

---

## 12. Open Questions & Future Research

### Unresolved Questions

- [ ] What's the computational cost of different brain configurations?
- [ ] How many memories can an agent have before performance degrades?
- [ ] What's the optimal tick budget per agent?
- [ ] How do we prevent "AI hive mind" behavior?
- [ ] What's the right balance of agent autonomy vs. story coherence?

### Research Needs

- [ ] Utility AI vs. GOAP vs. Behavior Trees for economic agents
- [ ] Memory consolidation algorithms
- [ ] Social simulation in games (academic research)
- [ ] Emergent narrative generation techniques
- [ ] AI debugging best practices

---

## 13. Decisions Log

| Date | Decision | Rationale |
|------|----------|-----------|
| Day 0 | Utility-based goals | Flexible, handles competing priorities |
| Day 0 | Episodic memory model | Creates believable, context-aware behavior |
| Day 0 | Price belief system | Realistic economic behavior, emergent dynamics |
| Day 0 | Faction formation | Emergent politics, no hardcoded parties |
| Day 0 | Multiple brain configs | Test what creates best player experience |

---

## Success Criteria

- [ ] Clear AI decision-making architecture
- [ ] Economic behavior model specified
- [ ] Political behavior model specified
- [ ] Population elasticity algorithm defined
- [ ] Personality/diversity system designed
- [ ] Experimental configurations outlined
- [ ] Emergent narrative system designed
- [ ] Debuggability architecture specified

---

**Status**: TEMPLATE - Ready for Day 2 Planning
