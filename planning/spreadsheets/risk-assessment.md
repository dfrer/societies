# Risk Assessment Spreadsheet

This document serves as a template for the Excel spreadsheet with risk matrices, mitigation strategies, and validation gates.

## Sheet 1: Technical Risks

| ID | Risk | Probability | Impact | Risk Score | Mitigation Strategy | Owner | Status |
|----|------|-------------|--------|------------|---------------------|-------|--------|
| T1 | AI performance at 100+ agents | High (70%) | Critical (10) | 7.0 | Aggressive LOD, tick budgeting, behavior simplification | Core Systems | Open |
| T2 | Multiplayer desync issues | Medium (50%) | Critical (10) | 5.0 | Deterministic simulation, state reconciliation, testing | Networking | Open |
| T3 | Database performance bottleneck | Medium (40%) | High (8) | 3.2 | Caching layer, query optimization, read replicas | Backend | Open |
| T4 | Godot engine limitations | Low (20%) | High (8) | 1.6 | Research upfront, community support, fallback strategies | Core Systems | Open |
| T5 | Memory leaks over long sessions | Medium (40%) | High (8) | 3.2 | Profiling, object pooling, automated testing | Core Systems | Open |
| T6 | Network latency unacceptable | Medium (30%) | Critical (10) | 3.0 | Client prediction, interpolation, regional servers | Networking | Open |
| T7 | Save system corruption | Low (15%) | Critical (10) | 1.5 | Backups, validation, atomic writes | Backend | Open |
| T8 | Platform compatibility issues | Low (20%) | Medium (6) | 1.2 | PC-only scope, testing matrix | QA | Open |

### Risk Score Formula
```
Risk Score = Probability × Impact

Probability Scale:
- Low: 0-30%
- Medium: 31-60%
- High: 61-100%

Impact Scale:
- Low: 1-3 (minor delay)
- Medium: 4-6 (moderate delay)
- High: 7-8 (major delay)
- Critical: 9-10 (project threat)
```

### Technical Risk Matrix

```
Impact
 10 |                        T1, T2
    |                              T6
  8 |         T3, T4, T5
    |
  6 |                              T8
    |
  4 |
    |
  2 |
    |
  0 +----+----+----+----+----+----+----+----+----+----
    0   10   20   30   40   50   60   70   80   90   100
                        Probability (%)
```

---

## Sheet 2: Design Risks

| ID | Risk | Probability | Impact | Risk Score | Mitigation Strategy | Contingency |
|----|------|-------------|--------|------------|---------------------|-------------|
| D1 | AI behavior feels robotic | High (65%) | Critical (9) | 5.9 | Multiple brain configs, extensive playtesting | Simplify AI, focus on economic rather than social |
| D2 | Governance too complex/tedious | Medium (50%) | High (8) | 4.0 | Progressive disclosure, templates, automation | Reduce scope to simple voting only |
| D3 | Not fun - poor game feel | Medium (40%) | Critical (10) | 4.0 | Early playtesting, iterate on core loop | Pivot to more survival-focused gameplay |
| D4 | Economy broken (exploits/inflation) | High (60%) | High (8) | 4.8 | Economic modeling, data-driven balancing | Manual intervention, rule patches |
| D5 | Progression too slow/fast | Medium (45%) | Medium (6) | 2.7 | Spreadsheet modeling, adjustable parameters | Server config settings |
| D6 | Scope creep | High (70%) | High (7) | 4.9 | Strict MVP definition, regular scope reviews | Cut features aggressively |
| D7 | Multiplayer toxicity/griefing | Medium (35%) | Medium (6) | 2.1 | Governance tools, reporting, moderation | Private servers, whitelist |
| D8 | Learning curve too steep | Medium (40%) | High (7) | 2.8 | Tutorial system, progressive complexity | Simplify systems, better guidance |

### Design Validation Gates

| Gate | When | Criteria | Go/No-Go |
|------|------|----------|----------|
| Fun Factor | Proto 2 | 7+/10 rating from 5+ testers | ≥6 to continue, <6 to pivot |
| AI Authenticity | Proto 2 | "Feels alive" from 70%+ testers | ≥60% to continue |
| Governance Engagement | Proto 3 | 60%+ participation in tests | ≥50% to continue |
| Economic Balance | Proto 4 | No exploits found in 2 weeks | 0 exploits to continue |
| Progression Feel | Proto 4 | Satisfying progression reported | ≥70% positive to continue |

---

## Sheet 3: Market/Business Risks

