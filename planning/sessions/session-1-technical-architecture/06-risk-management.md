# Day 1: Risk Management

> **Navigation**: [← Previous: Technology & Testing](05-technology-testing.md) | [Index]([AGENTS-READ-FIRST]-index.md) | [Next: Appendices](07-appendices.md)
> 
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

## 12. Technical Risk Assessment (MAJOR UPDATE)

### Top 5 Technical Risks (Updated with Research Evidence)

| Rank | Risk | Probability | Impact | Mitigation | Evidence |
|------|------|-------------|--------|------------|----------|
| 1 | **AI Performance** | **CONFIRMED HIGH** | Critical | Utility AI + Behavior Trees, LOD, tick budgeting | GOAP degrades past 20 agents; Utility AI scales to 200+ [r7-ai-systems-games.md] |
| 2 | **Multiplayer Sync** | **MANAGEABLE MEDIUM** | Critical | ENet + state sync + delta compression | Solutions documented; 0.6 KB/s achievable [r1-network-sync-research.md] |
| 3 | **Memory Usage** | **MANAGEABLE MEDIUM** | High | Object pooling, headless mode (70-80% savings), spatial partitioning | Headless mode validated; 5,000-10,000 entity limit [r1-godot-headless-research.md] |
| 4 | **Database Performance** | **HIGH if not careful** | **CRITICAL** | PostgreSQL from day one, connection pooling, batching | Eco's LiteDB disaster caused lag, timeouts [r3-eco-technical-postmortem.md] |
| 5 | **Godot Limitations** | **LOW** | High | Production-ready; active community | Godot 4.x proven in multiple multiplayer titles [r1-godot-multiplayer-research.md] |

**Revised Risk Assessments**:

**Risk 1: AI Performance** (Probability: High → CONFIRMED)
- **Evidence**: GOAP degrades past 20 agents; Utility AI handles 100-500 [r7-ai-systems-games.md]
- **Mitigation**: Use Utility AI + BT approach (already planned)
- **Validation**: Prototype 2 will test 100+ agents

**Risk 4: Database Performance** (Impact: Medium → CRITICAL, Probability: Medium → HIGH)
- **Evidence**: Eco's LiteDB bottleneck caused database read/write spikes, lag, and timeouts [r3-eco-technical-postmortem.md, Section 1.3]
- **Impact**: Will cause server lag, timeouts, unplayable experience
- **Mitigation**: PostgreSQL from day one with GIN indexes, connection pooling, batching

### New Risks Identified in Research

**Risk 6: Database I/O Bottlenecks** [r3-eco-technical-postmortem.md] - **HIGH**
- **Evidence**: Eco's LiteDB implementation caused database I/O bottlenecks with read/write operations creating lag
- **Impact**: Server lag, timeouts, player frustration
- **Mitigation**: PostgreSQL from day one, connection pooling (10-100), batch writes every 5s
- **Status**: Mitigated by architecture decision

**Risk 7: Multiplayer Timeline: 4+ Years** [r6-multiplayer-simulation-tech.md] - **HIGH**
- **Evidence**: Space Engineers took 4+ years; Factorio continuously refined for 8+ years
- **Impact**: Extended development, potential scope creep, delayed launch
- **Mitigation**: Start simple in Prototype 1 (localhost), use proven patterns (state sync), iterate rapidly
- **Status**: Managed by incremental approach

**Risk 8: AI Performance at Scale** [r7-ai-systems-games.md] - **MEDIUM**
- **Evidence**: GOAP fails at 20+ agents; F.E.A.R. rats caused CPU issues
- **Impact**: Unplayable with 100 agents if wrong architecture chosen
- **Mitigation**: Utility AI + BT approach (scales to 100-500 agents with optimization per [r7-ai-systems-games.md])
- **Status**: Mitigated by architecture decision

**Risk 9: Network Library Longevity** [r3-eco-technical-postmortem.md] - **MEDIUM**
- **Evidence**: Unity UNET deprecated, caused 7+ years of technical debt for Eco
- **Impact**: Forced migration, technical debt, feature limitations
- **Mitigation**: Godot ENet is native, actively maintained by Godot team
- **Status**: Lower risk than Unity UNET

