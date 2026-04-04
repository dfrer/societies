# Technical Postmortems & GDC Research Guide

This guide provides structured research tasks for gathering technical insights from game development postmortems and conference talks.

## Research Sources

### Primary Sources
1. **GDC Vault** (gdcvault.com) - Conference talks and postmortems
2. **Gamasutra/Game Developer** - Postmortem articles
3. **GDC YouTube Channel** - Free video content
5. **Game Developer Magazine Archives** - Historical articles

### Secondary Sources
- Developer blogs (personal websites)
- Podcasts (Game Design Round Table, etc.)
- Twitter/X threads from developers
- Discord communities

---

## Search Strategy

### Keywords to Search

**Technical Topics**:
- "Multiplayer simulation game postmortem"
- "Persistent world architecture"
- "AI agent system implementation"
- "Economy simulation game technical"
- "Godot multiplayer scaling"
- "C# game development postmortem"
- "Entity Component System implementation"
- "Database design game state"
- "Network synchronization techniques"
- "Procedural generation performance"

**Game-Specific**:
- "Eco game development postmortem"
- "Dwarf Fortress technical design"
- "Factorio multiplayer architecture"
- "RimWorld AI system"
- "Stardew Valley development story"
- "Minecraft server architecture"
- "Space Engineers multiplayer"

**Genre-Specific**:
- "Survival game technical challenges"
- "Colony simulation optimization"
- "City builder performance"
- "Sandbox game architecture"

### Search Process

1. **Start Broad**: Search general terms
2. **Narrow Down**: Find specific games/systems
3. **Follow References**: Check citations and links
4. **Cross-Reference**: Compare multiple sources
5. **Prioritize**: Focus on most relevant findings

---

## Target Postmortems

### High Priority

#### 1. Eco by Strange Loop Games
**What to Find**:
- Multiplayer architecture
- Environmental simulation
- Law system implementation
- Economic balancing
- Meteor event design

**Likely Sources**:
- GDC talks by John Krajewski
- Developer blog posts
- Reddit AMAs
- Postmortem articles

#### 2. Factorio by Wube Software
**What to Find**:
- Multiplayer synchronization
- Deterministic simulation
- Mod support architecture
- Performance optimization
- Save system design

**Likely Sources**:
- Friday Facts blog (factorio.com/blog)
- Technical blog posts
- Developer interviews
- GDC talks

#### 3. Dwarf Fortress by Bay 12 Games
**What to Find**:
- Agent simulation architecture
- World generation
- Memory management
- Save system complexity
- Development at scale (20+ years)

**Likely Sources**:
- Bay 12 Games dev logs
- Tarn Adams interviews
- GDC talks
- Academic papers about DF

#### 4. RimWorld by Ludeon Studios
**What to Find**:
- AI behavior design (Utility AI)
- Storytelling systems
- Mod architecture
- Performance with many agents
- Procedural generation

**Likely Sources**:
- Tynan Sylvester's blog
- GDC talks
- Reddit posts by Tynan
- Design philosophy articles

### Medium Priority

#### 5. Stardew Valley by ConcernedApe
**What to Find**:
- Solo development insights
- Save system design
- Content management
- Optimization strategies

#### 6. Space Engineers by Keen Software
**What to Find**:
- Voxel engine performance
- Multiplayer physics
- Large world management
- Memory optimization

#### 7. Minecraft by Mojang
**What to Find**:
- Multiplayer server architecture
- World persistence
- Modding ecosystem
- Scalability lessons

### General Technical Topics

#### 8. Godot Engine Case Studies
**What to Find**:
- Godot 4 multiplayer projects
- C# performance experiences
- Large project management
- Optimization techniques

#### 9. Database in Games
**What to Find**:
- PostgreSQL in games
- SQLite for single-player
- Redis caching
- Data persistence strategies

#### 10. AI in Games
**What to Find**:
- Utility AI implementations
- GOAP case studies
- Behavior tree scaling
- Multi-agent systems

---

## Agent Research Prompts

### Prompt 1: Eco Postmortem Research

