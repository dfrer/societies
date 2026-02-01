# Reference Materials - Decisions, Questions & Skills

**Part of**: Session 2 - AI System Design  
**File**: 06-ai-skills-reference.md  
**Status**: Complete

---

> **Navigation**: [Index]([AGENTS-READ-FIRST]-index.md) | [Prev: Narrative & Debugging](05-narrative-debugging.md)
> 
> **Part of**: [Session 2 AI System Design]([AGENTS-READ-FIRST]-index.md)
> **Requires**: [Session 1 Architecture](../session-1-technical-architecture/)
> **Informs**: [Future Sessions] (Session 3-7 planning not yet started)

---

## Open Questions

### Research & Development Areas

The following questions represent technical challenges, implementation decisions, and future research directions for the AI system:

## 12. Open Questions & Future Research

### Performance & Scale Questions

1. **What is the exact performance cost of 15-20 considerations vs. 5-8?**
   - Current budget assumes 15 considerations at ~0.02ms each
   - Need empirical testing: does 20 considerations exceed 2ms budget at 20 agents (MVP)?
   - May require dynamic LOD: fewer considerations for distant agents

2. **How many agents can we support at 2ms budget with full AI?**
   - MVP: 20 agents × 2ms = 40ms per tick (fits comfortably within 50ms tick window - no amortization needed)
   - Post-MVP scale: 50-100 agents requires amortization via 5 buckets to bring processing to 20-40ms per tick
   - What's the practical limit before we need aggressive LOD or reduced tick rates?

3. **What's the memory bandwidth impact of 100 agents with 10 memories each (post-MVP)?**
   - 10 slots × 64 bytes = 640 bytes per agent
   - 100 agents = 64KB memory footprint (post-MVP - reasonable)
   - But consolidation/decay scans all agents every 10 ticks - cache thrashing risk at scale?

### Behavior Quality Questions

4. **What's the optimal balance of agent autonomy vs. narrative coherence?**
   - Too much autonomy: agents pursue boring goals, ignore dramatic opportunities
   - Too little autonomy: agents feel scripted, repetitive
   - Need metrics: "interesting events per hour" vs. "agent satisfaction"

5. **How do we prevent "AI hive mind" behavior where all agents act identically?**
   - Weighted random selection helps, but is it sufficient?
   - Do we need explicit diversity injection (ensure at least 3 different choices in any situation)?
   - Should personality traits have non-linear interactions (squared terms, cross-products)?

6. **What's the minimum memory consolidation rate to maintain believable agent histories?**
   - Current: every 10 ticks (0.5s at 20 TPS)
   - Too frequent: performance cost, agents remember trivial events
   - Too rare: important events get overwritten before consolidation
   - Need "goldilocks" zone through prototype testing

### Economic & Social Questions

7. **How many price beliefs can agents maintain before market learning degrades?**
   - Current: 32 beliefs (256 bytes)
   - Is 32 enough for a complex economy (50+ item types)?
   - Belief decay: when should old beliefs be purged to make room for new?

8. **What's the critical threshold for faction formation vs. solitary behavior?**
   - Current logic: 3+ similar agents + communication network density > 0.6
   - Does this create too many factions (fragmentation) or too few (consolidation)?
   - Need testing with 20 agents over 7-day simulation (MVP validation)

9. **How do we detect and handle "behavioral dead ends" (agents stuck in loops)?**
   - Current: goal satisfaction decay should eventually force new goals
   - But what if an agent has contradictory goals (high Safety + high Adventure)?
   - Need deadlock detection and intervention mechanisms

### Technical Implementation Questions

10. **What's the optimal serialization format for agent state snapshots?**
     - Options: JSON (readable), MessagePack (fast), custom binary (smallest)
     - Must balance: human debuggability, network transmission, database storage
     - Need profiling with 100-agent world save/load (post-MVP scale)

11. **How do we calibrate population elasticity thresholds to prevent oscillation?**
    - Current: 4 metrics trigger spawn/despawn with fixed thresholds
    - Risk: Over-correction causing boom/bust population cycles
    - Need: Hysteresis bands, smoothing algorithms, minimum stable periods
    - Testing: 7-day simulation with varying economic conditions to validate stability

12. **What's the minimum gossip fidelity required for narrative emergence?**
    - Current: 5% accuracy loss per transmission hop
    - Risk: Information degrades to noise before reaching players
    - Need: Threshold where gossip creates meaningful (not random) narratives
    - Testing: Measure story coherence at various degradation rates (2%, 5%, 10%, 20% loss)

### Future Research Areas

**Immediate (Day 3-5):**
- Prototype tick budget validation with 20 agents (MVP), 50 agents (Alpha), 100 agents (post-MVP stress test)
- Test consideration count impact on decision quality vs. performance
- Validate memory consolidation timing through simulation

**Short-term (Week 2-3):**
- Economic testing: market clearing with various agent counts
- Social testing: relationship formation rates under different proximity rules
- Political testing: faction emergence with different value distributions

**Medium-term (Month 2):**
- Player perception studies: what makes AI behavior feel "believable"?
- Comparative analysis: Utility AI vs. Monte Carlo Tree Search for agents
- Long-term simulation: 30-day agent lifecycle stability testing