| ID | Risk | Probability | Impact | Risk Score | Mitigation | Impact if Occurs |
|----|------|-------------|--------|------------|------------|------------------|
| B1 | Too niche - small audience | Medium (45%) | High (8) | 3.6 | Clear marketing, unique positioning | Lower sales, adjust budget |
| B2 | Competition from similar games | High (65%) | Medium (5) | 3.3 | AI-human equivalence differentiator | Fight for market share |
| B3 | Monetization failure | Low (20%) | Critical (9) | 1.8 | Sustainable budget, low burn rate | Pivot to different model |
| B4 | Negative reviews at launch | Medium (35%) | High (8) | 2.8 | Extensive testing, EA if needed | Damage control, updates |
| B5 | Server costs exceed revenue | Low (25%) | High (7) | 1.8 | Self-hosting option, efficiency | Reduce server features |
| B6 | Platform policy changes | Low (15%) | Medium (5) | 0.8 | PC direct sales backup | Adjust distribution |
| B7 | Team burnout (solo) | Medium (40%) | High (7) | 2.8 | Flexible timeline, scope control | Extend timeline, get help |
| B8 | Scope too large for solo | Medium (50%) | Critical (9) | 4.5 | Strict MVP, incremental delivery | Cut scope, delay launch |

### Financial Scenarios

| Scenario | Probability | Revenue | Expenses | Outcome |
|----------|-------------|---------|----------|---------|
| **Best Case** | 20% | $100K+ | $15K | Profit: $85K+ |
| **Good Case** | 40% | $50K | $12K | Profit: $38K |
| **Expected** | 30% | $20K | $10K | Profit: $10K |
| **Poor Case** | 9% | $5K | $8K | Loss: -$3K |
| **Worst Case** | 1% | $1K | $15K | Loss: -$14K |

**Expected Value**: (0.20×$85K) + (0.40×$38K) + (0.30×$10K) + (0.09×-$3K) + (0.01×-$14K) = **$33.9K profit**

---

## Sheet 4: Risk Mitigation Strategies

### High Priority Risks (Score > 4.0)

| Risk | Mitigation Plan | Timeline | Effort | Status |
|------|----------------|----------|--------|--------|
| T1: AI Performance | Profile early, implement LOD, tick budgeting, stress test at 100 agents | Proto 2 | High | Planned |
| D1: AI Robotic | 3 brain configs, extensive playtesting, behavior diversity | Proto 2 | High | Planned |
| D4: Economy Broken | Spreadsheet modeling, automated testing, data validation | Proto 4 | Medium | Planned |
| D6: Scope Creep | Weekly scope reviews, strict MVP definition, feature freeze dates | Ongoing | Low | Active |
| B8: Scope Too Large | Aggressive cutting, defer to post-launch, validate necessity | Ongoing | Medium | Active |

### Medium Priority Risks (Score 2.5-4.0)

| Risk | Mitigation Plan | Timeline | Status |
|------|----------------|----------|--------|
| T2: Multiplayer Sync | Deterministic sim, reconciliation, 20-player stress test | Proto 2 | Planned |
| D2: Governance Complex | Progressive UI, templates, user testing | Proto 3 | Planned |
| D3: Not Fun | Early playtesting, iterate on feedback, pivot if needed | Proto 2 | Planned |
| B1: Niche Appeal | Marketing clarity, community building, demo availability | Pre-launch | Planned |
| B7: Burnout | Flexible schedule, milestone celebrations, scope control | Ongoing | Active |

### Low Priority Risks (Score < 2.5)

| Risk | Mitigation Plan | Status |
|------|----------------|--------|
| T3: Database Perf | Caching, optimization, monitoring | Planned |
| T4: Godot Limits | Research, community, alternatives | Planned |
| D5: Progression Pace | Adjustable settings, data monitoring | Planned |
| D7: Toxicity | Governance tools, moderation, private servers | Planned |
| D8: Learning Curve | Tutorial, progressive disclosure, documentation | Planned |

---

## Sheet 5: Go/No-Go Decision Points

### Prototype Gates

| Gate | Decision Criteria | Go Criteria | No-Go Criteria | Action if No-Go |
|------|------------------|-------------|----------------|-----------------|
| **Proto 1** | World simulation works | 60+ FPS, stable, engaging | <30 FPS, crashes, boring | Optimize or change engine |
| **Proto 2** | AI authentic, economy works | 70%+ "feels alive", trades occur | Robotic, broken economy | Simplify AI, fix economy |
| **Proto 3** | Governance usable | 50%+ engagement, laws work | Confusing, broken | Cut governance scope |
| **Proto 4** | Progression engaging | 60%+ defeat meteor, satisfied | Too easy/hard, boring | Adjust difficulty |
| **Proto 5** | Environment meaningful | Pollution creates response | Ignored or overwhelming | Adjust visibility/impact |
| **Alpha** | Complete game loop | 3+ hour sessions, retention | Not fun, unstable | Major revision |

### Monthly Review Gates