**Risk 10: Single-Thread Limits** [r3-eco-technical-postmortem.md, r6-multiplayer-simulation-tech.md] - **MEDIUM**
- **Evidence**: Eco's core logic single-threaded; requires 12-16 cores for 100 players
- **Impact**: Can't use all CPU cores; vertical scaling hits ceiling
- **Mitigation**: Careful multi-threading design (ecosystem parallel, economy sequential)
- **Status**: Managed by architecture design

### Risks Mitigated by Research

**Validated Feasibility**:

1. **Technical Feasibility**: Eco proved the concept works [r2-eco-game-analysis.md, r3-eco-technical-postmortem.md]
   - All major systems (ecosystem, economy, governance) coexist successfully
   - 50-100 players achievable with proper hardware

2. **AI Believability**: Dwarf Fortress proved complex agents work [r4-dwarf-fortress-agents.md]
   - Memory systems create believable agents
   - Utility AI scales appropriately

3. **Network Sync**: Solutions documented and validated [r1-network-sync-research.md, r6-multiplayer-simulation-tech.md]
   - State sync approach validated
   - Bandwidth targets achievable (0.6 KB/s vs 76 KB/s snapshots)

4. **Database Strategy**: PostgreSQL validated [r1-postgresql-jsonb-research.md]
   - GIN indexes provide <1ms query times
   - JSONB flexibility proven

5. **Performance Targets**: Achievable based on benchmarks [r1-research-summary.md]
   - 20 TPS validated by Eco
   - 112 KB/s bandwidth calculated and achievable
   - 5,000-10,000 entity limit in headless mode

### Prototyping Needs (Updated)

**Priority Tests with Research Validation**:

1. **Agent Stress Test**: 200+ agents [r1-research-summary.md]
   - **What to measure**: CPU per agent, memory per agent, decision time
   - **When**: Prototype 2 (Month 1, Week 3-4)
   - **Success criteria**: <2ms per agent decision, maintain 20 TPS
   - **Risk if failed**: Need to reduce agent count, simplify AI, or reduce tick rate to 15 TPS

2. **Network Sync Test**: 20 players [r1-research-summary.md]
   - **What to measure**: Bandwidth, latency, desync rate
   - **When**: Prototype 2 (Month 2)
   - **Success criteria**: <112 KB/s per player, <50ms latency, <1% desync
   - **Risk if failed**: Need aggressive culling or reduced sync rate

3. **Database Load Test**: High write load [r1-research-summary.md, r3-eco-technical-postmortem.md]
   - **What to measure**: Write throughput, query latency, connection pool usage
   - **When**: Prototype 1 (Month 1, Week 4)
   - **Success criteria**: <100ms query time, handle 1000 writes/minute
   - **Risk if failed**: PostgreSQL tuning or batching optimization

4. **Godot Headless Validation**: Performance [r1-research-summary.md]
   - **What to measure**: CPU usage, memory footprint, entity limit
   - **When**: Prototype 1 (Month 1, Week 2)
   - **Success criteria**: 40-60% CPU reduction vs graphical, <1GB RAM for 100 agents
   - **Risk if failed**: Re-evaluate server architecture

### Risk Monitoring Plan

**Performance Metrics to Track**:

| Risk | Metric | Early Warning | Critical Threshold | Action |
|------|--------|---------------|-------------------|--------|
| AI Performance | CPU per agent | >3ms/agent | >5ms/agent | Reduce agent count, simplify AI |
| Database | Query latency | >50ms | >100ms | Enable read replicas, optimize queries |
| Memory | RAM usage | >6GB | >8GB | Restart server, investigate leaks |
| Network | Bandwidth/player | >150 KB/s | >200 KB/s | Reduce sync radius, enable culling |
| Tick Rate | TPS stability | <18 TPS | <15 TPS | Reduce quality, alert admin |

**Early Warning Indicators**:
- **CPU > 75% sustained** → Reduce tick rate or sync radius
- **Database queries > 100ms** → Enable read replicas, check indexes
- **AI decision time > 5ms per agent** → Switch to simpler behavior trees
- **Bandwidth > 150 KB/s per player** → Aggressive spatial culling
- **Memory growth > 100MB/hour** → Object pool tuning, leak detection

