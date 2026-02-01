# Research: Utility AI Systems and Consideration Curves

**Research ID**: R7  
**Date**: January 31, 2026  
**Source**: Curvature project (Mike Lewis), Guild Wars 2 implementation, GDC 2018 talk, Game AI Pro 3  
**Referenced In**: Session 2 AI System Design

---

## Executive Summary

Utility AI, specifically the Infinite Axis Utility System (IAUS), provides a data-driven, scalable approach to game AI decision-making. This research documents the architecture, consideration curves, and production-proven techniques from Guild Wars 2: Heart of Thorns and Path of Fire.

---

## 1. Why Utility AI Over Other Approaches

### Comparison of AI Architectures

| Approach | Scalability | Flexibility | Performance | Complexity |
|----------|-------------|-------------|-------------|------------|
| **Finite State Machines** | Poor | Low | High | Simple |
| **Behavior Trees** | Good | Medium | High | Medium |
| **GOAP** | Poor | High | Low | Complex |
| **Utility AI** | **Excellent** | **High** | **Medium** | **Medium** |
| **Neural Networks** | Good | Very High | Low | Very Complex |

### Session 2 Decision: Utility AI + Behavior Trees
**Rationale**:
- ✅ Scales to 100+ agents (O(n) vs GOAP's exponential)
- ✅ Predictable performance (bounded calculations)
- ✅ Data-driven (designers can tweak without code)
- ✅ Rich emergent behavior (multiplicative scoring)
- ✅ Interrupt handling (better than GOAP)

---

## 2. Infinite Axis Utility System (IAUS)

### Core Concepts

**Decision Score Evaluator (DSE)**
- Represents one possible action (e.g., "Attack", "Flee", "Heal")
- Scored by multiple considerations
- Final score = product of all consideration scores

**Consideration**
- One factor affecting a decision (e.g., "Distance to target", "Health remaining")
- Input: Raw game data (float, int, bool)
- Normalized to [0, 1] range
- Processed through response curve

**Response Curve**
- Mathematical function remapping [0, 1] → [0, 1]
- Types: Linear, Quadratic, Logistic, Exponential
- Designer-controlled importance weighting

### Scoring Formula
```
DSE_Score = Product(Consideration_Score[i] ^ Weight[i])

Where:
- Consideration_Score = ResponseCurve(NormalizedInput)
- Weight = Importance multiplier (0.0 to 2.0 typical)
- If any consideration = 0, DSE_Score = 0 (early-out)
```

---

## 3. Consideration Curves in Detail

### Common Curve Types

**Linear**
```
Output = Input
```
- Use when: Direct proportionality
- Example: Health (more health = more confident)

**Quadratic (U-shaped or Inverse)**
```
Output = Input² (U-shaped - prefers extremes)
Output = 1 - (1 - Input)² (Inverted - prefers middle)
```
- Use when: Optimal range exists
- Example: Distance (too close or too far = bad)

**Logistic (S-curve)**
```
Output = 1 / (1 + e^(-k(Input - midpoint)))
```
- Use when: Sharp threshold behavior
- Example: Danger detection (safe → dangerous transition)

**Exponential**
```
Output = Input^k (k > 1 = accelerating)
```
- Use when: Diminishing returns or urgency
- Example: Hunger (increasingly urgent as it grows)

### Guild Wars 2 Examples

**Tactical Movement - Distance Consideration**
```
Input: Distance to target (0-100m normalized to 0-1)
Curve: Logistic with midpoint at 25m
Result: Strong preference for 20-30m range
Purpose: Archers want optimal firing distance
```

**Combat - Health Consideration**
```
Input: Current health / Max health
Curve: Exponential (k=2)
Result: Below 50% health = rapidly fleeing
Purpose: Urgency increases non-linearly
```

**Social - Relationship Strength**
```
Input: Relationship score (-100 to +100 normalized)
Curve: Linear
Result: Direct proportion to willingness to help
Purpose: Simple linear preference
```

---

## 4. Data-Driven Architecture

### Designer Workflow
1. **Define Inputs**: What data can AI access? (health, distance, time, etc.)
2. **Create Considerations**: Pair inputs with curves
3. **Build Behaviors**: Group considerations into decisions
4. **Assign Archetypes**: Which behaviors for which agent types?
5. **Test in Sandbox**: Run scenarios, observe, iterate

### Benefits
- ✅ No programmer required for tuning
- ✅ Rapid iteration (change curves in real-time)
- ✅ Visual editing (Curvature tool)
- ✅ Reusable components (share considerations across behaviors)

---

## 5. Performance Optimizations

### Early-Out Optimization
```csharp
foreach (consideration in decision.considerations) {
    score = consideration.Evaluate();
    if (score == 0) {
        return 0; // Stop processing this decision
    }
    product *= score;
}
```
**Benefit**: Expensive considerations (pathfinding, raycasts) can be ordered last and skipped if cheap ones disqualify the decision.

### Caching Strategies
- **Input values**: Cache expensive calculations (distance, line-of-sight)
- **Consideration scores**: Valid for single tick only
- **DSE rankings**: Sort once per evaluation cycle

### Amortization
- **Goal evaluation**: Every 5 ticks (not every tick)
- **Planning**: Only when goal changes
- **Learning**: Every 10 ticks
- **Full DSE evaluation**: Only for top 3 candidate goals

---

## 6. Interrupt Handling

### Why Utility AI Excels at Interrupts

Unlike GOAP (which must complete or replan entire sequences), Utility AI can immediately re-evaluate:

```csharp
// Every tick (or every 5 ticks)
var currentGoalScore = EvaluateGoal(currentGoal);
var bestGoalScore = FindBestGoal();

if (bestGoalScore > currentGoalScore * interruptThreshold) {
    InterruptCurrentGoal();
    SwitchTo(bestGoal);
}
```

### Interrupt Types (Session 2)

**Critical Need** (InterruptThreshold = 0.0)
- Starvation, exhaustion, immediate danger
- Always interrupts

**Threat** (InterruptThreshold = 0.5)
- Enemies approaching
- Interrupts if current goal is low-priority

**Opportunity** (InterruptThreshold = 0.8)
- Rare chances, trading opportunities
- Only interrupts if current goal is unimportant

---

## 7. Weighted Random Selection

### Preventing "Hive Mind"

**Problem**: If all agents pick the highest-scoring goal, they behave identically (boring, robotic)

**Solution**: Weighted random from top 3 candidates
```csharp
var candidates = ScoreAllGoals().OrderByDescending().Take(3);
var selected = WeightedRandom(candidates, weights: [0.6, 0.3, 0.1]);
// 60% chance of #1, 30% chance of #2, 10% chance of #3
```

**Benefits**:
- ✅ Emergent diversity (identical agents behave differently)
- ✅ More interesting to watch
- ✅ Natural "personality" expression
- ✅ Handles ties gracefully

---

## 8. Session 2 Implementation

### Integration with Behavior Trees

**Utility AI decides WHAT to do**
- Selects goal (highest utility)
- Re-evaluates periodically
- Handles interrupts

**Behavior Trees decide HOW to do it**
- Executes sequences of actions
- Handles animation state machines
- Manages action preconditions

### Example: Combat Goal
```
[Utility AI]
Goal: Combat (score: 0.85)
- Consideration: Enemy proximity (score: 0.9, curve: logistic)
- Consideration: Health level (score: 0.8, curve: exponential)
- Consideration: Bravery trait (score: 1.0, multiplier: 1.2)

[Behavior Tree]
Execute Combat:
1. Select weapon (based on distance)
2. Move to optimal range
3. Attack sequence
4. Evaluate retreat condition
```

---

## 9. Production Validation

### Guild Wars 2: Heart of Thorns (2015)
- **Scale**: 100+ simultaneous NPCs in events
- **Complexity**: Dynamic world events, evolving objectives
- **Performance**: Consistent frame rates in large battles
- **Result**: Award-winning expansion

### Guild Wars 2: Path of Fire (2017)
- **Improvements**: Refined consideration curves
- **Features**: Mount AI, bounty hunting, dynamic difficulty
- **Result**: Continued successful use of IAUS

### Curvature Tool (2018)
- **Purpose**: Visual editor for Utility AI
- **Features**: Real-time tuning, sandbox testing
- **Release**: Open beta, used by indies and AAA

---

## 10. Best Practices

### Do's ✅
- Keep considerations cheap (avoid raycasts/pathfinding)
- Use early-out ordering (cheapest considerations first)
- Normalize all inputs to [0, 1] before curves
- Test with extreme values (0, 1, 0.5, edge cases)
- Visualize decision scores during development

### Don'ts ❌
- Use too many considerations (3-5 per decision is sweet spot)
- Mix different curve types without documentation
- Forget to handle all-zero scenarios
- Neglect interrupt thresholds
- Skip weighted random selection (leads to hive-mind)

---

## References

1. **Curvature Project** - github.com/apoch/curvature (Mike Lewis)
2. **GDC 2018 Talk** - "Winding Road Ahead: Designing Utility AI with Curvature"
3. **Game AI Pro 3** - Chapter 13: "Choosing Effective Utility-Based Considerations" (Mike Lewis)
4. **Infinite Axis Utility System** - Dave Mark (2013 GDC)
5. **Guild Wars 2 Developer Blogs** - ArenaNet AI programming articles

---

*Research compiled for Session 2 AI System Design verification*  
*Key Insight: Utility AI scales better than GOAP for 100+ agents while producing rich emergent behavior*  
*Last updated: January 31, 2026*