```
# Research Task: Eco Technical Postmortem Analysis

## Objective
Find and analyze technical postmortems, talks, and articles about Eco's development, focusing on multiplayer architecture, simulation systems, and governance implementation.

## Background
Eco is a multiplayer environmental simulation with player-run governments and a persistent ecosystem. It shares many technical challenges with Societies.

## Research Questions

### Multiplayer Architecture
1. What networking solution did they use?
   - Client-server or P2P architecture
   - How do they handle 100+ concurrent players?
   - What synchronization challenges did they face?

2. How is the simulation made deterministic?
   - Tick rate and timing
   - Random number generation
   - Floating point consistency
   - How do they handle desyncs?

### Environmental Simulation
3. How does the ecosystem simulation work?
   - What species are modeled?
   - How is the food web implemented?
   - Performance optimization techniques
   - How is state stored and synchronized?

4. How does pollution spread?
   - Algorithm details
   - Performance considerations
   - Visualization techniques
   - Impact on gameplay

### Economic Systems
5. How is the economy simulated?
   - Currency system
   - Price discovery mechanism
   - Trade implementation
   - Performance with many transactions

6. How do skills work technically?
   - Progression system
   - Specialization implementation
   - Data storage
   - Network synchronization

### Governance Systems
7. How is the law system implemented?
   - Law data structure
   - Enforcement mechanism
   - Voting system
   - UI implementation

8. What challenges did they face with player governments?
   - Griefing prevention
   - Complexity management
   - Performance of law checking
   - Lessons learned

### Development Lessons
9. What technical regrets do they have?
   - What would they do differently?
   - What took longer than expected?
   - What was unexpectedly difficult?
   - What worked well?

10. What advice do they give for similar games?
    - Architecture recommendations
    - Technology choices
    - Team structure
    - Development timeline

## Deliverables

For each question:
1. Direct quotes or summaries from sources
2. Technical details (architectures, algorithms)
3. Lessons learned and warnings
4. Applicability to Societies

Format as:
- Executive summary (1 paragraph)
- Detailed findings by section
- Direct quotes with citations
- Recommendations for Societies
- List of all sources found

## Success Criteria
- [ ] Minimum 3 high-quality sources found
- [ ] Technical architecture details documented
- [ ] Lessons learned extracted
- [ ] Specific recommendations for Societies
- [ ] All sources properly cited
- [ ] Word count: 2000-3500 words

## Search Strategy

**Primary Sources**:
1. Search GDC Vault for "Eco" or "John Krajewski"
2. Check Gamasutra for "Eco postmortem"
3. Search YouTube: "Eco GDC" or "Strange Loop Games"
4. Check Eco's official blog/website
5. Search Reddit: r/EcoGlobalSurvival, r/gamedev

**Secondary Sources**:
- Developer interviews
- Podcast appearances
- Twitter/X threads
- Discord Q&As

## Time Budget
3-4 hours searching and analyzing

## Quality Standards
- Prefer primary sources (developers speaking)
- Verify technical claims when possible
- Note dates (older info may be outdated)
- Distinguish between fact and opinion
```

---

### Prompt 2: Multiplayer Simulation Research

```
# Research Task: Multiplayer Simulation Games Technical Analysis

## Objective
Find technical postmortems and talks about multiplayer simulation games (Factorio, Dwarf Fortress, RimWorld, etc.), focusing on architecture, optimization, and lessons learned.

## Research Questions

### Architecture
1. What architectures work for simulation games?
   - Client-server vs. P2P
   - Deterministic simulation vs. state sync
   - Tick-based vs. event-driven
   - Authoritative server patterns

2. How do you scale simulation games?
   - Entity count optimization
   - Spatial partitioning
   - LOD for simulation
   - Multi-threading strategies

### Networking
3. How is multiplayer synchronization handled?
   - Snapshot interpolation
   - Delta compression
   - Client-side prediction
   - Handling lag and packet loss

4. What networking libraries work well?
   - ENet, WebSocket, custom solutions
   - Godot multiplayer experiences
   - Custom UDP implementations

### Performance
5. How do you optimize simulation performance?
   - Profiling strategies
   - Hot path optimization
   - Memory management
   - Caching techniques

6. How many entities can you handle?
   - Real-world numbers from games
   - Bottlenecks identified
   - Optimization techniques
   - Hardware requirements

### Development Process
7. What takes longest in simulation game dev?
   - Common time sinks
   - Unexpected challenges
   - What to prioritize
   - What to cut

8. What are common multiplayer pitfalls?
   - Desynchronization issues
   - Security vulnerabilities
   - Performance degradation
   - Player experience problems

## Target Games

**Must Find**:
- Factorio (deterministic simulation expert)
- Dwarf Fortress (complex agent simulation)
- RimWorld (Utility AI at scale)
- Eco (environmental multiplayer)

**Nice to Have**:
- Space Engineers (voxel + physics)
- Stardew Valley (solo to multiplayer)
- Terraria (2D multiplayer)
- Starbound (procedural multiplayer)

## Deliverables

Comparative analysis including:
1. Architecture comparison table
2. Performance benchmarks from sources
3. Common patterns and anti-patterns
4. Technology recommendations
5. Pitfall warnings
6. Resource list with links

## Success Criteria
- [ ] Analysis of at least 4 games
- [ ] Architecture comparison documented
- [ ] Performance numbers found
- [ ] Common pitfalls identified
- [ ] Actionable recommendations
- [ ] Word count: 2500-4000 words

## Search Terms

"Factorio multiplayer architecture"
"Dwarf Fortress technical design"
"RimWorld AI implementation"
"Simulation game networking"
"Deterministic multiplayer game"
"Entity Component System performance"
"Game state synchronization"
"Godot multiplayer optimization"

## Time Budget
4-5 hours research and synthesis
```