| Month | Review Focus | Continue If | Pivot If |
|-------|-------------|-------------|----------|
| Month 3 | Core systems | Systems functional | Fundamental issues |
| Month 6 | Alpha ready | Engagement positive | Not fun |
| Month 9 | Beta ready | Retention good | Players leaving |
| Month 12 | Launch ready | Stable, complete | Critical bugs |

---

## Sheet 6: Contingency Plans

### If Performance Issues

**Plan A**: Optimize
- Profile and optimize hot paths
- Implement aggressive LOD
- Reduce agent count (150 instead of 200)
- Simplify agent behaviors

**Plan B**: Scale Down
- Reduce world size
- Limit concurrent players
- Simplify simulation

**Plan C**: Technical Pivot
- Move to Unity (if Godot insufficient)
- Custom engine components
- Cloud compute for AI

### If Not Fun

**Plan A**: Iterate
- Focus group feedback
- Rapid prototyping of alternatives
- A/B testing mechanics

**Plan B**: Pivot Scope
- Focus on strongest aspect (economy? building?)
- Cut weak systems
- Emphasize multiplayer interaction

**Plan C**: Concept Pivot
- Shift to pure survival (add monsters)
- Make PvP optional
- Focus on creative mode

### If Solo Development Fails

**Plan A**: Extend Timeline
- Push back launch date
- Reduce scope further
- Seek part-time help

**Plan B**: Team Up
- Find co-developer
- Revenue share arrangement
- Open source community

**Plan C**: Pivot Scale
- Make smaller game first
- Release in episodes
- Focus on single-player initially

---

## Sheet 7: Risk Monitoring

### Weekly Risk Review

| Week | Top 3 Risks | Status Changes | New Risks | Mitigation Progress |
|------|-------------|----------------|-----------|---------------------|
| W1 | Scope creep, AI perf, Timeline | - | - | Scope reviews started |
| W2-4 | AI perf, Multiplayer, Fun | - | Database concerns | Profiling planned |
| W5-8 | Fun factor, Economy, Scope | AI perf ↓ | - | Proto 2 testing soon |
| W9-12 | Governance, Progression, Balance | Fun factor ↓ | - | Iteration ongoing |
| W13-16 | Polish, Retention, Launch | Governance ↓ | - | Beta prep |
| W17+ | Launch risks, Reception, Servers | - | Post-launch risks | Monitoring active |

### Risk Dashboard Metrics

```
┌─────────────────────────────────────┐
│ RISK DASHBOARD                      │
├─────────────────────────────────────┤
│                                     │
│ Critical Risks:     2  ████░░░░░░   │
│ High Risks:         4  ████████░░   │
│ Medium Risks:       6  ██████████   │
│ Low Risks:          8  ██████████   │
│                                     │
│ Mitigated:         45% █████░░░░░   │
│ In Progress:       35% ███░░░░░░░   │
│ Open:              20% ██░░░░░░░░   │
│                                     │
│ Next Review: Friday 5PM             │
└─────────────────────────────────────┘
```

---

## Excel Implementation Notes

### Formulas

1. **Risk Score**: `=Probability*Impact`
2. **Weighted Risk**: `=RiskScore*Weight`
3. **Total Risk**: `=SUM(RiskScores)`
4. **Mitigation Progress**: `=MitigatedRisks/TotalRisks`
5. **Expected Value**: `=SUM(Probability*Outcome)`

### Conditional Formatting

**Risk Score Colors**:
- Red: >6.0 (Critical - immediate action)
- Orange: 4.0-6.0 (High - action needed)
- Yellow: 2.5-4.0 (Medium - monitor)
- Green: <2.5 (Low - track)

**Status Colors**:
- Red: Open (no mitigation)
- Yellow: In Progress (mitigation started)
- Green: Mitigated (resolved)

### Charts to Create

1. **Risk Matrix**: Scatter plot (Probability × Impact)
2. **Risk Trends**: Line chart over time
3. **Category Breakdown**: Pie chart by risk type
4. **Mitigation Progress**: Progress bars
5. **Heat Map**: Risk by category and severity

### Pivot Tables

1. **By Category**: Technical vs Design vs Business
2. **By Status**: Open vs In Progress vs Mitigated
3. **By Owner**: Responsibility assignment
4. **By Timeline**: When risks need attention

---

**Instructions for Excel**:
1. Create workbook: "risk-assessment.xlsx"
2. Create sheets: "Technical", "Design", "Business", "Mitigation", "Gates", "Contingency", "Monitoring"
3. Implement risk scoring formulas
4. Add conditional formatting for visual priority
5. Create risk matrix scatter chart
6. Add data validation for probability/impact ranges
7. Create dashboard summary on first sheet
8. Set up weekly review reminder

**Risk Management Process**:
1. Identify risks during planning
2. Score and prioritize
3. Assign owners
4. Develop mitigation strategies
5. Monitor weekly
6. Update as project evolves
7. Trigger contingencies when needed