**Ongoing Research:**
- Academic literature on artificial societies (Sugarscape, etc.)
- Game AI community patterns for large-scale agent systems
- Psychology research on realistic decision-making errors and biases

---

## 13. Decisions Log

### Decision 1: Utility AI + Behavior Trees Architecture

**Date**: Day 2 - Core Architecture Decision

**Decision**: Use Utility AI for goal/action selection combined with Behavior Trees for action execution

**Rationale**:
- **Scalability**: Utility AI evaluates O(n) considerations vs. GOAP's O(n^m) planning (critical for 20+ agents, 50-100 post-MVP)
- **Proven Pattern**: Successfully used in RimWorld (100+ pawns), The Sims (agent needs), Civilization (AI personalities)
- **Flexibility**: Multiplicative scoring naturally handles competing priorities without explicit priority trees
- **Debuggability**: Clear consideration values make "why did agent do X?" answerable

**Alternatives Considered**:
- **GOAP (Goal-Oriented Action Planning)**: Rejected due to exponential complexity and difficulty debugging plan failures
- **Finite State Machines**: Rejected for being too rigid - couldn't handle goal interruptions and emergent combinations
- **Monte Carlo Tree Search**: Rejected as overkill - need fast decisions, not optimal play
- **HTN (Hierarchical Task Networks)**: Rejected for requiring too much hand-authored domain knowledge

**Confidence**: High (90%)

**Reversible**: Yes, but expensive - would require rewriting entire decision system (~2-3 weeks)

**Impact**: Defines entire AI architecture; affects Session 1 (tick budgets), Session 3 (gameplay interactions), Session 5 (governance actions)

---

### Decision 2: 3-Tier Memory System (5+5 Slots)

**Date**: Day 2 - Memory System Design

**Decision**: Implement 3-tier memory: 5 Short-Term slots (12-24 hours), 5 Long-Term slots (weeks-months), unlimited Core memories (permanent)

**Rationale**:
- **Performance**: 10 slots × 64 bytes = 640 bytes per agent (fits in L1 cache, ~8KB total per agent)
- **Emergent Depth**: Slot competition creates natural "forgettable" moments without explicit deletion
- **Simplified from DF**: Dwarf Fortress uses 8+8 slots; we reduced to 5+5 for performance while keeping core mechanics
- **Personality Impact**: Memory consolidation (STM→LTM→Core) tied to emotional valence creates meaningful character growth

**Alternatives Considered**:
- **Unlimited memory**: Rejected - unbounded growth, agents would remember every meal forever
- **Single tier**: Rejected - no sense of "recent" vs. "distant" past, all memories compete equally
- **7+7 slots (closer to DF)**: Rejected - 14 slots = 896 bytes just for memory, pushes agent over 8KB budget
- **Time-based decay only**: Rejected - doesn't create competition dynamics; agents just "fade"

**Confidence**: Medium-High (75%)

**Reversible**: Yes - can adjust slot counts via configuration (though affects savegame format)

**Impact**: Core agent behavior; affects emergent narrative quality, relationship formation, decision-making context

---

### Decision 3: Price Belief System with Uncertainty Ranges

**Date**: Day 2 - Economic Behavior Model

**Decision**: Agents maintain price beliefs as (mean, uncertainty) pairs with min/max bounds, updated via weighted averaging with personality bias