---

### Prompt 3: AI Systems in Games Research

```
# Research Task: AI System Implementation Case Studies

## Objective
Find technical details about AI system implementations in games, particularly Utility AI, GOAP, and behavior trees used at scale.

## Research Questions

### Utility AI
1. What games use Utility AI?
   - Implementation details
   - Performance characteristics
   - Scalability limits
   - Pros and cons

2. How is Utility AI architected?
   - Scoring calculations
   - Consideration design
   - Performance optimization
   - Debugging tools

### GOAP (Goal-Oriented Action Planning)
3. What games use GOAP?
   - Implementation examples
   - Planning algorithm details
   - Performance with many agents
   - Common issues

4. When is GOAP appropriate?
   - Use cases
   - Complexity trade-offs
   - Alternatives to consider
   - Implementation difficulty

### Behavior Trees
5. How do behavior trees scale?
   - Performance with complex trees
   - Best practices
   - Common pitfalls
   - Debugging techniques

6. What are hybrid approaches?
   - Combining BT + Utility
   - BT + GOAP
   - Hierarchical designs
   - Layered behaviors

### Multi-Agent Systems
7. How do agents interact?
   - Communication patterns
   - Coordination mechanisms
   - Emergent behavior
   - Performance optimization

8. How do you debug AI at scale?
   - Visualization tools
   - Logging strategies
   - Replay systems
   - Live debugging

## Target Sources

**Games to Research**:
- RimWorld (Utility AI)
- F.E.A.R. (GOAP - classic example)
- The Sims (various AI systems)
- Dwarf Fortress (complex agents)
- Crusader Kings (AI decision-making)

**Technical Sources**:
- GDC AI talks
- AI Game Programming Wisdom books
- Academic papers on game AI
- Developer blogs

## Deliverables

Technical report with:
1. Comparison of AI architectures
2. Implementation recommendations
3. Performance considerations
4. Debugging strategies
5. Source code examples (if available)
6. Decision framework (which to use when)

## Success Criteria
- [ ] Analysis of 3+ AI architectures
- [ ] Performance data found
- [ ] Implementation guidance provided
- [ ] Debugging strategies documented
- [ ] Clear recommendations
- [ ] Word count: 2000-3500 words

## Search Terms

"Utility AI implementation"
"GOAP game AI"
"Behavior tree optimization"
"RimWorld AI design"
"Multi-agent game systems"
"AI debugging tools games"
"Game AI architecture"
"Scalable AI systems"

## Time Budget
3-4 hours
```

---

## Research Tracking Template

### Sources Found Log

| Date | Source | Game/Topic | Key Insights | Relevance | Cited |
|------|--------|-----------|--------------|-----------|-------|
| | | | | /10 | |

### Key Findings Database

| Category | Finding | Source | Applicability | Priority |
|----------|---------|--------|---------------|----------|
| Architecture | | | | |
| Performance | | | | |
| Design | | | | |
| Pitfalls | | | | |

---

## Expected Outcomes

### From This Research

**Technical Architecture Insights**:
- Proven multiplayer patterns
- Performance benchmarks
- Scalability limits
- Technology recommendations

**Development Process Insights**:
- Common time sinks
- Unexpected challenges
- Team structure recommendations
- Timeline estimates

**Pitfall Avoidance**:
- Known technical issues
- Design mistakes to avoid
- Performance anti-patterns
- Security vulnerabilities

**Validation**:
- Confirmation of technical choices
- Alternatives to consider
- Risk mitigation strategies
- Confidence in approach

---

## Integration Plan

After research completion:
1. Compile all sources into bibliography
2. Extract key technical insights
3. Update planning documents with findings
4. Revise risk assessment based on warnings
5. Adjust technical architecture if needed
6. Create implementation guidelines

---

**Instructions for Use**:
1. Assign research tasks to agents or team members
2. Use search terms to find relevant content
3. Fill out tracking templates as you go
4. Prioritize findings by relevance
5. Update planning documents with insights
6. Share findings with team