**Contingency Triggers**:
- **If CPU > 90%**: Immediately reduce tick rate to 15 TPS, reduce sync radius 50%
- **If DB lag > 200ms**: Enable emergency read-only mode for analytics, prioritize game writes
- **If AI performance degrades**: Switch distant agents to "dumb" behavior (no planning)
- **If bandwidth exceeded**: Disconnect non-critical updates (weather, distant agents)

**Monitoring Tools**:
```csharp
public class RiskMonitor {
    public void CheckMetrics() {
        // CPU monitoring
        if (GetCurrentCpuPercent() > 0.75f) {
            GD.PushWarning("CPU usage high - consider quality reduction");
        }
        
        // Database monitoring
        if (GetAvgQueryTime() > 100) {
            GD.PushWarning("Database lag detected - enabling optimizations");
        }
        
        // AI monitoring
        if (GetAvgAgentDecisionTime() > 5.0f) {
            GD.PushWarning("AI performance degrading - switching to simple mode");
        }
    }
}
```

**Dashboard Requirements**:
- Real-time CPU, memory, bandwidth graphs
- Per-subsystem performance breakdown
- Database query time histogram
- Agent decision time tracking
- Network latency per player

---

## 13. Open Questions & Future Research

### Unresolved Technical Questions (Prioritized)

#### **HIGH PRIORITY - Answer Before Prototyping (Month 1)**
- [ ] **Q1** What's the actual CPU cost per AI agent? **(Blocks: Prototype 2 AI scope)**
  - *Answer Path*: Prototype 1 agent stress test with profiling
  - *Timeline*: Month 1, Week 3-4
  
- [ ] **Q2** How much network bandwidth does ENet use under load? **(Blocks: Player limit decisions)**
  - *Answer Path*: Prototype 1 network sync test with 20 clients
  - *Timeline*: Month 1, Week 3-4

- [ ] **Q3** Can Godot handle 5000+ entities efficiently? **(Blocks: Entity system design)**
  - *Answer Path*: Prototype 1 entity stress test
  - *Timeline*: Month 1, Week 2-3

- [ ] **Q4** What's the optimal spatial partitioning grid size? **(Blocks: Performance optimization)**
  - *Answer Path*: Benchmark different grid sizes during Prototype 1
  - *Timeline*: Month 1, Week 3-4

#### **MEDIUM PRIORITY - Answer During Prototyping (Months 1-3)**
- [ ] **Q5** How much memory does PostgreSQL use for world state? **(Blocks: Server sizing)**
  - *Answer Path*: Database load test during Prototype 2
  - *Timeline*: Month 2, Week 2-3

- [ ] **Q6** What's the test execution time impact on development velocity? **(Blocks: CI/CD optimization)**
  - *Answer Path*: Measure build/test times after Week 2 setup
  - *Timeline*: Month 1, Week 3 ongoing

- [ ] **Q7** How testable is Godot.Node-based code in practice? **(Blocks: Testing strategy refinement)**
  - *Answer Path*: Real-world testing during Prototype 1 implementation
  - *Timeline*: Month 1, Week 2-4

- [ ] **Q8** How do we test non-deterministic AI behavior? **(Blocks: AI testing strategy)**
  - *Answer Path*: Develop testing patterns during Prototype 2
  - *Timeline*: Month 2, Week 3-4

#### **LOW PRIORITY - Answer Later/Defer (Months 3+)**
- [ ] **Q9** How much code coverage is realistic for Godot C# projects?
  - *Defer To*: After 3 months of development, review coverage reports
  - *Current Hypothesis*: 70-80% achievable based on testable architecture

- [ ] **Q10** Can we achieve <5 minute CI builds with full test suite?
  - *Defer To*: Month 3+ when test suite is substantial
  - *Current Hypothesis*: Possible with caching and parallelization

- [ ] **Q11** What's the performance overhead of Testcontainers for PostgreSQL?
  - *Defer To*: Month 2+ when integration test volume increases
  - *Current Hypothesis*: 10-20% overhead acceptable for test reliability