**Rationale**:
- **Realistic Trading**: Creates natural bid-ask spreads (agents won't buy above max, sell below min)
- **Emergent Price Discovery**: Different beliefs create market opportunities and arbitrage
- **Personality Expression**: Greedy agents widen spreads (buy low, sell high), generous agents narrow them
- **Learning Model**: Weighted update formula (memoryWeight × old + obsWeight × new) matches behavioral economics research

**Alternatives Considered**:
- **Single price point**: Rejected - creates instant agreement, no negotiation dynamics
- **Perfect market knowledge**: Rejected - all agents would have identical prices, no trading opportunities
- **Fixed beliefs**: Rejected - agents wouldn't adapt to market changes
- **Bayesian updating**: Rejected - too computationally expensive for 1000+ agents with 32 beliefs each

**Confidence**: High (85%)

**Reversible**: Yes - alternative economic models could be swapped in (though affects all trading code)

**Impact**: Economic system core; affects Session 3 (trading gameplay), Session 4 (market UI), all agent economic decisions

---

### Decision 4: 20 TPS Tick Rate with Amortization

**Date**: Day 2 - Performance Architecture

**Decision**: Run AI at 20 TPS (50ms ticks) with 5 buckets available for post-MVP scaling. At MVP (20 agents), all agents process every tick (~40ms). At 50-100 agent scale, use 5 buckets for amortization.

**Rationale**:
- **Budget Math**: 20 agents × 2ms = 40ms per tick, leaving 10ms for physics/networking/ecosystem
- **Player Perception**: 20 TPS provides smooth visual updates; 10 TPS feels sluggish
- **Amortization**: Not all agents need full processing every tick; bucket approach allows LOD (Level of Detail)
- **Session 1 Alignment**: Matches server tick architecture defined in technical planning

**Alternatives Considered**:
- **30 TPS (33ms)**: Rejected - unnecessary complexity, 20 TPS sufficient for MVP
- **10 TPS (100ms)**: Rejected - too slow, agents would appear unresponsive to players
- **Variable tick rate**: Rejected - adds complexity, determinism issues, hard to debug
- **Process all agents every tick**: Accepted for MVP (20 agents × 2ms = 40ms < 50ms tick window). Post-MVP (50-100 agents) requires bucketing.

**Confidence**: High (90%)

**Reversible**: No - tick rate is fundamental to all timing calculations; changing would require rebalancing all decay rates, action durations, etc.

**Impact**: Core performance architecture; affects Session 1 (server design), all time-based agent systems

---

### Decision 5: 15-20 Personality Facets (Core 5 + Big 5 + Secondary 9)

**Date**: Day 2 - Personality System Design

**Decision**: Implement 19 personality facets: Core 5 (Gregariousness, Work Ethic, Violence, Greed, Emotional Stability) + Big Five (OCEAN) + Secondary 9 (Bravery, Altruism, etc.)

**Rationale**:
- **Expressive Range**: 19 facets create 10,000+ unique personality profiles (vs. 5 facets = 100s of combinations)
- **Psychological Grounding**: Big Five is well-validated in psychology research; extends naturally
- **Game-Relevant**: Core 5 directly map to gameplay (trading, crafting, combat, social)
- **Memory Efficient**: Stored as bytes (0-100 scale), total 19 bytes per agent (negligible cost)

**Alternatives Considered**:
- **Big Five only**: Rejected - too abstract, doesn't directly map to game behaviors
- **Core 5 only**: Rejected - insufficient depth, agents feel too similar
- **30+ facets**: Rejected - diminishing returns, computational cost of trait interactions
- **Binary traits**: Rejected - too coarse (e.g., "brave/cowardly" misses nuanced behavior)

**Confidence**: Medium (70%)

**Reversible**: Yes - can add/remove traits via configuration, though affects agent generation and all trait-impact calculations

**Impact**: Agent diversity; affects goal weights, economic decisions, social preferences, political values

---

### Decision 6: Weighted Random Goal Selection

**Date**: Day 2 - Goal System Architecture

**Decision**: Select goals via weighted random choice from top 3 candidates (vs. always picking highest utility)

**Rationale**:
- **Prevents Hive Mind**: Even with identical inputs, agents make different choices (essential for believability)
- **Personality Expression**: Same trait values still produce varied behavior due to randomness
- **Exploration**: Agents occasionally try suboptimal goals, leading to unexpected (but plausible) outcomes
- **Fallback Protection**: If top goal is blocked, 2nd/3rd alternatives are already scored and ready

**Alternatives Considered**:
- **Pure max utility**: Rejected - agents with same stats act identically (hive mind)
- **Fully random**: Rejected - ignores goal priorities completely, agents act nonsensically
- **Top 1 with personality jitter**: Rejected - jitter applied after selection doesn't help when utilities are close
- **Epsilon-greedy (ML-style)**: Rejected - too complex, requires learning rate tuning

**Confidence**: High (80%)

**Reversible**: Yes - can switch to deterministic selection via configuration flag

**Impact**: Core decision quality; affects perceived agent intelligence, diversity, replayability

---

### Decision 7: Market-Based Coordination (No Central Planner)

**Date**: Day 2 - Economic Coordination

**Decision**: Agents coordinate purely through market signals (prices, supply/demand) without central planning or global optimization

**Rationale**:
- **Emergent Complexity**: Market prices naturally coordinate agent behavior (no hand-authored scripts)
- **Scalability**: O(n) local decisions vs. O(n²) global optimization for 100 agents (post-MVP scale)
- **Realism**: Matches real-world economic coordination; players understand market dynamics
- **Robustness**: No single point of failure; if one agent fails, market continues

**Alternatives Considered**:
- **Central economic planner**: Rejected - requires solving NP-hard optimization, not real-time feasible
- **Faction-based command economy**: Rejected - too rigid, doesn't handle inter-faction trade
- **Auction-based**: Rejected - too synchronous, requires all agents to participate simultaneously
- **Contract-based**: Rejected - complex to implement, agents need to negotiate terms

**Confidence**: High (85%)

**Reversible**: Partially - could add planner for specific scenarios (e.g., emergency resource distribution)

**Impact**: Economic system architecture; affects all production, trading, and career decisions

---

### Decision 8: Population Elasticity with 4 Metrics

**Date**: Day 2 - Population Management

**Decision**: Spawn/despawn agents based on 4 metrics: Economic Velocity, Labor Gaps, Geographic Balance, Player Activity

**Rationale**:
- **Responsive**: Adjusts population to match world state (not arbitrary timer)
- **Player-Centric**: Reduces agents during low activity (performance), increases during high activity (engagement)
- **Economic Balance**: Fills labor gaps automatically, prevents dead economies
- **Natural Feel**: Agents "migrate" based on opportunity (believable world)

**Alternatives Considered**:
- **Fixed population**: Rejected - wastes resources when no players online, overcrowds when many players active
- **Pure player-count based**: Rejected - ignores economic needs; could have 20 agents but no food producers at MVP scale
- **Random spawn/despawn**: Rejected - feels arbitrary, breaks player immersion
- **Time-of-day based**: Rejected - doesn't account for actual player activity or economic state

**Confidence**: Medium (70%)

**Reversible**: Yes - can disable elasticity and use fixed population via configuration

**Impact**: World liveliness; affects performance, economic stability, player immersion

---

## Cross-Document Issues

This section documents contradictions, gaps, or integration issues identified between Session 2 (AI System Design) and other planning sessions.

### Issue 1: Perception Range vs. Network Synchronization
**Discovered in**: Session 2 Section 1.1 (Decision Loop), Session 1 Section 4.2 (Networking)
**Affects**: Session 1 (Networking), Session 2 (Perception System)
**Description**: 
- Session 2 defines perception range as 50m for agent sensing
- Session 1 network culling may not sync entities beyond 30m to clients
- Agents may "see" entities that clients cannot see, causing desync in debug visualization

**Resolution**: 
- Network sync range should be max(agent perception, 50m) or agent decisions will use invisible entities
- Add "perception padding" to network culling: sync entities within (player view + 20m buffer)
- Document in Session 1: AI perception requires extended sync range

**Status**: Open - Needs Session 1 revision

---

### Issue 2: Memory Consolidation Timing vs. Server Save/Load
**Discovered in**: Session 2 Section 3.2 (Memory Consolidation), Session 1 Section 5.3 (Persistence)
**Affects**: Session 1 (Save System), Session 2 (Memory System)
**Description**:
- Session 2 specifies memory consolidation runs every 10 ticks (0.5s)
- Session 1 specifies world save occurs every 5 minutes
- If server crashes between consolidations, agents lose unconsolidated STM memories (last 0.5s-24h)
- This may lose important recent events (e.g., "player helped agent")

**Resolution**:
- Add "critical memory" flag for STM slots - these persist even if unconsolidated
- OR: Force consolidation before save (performance hit, but ensures data integrity)
- Document in Session 2: STM has "recent critical" sub-buffer that persists

**Status**: In Progress - Prototype testing needed

---

### Issue 3: Economic Transaction Frequency vs. Database Write Load
**Discovered in**: Session 2 Section 4.1 (Trading Strategy), Session 1 Section 5.3 (PostgreSQL)
**Affects**: Session 1 (Database Design), Session 2 (Economic Behavior)
**Description**:
- Session 2 agents can trade 3-7 times per day (20 agents = 60-140 transactions/day at MVP)
- Session 1 targets 0.5-0.8ms per transaction write
- 700 transactions × 0.8ms = 560ms write time (acceptable for daily batch)
- BUT: If agents trade more frequently (e.g., 20x/day during market events), exceeds write budget

**Resolution**:
- Implement transaction batching: buffer 50 transactions before DB write
- Add transaction rate limiting: max 1 trade per agent per 5 minutes for non-critical trades
- Document in Session 2: Trading has cooldown to prevent DB overload

**Status**: Resolved - Added to Section 4.1

---

### Issue 4: Faction Formation vs. Session 5 Governance Scope
**Discovered in**: Session 2 Section 5.3 (Faction Formation), Session 5 (Governance)
**Affects**: Session 5 (Political System), Session 2 (Faction System)
**Description**:
- Session 2 allows dynamic faction formation (emergent, no hardcoded parties)
- Session 5 may assume predefined governance structures (council, democracy, etc.)
- Unclear how emergent factions map to formal governance roles
- Risk: Factions form but have no mechanism to gain political power

**Resolution**:
- Session 5 needs to define "faction → governance pathway" (e.g., petition for recognition, election thresholds)
- Session 2 needs to add "political legitimacy" metric to factions (based on size, cohesion, duration)
- Cross-reference: Faction legitimacy determines governance participation rights

**Status**: Open - Requires Session 5 planning

---

### Issue 5: Population Elasticity "Departing" State vs. Player-Owned Agents
**Discovered in**: Session 2 Section 7.3 (Agent Lifecycle), Session 3 (Player Systems)
**Affects**: Session 3 (Player Housing/Employment), Session 2 (Population Management)
**Description**:
- Session 2 defines "Departing" state where agents leave world due to dissatisfaction
- Session 3 (anticipated) may allow players to "own" or employ agents (servants, workers)
- Can player-owned agents depart? If so, player loses investment. If not, violates elasticity rules.

**Resolution**:
- Add "player attachment" flag to agents (hired, befriended, quest-related)
- Player-attached agents cannot be despawned, but can enter "inactive" state (minimal processing)
- If player-attached agent should depart due to extreme dissatisfaction, trigger quest/event instead
- Document in Session 2: Player attachment overrides despawn logic

**Status**: Open - Needs Session 3 definition of player-agent relationships

---

### Issue 6: Debug Console Commands vs. Security Model
**Discovered in**: Session 2 Section 10.4 (Debug Console), Session 1 Section 6 (Security)
**Affects**: Session 1 (Authorization), Session 2 (Debug Tools)
**Description**:
- Session 2 defines extensive debug console commands (force action, inject memory, set state)
- Session 1 likely has security model for admin vs. player permissions
- Debug commands could be abused if available to non-admin players (e.g., "set credits 99999")

**Resolution**:
- Document requirement: All Session 2 debug commands require "admin" or "developer" role
- Add command audit logging (who ran what, when, on which agent)
- Consider client-side debug UI only (no server commands) for regular players
- Cross-reference with Session 1: Role-based command authorization

**Status**: Open - Needs Session 1 security specification

---

### Issue 7: Brain Configuration Testing vs. QA Automation
**Discovered in**: Session 2 Section 11.4 (Testing Metrics), Session 6 (QA/Testing)
**Affects**: Session 6 (Test Automation), Session 2 (Experimental Brains)
**Description**:
- Session 2 defines 4 brain configurations (Realistic, Optimal, Chaotic, Cooperative)
- Each requires 7-90 days of simulation testing
- Session 6 needs to define how automated tests validate these long-running simulations
- Current Session 6 may focus on unit/integration tests, not emergent behavior validation

**Resolution**:
- Add to Session 6: "Simulation Test Harness" for multi-day agent behavior validation
- Define metrics that can be automatically checked (economic efficiency, conflict frequency, etc.)
- Use headless simulation runs (no rendering) for faster testing
- Document in Session 2: Brain configs validated via automated simulation harness

**Status**: Open - Needs Session 6 planning

---

### Summary of Cross-Session Dependencies

| Session 2 Component | Depends On | Priority |
|---------------------|-----------|----------|
| Perception System | Session 1 Network Sync | High |
| Memory Persistence | Session 1 Save/Load | High |
| Economic Transactions | Session 1 Database | Medium |
| Faction Governance | Session 5 Political System | Medium |
| Player-AI Relationships | Session 3 Gameplay | Medium |
| Debug Console | Session 1 Security | Low |
| Brain Config Testing | Session 6 QA | Low |

**Next Steps**:
1. Review Session 1 and update network sync, persistence, and security sections
2. Coordinate with Session 5 planning to ensure faction → governance mapping
3. Flag for Session 3: Define player-agent ownership/attachment mechanics
4. Document these dependencies in session dependency matrix (planning/README.md)

---

## 14. AI Implementation Skills & Knowledge Base

### Overview

This section documents the comprehensive AI development skills required for Societies' agent systems. These skills cover utility-based AI, memory systems, economic agents, political behavior, personality models, and emergent narrative generation.

### 14.1 Core AI Programming Skills

#### Skill 1: Utility-Based AI Systems

**Research Sources:**
- **Primary:** "Behavioral Mathematics for Game AI" by Dave Mark
- **Patterns:** "Game AI Pro" chapters on utility AI (free online)
- **Case Studies:** GDC talks on The Sims AI, Civilization AI
- **Academic:** Decision-theoretic planning papers (IEEE, AIIDE)

**Key Competencies:**
- Curve functions for utility scoring (linear, exponential, logistic)
- Normalization and weighting strategies
- Decision tree vs utility system tradeoffs
- Performance optimization for large agent counts (50-100 post-MVP)
- Goal selection algorithms with interruption
- Multi-criteria decision analysis

**Creation Process:**
1. Document goal hierarchy (Maslow-based: Survival, Prosperity, Social, Self-Actualization)
2. Create utility curves for each goal type
3. Implement priority calculation formula: `P = urgency × value × personalityAlignment`
4. Benchmark utility calculations at scale
5. Research The Sims' utility system implementation
6. Document goal interruption and resumption patterns

**Verification Steps:**
- [ ] Can design utility curves for different goal types
- [ ] Can implement priority calculations with weights
- [ ] Can handle goal interruption gracefully
- [ ] Performance: 20 agents < 0.5ms per tick (MVP), 50-100 agents < 1ms per tick (post-MVP)
- [ ] Creates believable, non-random decision patterns

---

#### Skill 2: Memory System Architecture

**Research Sources:**
- **Psychology:** Human memory research (episodic, semantic, procedural, working)
- **Reference:** "The MIT Encyclopedia of the Cognitive Sciences"
- **Games:** Dwarf Fortress memory systems, The Sims memory
- **AI:** Knowledge representation (semantic networks, ontologies)

**Key Competencies:**
- Multi-tier memory (working, short-term, long-term)
- Consolidation algorithms (importance + emotional salience + rehearsal)
- Decay mechanics (time-based forgetting curves)
- Retrieval relevance scoring (context matching)
- Memory categorization (episodic, semantic, procedural, social)
- Memory visualization for debugging

**Creation Process:**
1. Document 4 memory types with structures:
   - Episodic: events with timestamp, location, emotional valence
   - Semantic: facts about world (prices, locations, recipes)
   - Procedural: how-to knowledge (skills, crafting)
   - Social: relationships, trust, reputation
2. Create memory decay formulas:
   - Working: 30 seconds
   - Short-term: hours with decay
   - Long-term: permanent with accessibility decay
3. Implement consolidation algorithm
4. Create memory retrieval with context matching
5. Build memory inspection tools

**Verification Steps:**
- [ ] Can store and retrieve different memory types
- [ ] Memory decay works realistically over time
- [ ] Consolidation promotes important memories
- [ ] Retrieval returns contextually relevant memories
- [ ] System handles 20+ memories per agent efficiently (MVP)

---

#### Skill 3: Economic Agent Modeling

**Research Sources:**
- **Economics:** Behavioral economics (Kahneman, Thaler, Akerlof)
- **Games:** "Economics for Game Designers" GDC talks
- **Academic:** Agent-based modeling papers
- **Case Studies:** EVE Online economic postmortems

**Key Competencies:**
- Price belief formation (weighted averages with personality bias)
- Trading strategy algorithms (supply/demand response)
- Market analysis and opportunity detection
- Career specialization decisions
- Economic efficiency optimization
- Budget management and savings behavior

**Creation Process:**
1. Document price belief update algorithm:
   ```
   newBelief = (oldBelief × weight + observedPrice × (1-weight)) × personalityBias
   weight = 0.7 + (Openness × 0.2) - (Neuroticism × 0.1)
   ```
2. Create trading decision trees
3. Implement career specialization logic
4. Research economic agent models (Zero Intelligence, ZIP, GD)
5. Test with simulated market scenarios
6. Document market participation thresholds

**Verification Steps:**
- [ ] Can implement price belief updates
- [ ] Trading decisions respond to market conditions
- [ ] Career specialization feels realistic
- [ ] Agents show diverse economic behaviors
- [ ] Market dynamics emerge from agent interactions

---

#### Skill 4: Political Behavior Simulation

**Research Sources:**
- **Theory:** Voting theory (approval, ranked choice, Condorcet)
- **Psychology:** Political psychology (values voting, social influence)
- **Game Theory:** Faction formation and coalition building
- **Design:** Democratic theory, constitutional design principles

**Key Competencies:**
- Voting decision algorithms (impact × values × social influence × performance)
- Faction formation and coalition building
- Political campaign effectiveness
- Policy preference inference
- Vote counting methods (plurality, majority, ranked, approval)
- Political power dynamics

**Creation Process:**
1. Document voting calculation:
   ```
   VoteScore = personalImpact × 0.3 + valueAlignment × 0.3 + socialInfluence × 0.2 + pastPerformance × 0.2
   ```
2. Create faction dynamics simulation
3. Implement different voting methods
4. Research real-world voting behavior models
5. Test political scenarios (elections, legislation)
6. Document AI voting behavior customization

**Verification Steps:**
- [ ] Can implement multiple voting methods
- [ ] Voting decisions consider multiple factors
- [ ] Factions form based on shared interests
- [ ] Political behavior feels authentic
- [ ] Different personalities vote differently

---

#### Skill 5: Personality Systems (Big Five/OCEAN)

**Research Sources:**
- **Psychology:** Big Five personality literature (Costa & McCrae)
- **Reference:** "The Big Five Trait Taxonomy" research
- **Games:** Procedural character generation research
- **Psychology:** Value systems and moral psychology (Schwartz Theory)

**Key Competencies:**
- Five-factor model implementation (Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism)
- Trait-to-behavior mapping systems
- Value system integration (Schwartz values)
- Personality diversity generation
- Personality stability vs change over time
- Cultural value transmission

**Creation Process:**
1. Document trait definitions and ranges (0-100 scale):
   - Openness: creativity, curiosity, preference for variety
   - Conscientiousness: organization, diligence, goal-directed
   - Extraversion: sociability, energy, positive emotion
   - Agreeableness: cooperation, trust, altruism
   - Neuroticism: anxiety, stress, emotional instability
2. Create trait impact matrix (how each trait affects behaviors)
3. Implement personality generation algorithms
4. Document value systems (power, achievement, security, etc.)
5. Research procedural character generation
6. Validate personality diversity distribution

**Verification Steps:**
- [ ] Can generate diverse personalities
- [ ] Traits meaningfully impact decisions
- [ ] Personality distribution feels realistic
- [ ] Can explain why agent made specific choice
- [ ] Personality profiles are distinct and memorable

---

#### Skill 6: Emergent Narrative Systems

**Research Sources:**
- **Games:** Dwarf Fortress emergent storytelling postmortems
- **Academic:** Procedural narrative generation research
- **Sociology:** Information propagation models (gossip networks)
- **Analysis:** Social network analysis principles

**Key Competencies:**
- Gossip propagation mechanics (network spread, distortion)
- Event significance scoring (emotional impact × rarity × social relevance)
- Information decay and distortion over time
- Narrative reconstruction from records
- News/worthiness algorithms
- Story clustering and pattern detection

**Creation Process:**
1. Document gossip system architecture:
   - Spread probability based on Extraversion and relationship strength
   - Decay based on time and newsworthiness
   - Distortion based on number of hops
2. Create event significance algorithm
3. Implement information cascade models
4. Build narrative reconstruction from episodic memories
5. Create news generation from world events
6. Test narrative emergence in simulations

**Verification Steps:**
- [ ] Can spread information through social networks
- [ ] Significant events propagate further
- [ ] Information distorts naturally over time
- [ ] Can reconstruct stories from agent memories
- [ ] Players discover interesting emergent stories

---

### 14.2 AI Skill Development Workflow

#### Research Priority Schedule

**Immediate (Week 1-2):**
- Utility AI implementation patterns
- Memory system data structures
- Economic agent basic behaviors
- Tick processing optimization

**Short-term (Month 1-2):**
- Political behavior algorithms
- Personality trait systems
- Social relationship modeling
- Debug visualization tools

**Medium-term (Month 2-3):**
- Emergent narrative systems
- Population elasticity algorithms
- Faction and coalition dynamics
- Advanced economic strategies

**Ongoing:**
- Performance optimization
- New behavior types
- Debugging and profiling tools
- Validation and testing patterns

---

### 14.3 AI Skill Validation Process

**For Each AI Skill:**

1. **Prototype Implementation (1-2 weeks):**
   - Build minimal working version
   - Test with 20+ agents (MVP target)
   - Profile performance
   - Document behavior patterns

2. **Behavioral Validation:**
   - Does behavior match design specification?
   - Do agents act believably?
   - Is performance acceptable?
   - Can we debug their decisions?

3. **Scale Testing:**
   - Test with target agent counts (20 MVP, 50-100 post-MVP)
   - Measure tick processing time
   - Memory usage profiling
   - Network synchronization impact

4. **Documentation Update:**
   - Update skill with implementation details
   - Document deviations from original design
   - Add performance characteristics
   - Include code examples

5. **External Review:**
   - Share with AI programming community
   - Get feedback on approach
   - Compare with industry practices
   - Iterate based on feedback

---

### 14.4 Skills to Create Priority List

**Immediate (Prototype 1-2):**
1. Utility-Based AI Architecture
2. Agent Memory Systems
3. Tick-Based Agent Processing
4. AI Debugging and Visualization

**Short-term (Prototype 2-3):**
5. Economic Agent Behaviors
6. Price Belief Systems
7. Trading Strategy Algorithms
8. Population Elasticity System

**Medium-term (Prototype 3-4):**
9. Political Behavior Simulation
10. Voting System Implementation
11. Personality Systems (Big Five)
12. Social Relationship Modeling

**Ongoing:**
13. Emergent Narrative Systems
14. Faction and Coalition Dynamics
15. AI Performance Optimization
16. Agent Lifecycle Management

---

### 14.5 AI Research Resources

#### Primary Sources
| Resource | Focus | Application |
|----------|-------|-------------|
| "Behavioral Mathematics for Game AI" | Utility systems | Goal selection |
| Game AI Pro (free chapters) | Implementation patterns | All AI systems |
| GDC Vault - The Sims AI | Agent simulation | Memory, goals |
| IEEE Xplore - Multi-agent | Academic research | Advanced behaviors |
| AIIDE Proceedings | Game AI research | New techniques |

#### Psychology Sources
| Resource | Focus | Application |
|----------|-------|-------------|
| Big Five Research | Personality | OCEAN model |
| Behavioral Economics | Decision making | Economic agents |
| Social Psychology | Influence | Political behavior |
| Memory Research | Cognitive models | Agent memory |

#### Game References
| Game | AI System | Study Focus |
|------|-----------|-------------|
| The Sims | Utility-based goals | Goal selection |
| Dwarf Fortress | Emergent narrative | Story generation |
| Crusader Kings | Social simulation | Relationships |
| EVE Online | Economic agents | Market behavior |
| RimWorld | Story generation | Events and drama |

---

## Success Criteria

### Core Architecture (4/4 Complete)

- [x] **Clear AI decision-making architecture**
  - ✓ Utility AI + Behavior Trees architecture defined (Section 1)
  - ✓ 20 TPS tick processing with 5-bucket amortization (Section 1.3)
  - ✓ <2ms per-agent budget allocation across 7 phases (Section 1.1)
  - ✓ Goal hierarchy: Survival → Prosperity → Social → Self-Actualization (Section 2)
  - ✓ Weighted random goal selection prevents hive-mind (Section 2.2)

- [x] **Economic behavior model specified**
  - ✓ Price belief formation with (mean, uncertainty) pairs (Section 4.1)
  - ✓ Trading strategy decision trees (buy vs. produce vs. gather) (Section 4.2)
  - ✓ Career specialization and switching logic (Section 4.3)
  - ✓ Market-based coordination (no central planner) (Decisions Log #7)

- [x] **Political behavior model specified**
  - ✓ 6-factor voting decision formula (Section 5.1)
  - ✓ 6 political value axes (-100 to +100) (Section 5.2)
  - ✓ Dynamic faction formation and cohesion (Section 5.3)
  - ✓ Information/gossip propagation system (Section 5.4)
  - ✓ Multiple voting methods (plurality, approval, ranked, score) (Section 5.1)

- [x] **Population elasticity algorithm defined**
  - ✓ 4 metrics: Economic Velocity, Labor Gaps, Geographic Balance, Player Activity (Section 7)
  - ✓ Spawn triggers (low velocity + labor gaps, geographic imbalance) (Section 7.2)
  - ✓ Despawn triggers (overpopulation, very low activity) (Section 7.2)
  - ✓ 5-state lifecycle: Spawning → Active → Dormant → Departing → Dead (Section 7.3)
  - ✓ Migration decision algorithm (economic/social/environmental dissatisfaction) (Section 7.3)

### Agent Depth Systems (3/3 Complete)

- [x] **Personality/diversity system designed**
  - ✓ 19 personality facets: Core 5 + Big Five (OCEAN) + Secondary 9 (Section 8.1)
  - ✓ Trait impact matrix with non-linear response curves (Section 8.1)
  - ✓ 6 political value axes with trait correlations (Section 8.2)
  - ✓ 8 emergent archetypes (Entrepreneur, Diplomat, Warrior, etc.) (Section 8.1)
  - ✓ Archetype-based personality generation with cultural biases (Section 8.1)

- [x] **Experimental configurations outlined**
  - ✓ 4 brain configurations: Realistic, Optimal, Chaotic, Cooperative (Section 11)
  - ✓ Configuration comparison matrix with 7 dimensions (Section 11.5)
  - ✓ Testing metrics: Economic Efficiency, Social Stability, Political Engagement, Emergent Behavior (Section 11.5)
  - ✓ Automated testing protocol (7-90 day simulations) (Section 11.5)

- [x] **Emergent narrative system designed**
  - ✓ 6 information discovery channels (observation, conversation, gossip, records, etc.) (Section 9)
  - ✓ Gossip propagation with degradation and mutation (Section 9.3)
  - ✓ Narrative event detection (rags-to-riches, feuds, political drama) (Section 9.4)
  - ✓ Player knowledge levels (Unknown → Surface → Personal → Intimate) (Section 9.5)
  - ✓ UI mockups: Agent Directory, Life Stories Feed, Relationship Map (Section 9.6)

### Development Support (3/3 Complete)

- [x] **Debuggability architecture specified**
  - ✓ Decision tracing system with complete context (Section 10.1)
  - ✓ Memory inspection tools (STM/LTM/Core/Beliefs/Relationships) (Section 10.2)
  - ✓ Goal monitoring dashboard with real-time priorities (Section 10.3)
  - ✓ Performance profiler with system breakdown (Section 10.4)
  - ✓ Regression testing framework with 6 test scenarios (Section 10.5)
  - ✓ Debug console commands (17 commands documented) (Section 10.6)
  - ✓ Visualization tools (decision trees, heatmaps, relationship graphs) (Section 10.7)

- [x] **AI implementation skills documented**
  - ✓ 6 core AI skills: Utility AI, Memory Systems, Economic Agents, Political Behavior, Personality, Emergent Narrative (Section 14.1)
  - ✓ Skill creation process with verification steps for each (Section 14.1)
  - ✓ Research priority schedule (Immediate → Short-term → Medium-term → Ongoing) (Section 14.2)
  - ✓ Skill validation workflow (5-step process) (Section 14.3)
  - ✓ Priority list: 16 skills ranked by urgency (Section 14.4)

- [x] **Research sources catalogued**
  - ✓ Tier 1 sources: R4 (DF), R7 (AI Systems), R8 (PDF Synthesis), R1 (Technical) (Research Summary)
  - ✓ 7 key insights synthesized from research (Research Summary)
  - ✓ Primary sources table: Books, conferences, academic papers (Section 14.5)
  - ✓ Psychology sources: Big Five, behavioral economics, social psychology (Section 14.5)
  - ✓ Game references: The Sims, Dwarf Fortress, Crusader Kings, EVE Online, RimWorld (Section 14.5)

- [x] **Skill creation workflow defined**
  - ✓ 4-phase workflow: Research → Prototype → Validate → Document (implied across Section 14)
  - ✓ Research phase: Primary sources, key competencies, creation process (Section 14.1)
   - ✓ Prototype phase: 1-2 week implementation, 20+ agent testing (Section 14.3)
  - ✓ Validation phase: Behavioral validation, scale testing, external review (Section 14.3)
  - ✓ Documentation phase: Update skill with implementation details, performance characteristics (Section 14.3)

### Additional Deliverables (Beyond Original 11)

- [x] **8 detailed decisions documented**
  - ✓ Utility AI + Behavior Trees architecture (Decisions Log #1)
  - ✓ 3-tier memory system 5+5 slots (Decisions Log #2)
  - ✓ Price belief system with uncertainty (Decisions Log #3)
  - ✓ 20 TPS tick rate with amortization (Decisions Log #4)
  - ✓ 15-20 personality facets (Decisions Log #5)
  - ✓ Weighted random goal selection (Decisions Log #6)
  - ✓ Market-based coordination (Decisions Log #7)
  - ✓ Population elasticity with 4 metrics (Decisions Log #8)

- [x] **10 open questions identified**
  - ✓ Performance questions (consideration count, agent scale, memory bandwidth)
  - ✓ Behavior quality questions (autonomy vs. coherence, hive mind prevention, consolidation rate)
  - ✓ Economic/social questions (price belief capacity, faction thresholds, behavioral dead ends)
  - ✓ Technical questions (serialization format)
  - ✓ Future research areas prioritized (Section 12)

- [x] **7 cross-document issues documented**
  - ✓ Perception range vs. network sync (Issue 1)
  - ✓ Memory consolidation vs. save/load timing (Issue 2)
  - ✓ Transaction frequency vs. DB write load (Issue 3 - Resolved)
  - ✓ Faction formation vs. governance scope (Issue 4)
  - ✓ Departing state vs. player-owned agents (Issue 5)
  - ✓ Debug console vs. security model (Issue 6)
  - ✓ Brain config testing vs. QA automation (Issue 7)

---

**Completion Status**: 11/11 Primary Criteria + 3 Additional Deliverables = **COMPLETE**

**Document Statistics**:
- Total Lines: ~10,500+
- Sections: 14 major sections + subsections
- Decision Entries: 8 (with alternatives, confidence, reversibility)
- Open Questions: 10 specific technical questions
- Cross-Issues: 7 integration points identified
- Code Examples: 50+ pseudocode/C# examples
- Tables: 100+ specification tables
- Diagrams: 20+ Mermaid diagrams

---

**Status**: COMPLETE - Ready for Day 2 Planning & Development

---

## Changes & Revisions Log

### [Date] - Session 2 Revision

**Trigger**: [What caused this revision]

**Changes Made**:
- [Section]: [What changed]

**Rationale**: [Why this revision was necessary]

**Impact**: [What other documents/systems are affected]

---