- [ ] **Q12** What's the optimal balance between unit and integration tests?
  - *Defer To*: Month 3+ based on real development experience
  - *Current Hypothesis*: 80/20 unit/integration as documented

- [ ] **Q13** What's the cost of maintaining test data fixtures?
  - *Defer To*: Month 2+ when fixture library grows
  - *Current Hypothesis*: Low with factory pattern approach

- [ ] **Q14** How do we test multiplayer synchronization without real clients?
  - *Defer To*: Month 3+ (Prototype 3+)
  - *Answer Path*: Bot-based testing framework

### Answered Questions (From Research)

**Q1: What is Godot's actual entity limit?** → **ANSWERED**: 5,000-10,000 entities in headless mode (See [r1-godot-headless-research.md, Section 2])

**Q2: How much bandwidth for 100 agents?** → **ANSWERED**: 112 KB/s per player at 20 TPS (See [r1-research-summary.md, Key Finding 4])

**Q3: Optimal snapshot frequency?** → **ANSWERED**: Every 2 seconds (Factorio approach, See [r1-factorio-case-study.md])

### New Questions Identified in Research

**Q4: AI-to-Human Economic Equivalence**
- **Question**: How many AI agents = 1 human player in economic activity?
- **Impact**: Determines AI-to-human ratio for server balancing
- **Priority**: HIGH
- **Answer Path**: Prototype 2 economic testing

**Q5: Entity Count Threshold for ECS Migration**
- **Question**: At what entity count does OOP → ECS become necessary?
- **Impact**: Architecture decision timeline  
- **Priority**: MEDIUM
- **Answer Path**: Prototype 1 stress testing

**Q6: Godot 4.x Performance Ceiling**
- **Question**: What is practical entity limit before degradation?
- **Impact**: World size and agent count targets
- **Priority**: HIGH  
- **Answer Path**: Prototype 1 profiling

**Q7: Law System Complexity Tolerance**
- **Question**: How complex can laws be before players overwhelmed?
- **Impact**: Law DSL design
- **Priority**: MEDIUM
- **Answer Path**: Prototype 3 user testing

**Q8: Visualization Effectiveness**
- **Question**: Which data visualizations best drive environmental behavior?
- **Impact**: Map layer design
- **Priority**: LOW
- **Answer Path**: A/B testing in Prototype 4-5

### Research Timeline

**Month 1 (Prototype 1)**:
- Answer Q6: Performance ceiling testing
- Begin database optimization

**Month 2 (Prototype 2)**:
- Answer Q4: AI economic equivalence
- Test AI scaling

**Month 3 (Prototype 3)**:
- Answer Q7: Law complexity testing
- Governance UI validation

**Month 4-5 (Prototypes 4-5)**:
- Answer Q5: ECS threshold determination
- Answer Q8: Visualization effectiveness

### Research Needed (Updated)

- [x] Godot 4.x multiplayer best practices **(Answered - See Research Summary)**
- [x] ENet optimization techniques **(Answered - See Research Summary)**
- [x] PostgreSQL JSONB performance patterns **(Answered - See Research Summary)**
- [x] Deterministic simulation techniques (lockstep vs. state sync) **(Answered - See Research Summary)**
- [ ] Replay system implementations in other games **(Defer to Month 2+)**
  - *Rationale*: Save/replay system design complete, implementation details can wait
  - *Sources to review*: Factorio replay system, RTS game postmortems

---

## 14. Decisions Log

### Session 1 - [Date]

#### Decision 1: Use Godot 4.x + C#
**Decision**: Godot 4.x with C# as primary development stack
**Rationale**: Free, excellent multiplayer support, familiar workflow, MIT license
**Research Evidence**: 
- Godot 4.3 MultiplayerAPI production-ready ([r1-godot-multiplayer-research.md])
- Headless mode 40-60% CPU reduction validated ([r1-godot-headless-research.md])
- C# 2-5x faster than GDScript ([r1-godot-headless-research.md])
**Confidence**: HIGH (validated by research)
**Reversibility**: NO (fundamental choice)
**Alternatives Considered**: Unity (expensive, UNET deprecated per [r3-eco-technical-postmortem.md]), Unreal (overkill)

#### Decision 2: Use ENet Networking
**Decision**: Godot's native ENet implementation for multiplayer
**Rationale**: Native Godot support, UDP performance, low-level control
**Research Evidence**:
- ENet protocol designed for games ([r1-enet-protocol-research.md])
- 112 KB/s bandwidth validated ([r1-research-summary.md, Key Finding 4])
- 255 channels, reliable+unreliable separation ([r1-enet-protocol-research.md])
**Confidence**: HIGH (validated by research)
**Reversibility**: YES (major refactoring required)
**Alternatives Considered**: WebSockets (higher latency), Custom TCP (more work)

#### Decision 3: PostgreSQL for Production + SQLite for Dev
**Decision**: Dual database strategy with PostgreSQL (production) and SQLite (dev/single-player)
**Rationale**: PostgreSQL proven at scale, SQLite zero-setup for development
**Research Evidence**:
- PostgreSQL JSONB 0.5-0.8ms queries with GIN indexes ([r1-postgresql-jsonb-research.md])
- Eco's LiteDB disaster avoided ([r3-eco-technical-postmortem.md])
- SQLite sufficient for single-player ([r1-postgresql-jsonb-research.md])
**Confidence**: HIGH (validated by research)
**Reversibility**: YES (with migration effort)
**Alternatives Considered**: MongoDB (less relational), MySQL (similar to PostgreSQL)

#### Decision 4: Offline Mode = Local Server
**Decision**: Single-player runs headless server locally via localhost
**Rationale**: No code duplication, enables multiplayer migration via world export/import, consistent behavior
**Research Evidence**:
- Headless mode 70-80% memory reduction ([r1-godot-headless-research.md])
- `CallLocal = true` RPC reduces latency ([r1-godot-multiplayer-research.md])
- Same code paths prevent maintenance burden ([r3-eco-technical-postmortem.md])
**Confidence**: HIGH (validated by research)
**Reversibility**: NO (architectural commitment)
**Alternatives Considered**: Separate single-player codebase (rejected)

#### Decision 5: Event-Sourced Save System
**Decision**: Event sourcing with snapshots + replay capability
**Rationale**: Replay capability, debugging, branching worlds, audit trail
**Research Evidence**:
- Factorio's deterministic replay for debugging ([r1-factorio-case-study.md, Section 4])
- Megapacket batching 90% overhead reduction ([r1-factorio-case-study.md, Section 5])
- Event log enables "what happened at tick X?" debugging ([r1-factorio-case-study.md])
**Confidence**: HIGH (validated by research)
**Reversibility**: YES (with data migration)
**Alternatives Considered**: Simple state snapshots (no replay)

#### Decision 6: Comprehensive Testing from Day One
**Decision**: xUnit + Testcontainers + CI/CD testing infrastructure from project start
**Rationale**: Prevents technical debt, ensures quality, enables confident refactoring
**Research Evidence**:
- Test everything reasonably testable ([r1-research-summary.md, Decision 6])
- CI/CD from day one prevents debt ([r1-research-summary.md])
- Interface-based testing enables mocking ([r6-multiplayer-simulation-tech.md])
**Confidence**: HIGH (industry best practice)
**Reversibility**: YES (by removing tests)
**Alternatives Considered**: Manual testing only (rejected), Postpone testing (rejected)

#### Decision 7: State Synchronization over Lockstep
**Decision**: Use state sync (not deterministic lockstep)
**Rationale**: 
- **Flexibility**: Variable tick rates (10-30 TPS) and time acceleration (2x-10x) possible
- **Simplicity**: No determinism requirements for AI randomness or floating-point economy
- **Join/Leave**: Easier mid-game joining without replay catchup (vs lockstep)
- **Trade-off**: Uses ~4x more bandwidth than lockstep (0.6 KB/s vs 0.14 KB/s) but avoids determinism complexity
- **Comparison**: 0.6 KB/s (state sync) vs 76 KB/s (naive snapshots) - 99% reduction vs snapshots
**Research Evidence**:
- State sync analysis ([r1-network-sync-research.md, Section 4])
- Multiplayer architecture comparison ([r6-multiplayer-simulation-tech.md])
- Lockstep complexity ([r1-factorio-case-study.md])
**Impact**: Network architecture, 32-112 KB/s realistic target (scales with agent count)
**Confidence**: HIGH
**Reversibility**: NO (fundamental networking choice)

#### Decision 8: Utility AI + Behavior Trees
**Decision**: Hybrid AI architecture (Utility for decisions, BT for actions)
**Rationale**:
- Utility AI: 100-500 agents scalable (vs GOAP's 10-20 agents)
- BT: 50-100+ agents for action execution
- GOAP only scales to 10-20 agents (insufficient for Societies)
**Research Evidence**:
- AI architecture comparison ([r7-ai-systems-games.md, Section 7] - R7 contains Utility AI vs GOAP scalability analysis)
- Agent complexity patterns ([r4-dwarf-fortress-agents.md] - for memory/personality systems)
**Impact**: AI system design, Session 2 planning
**Confidence**: HIGH
**Reversibility**: YES (major refactoring possible)
**Note**: Detailed AI behavior specification deferred to Session 2 (AI System Design)

#### Decision 9: Spatial Partitioning Mandatory
**Decision**: Implement 100m chunk spatial partitioning from day one
**Rationale**:
- O(n²) → O(n) for entity interactions
- Mandatory for 1000+ entities
- Eco used successfully ([r1-eco-performance-research.md])

**100m Chunk Size Derivation**:
- Based on agent interaction radius (50m) × 2 for neighbor coverage
- Cache-friendly alignment with spatial query patterns  
- Eco's successful implementation at similar scale ([r1-eco-performance-research.md])
- Balances granularity (more chunks = more overhead) vs. query efficiency
- 0.5 km² world = 50 chunks (manageable memory overhead)

**Alternatives Considered**:
- **Quadtree**: Better for uneven entity distribution, but higher overhead for uniform worlds; more complex implementation
- **Octree**: Optimized for 3D space, unnecessary overhead for ground-level simulation where Y-axis variation is minimal
- **Hash-based spatial indexing**: Good for exact lookups, poor for range queries needed for agent perception
- **Uniform Grid (100m)**: Selected for simplicity, cache locality, and suitability for evenly-distributed agents; easiest to debug and profile

**Research Evidence**:
- Spatial partitioning analysis ([r1-eco-performance-research.md])
- Scaling patterns ([r6-multiplayer-simulation-tech.md])
**Impact**: Entity system architecture, performance
**Confidence**: HIGH
**Reversibility**: YES (high refactoring cost)

---

## 16. Success Criteria

- [x] Clear technology stack chosen with rationale **(Section 8)**
- [x] System architecture diagram complete **(Section 1 - 3 complete with Mermaid diagrams)**
- [x] Performance targets defined **(Section 7 - MVP and Stretch Goal tables)**
- [x] Technical risks identified and prioritized **(Section 11 - Top 5 risks table)**
- [x] Prototype needs identified **(Section 11.4 and cross-references to Day 6)**
- [x] Offline mode architecture defined **(Section 4 with diagram)**
- [x] Save/replay system designed **(Section 5 with event-sourced architecture)**
- [x] Database schema outlined **(Section 6 with ER diagram)**
- [x] Testing architecture defined with technology choices **(Section 9)**
- [x] Test project structure documented **(Section 9 - test project structure)**
- [x] CI/CD testing strategy specified **(Section 9 - GitHub Actions workflow)**
- [x] Database testing approach (PostgreSQL + SQLite) documented **(Section 9)**
- [x] Godot testing strategy with workarounds **(Section 9 - Godot.XUnit)**
- [x] Network testing plan (deferred but architected) **(Section 9 - INetworkManager interface)**
- [x] Testing timeline by prototype phase **(Section 9 - testing table by prototype)**

---

**Previous**: [← Technology & Testing](05-technology-testing.md) | **Next**: [Appendices →](07-appendices.md)
