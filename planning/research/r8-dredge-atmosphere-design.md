# Research: Atmosphere and Narrative Tension in Game Design

**Research ID**: R8  
**Date**: January 31, 2026  
**Source**: Dredge game analysis, Black Salt Games interviews, atmospheric horror design patterns  
**Referenced In**: Session 2 AI System Design

---

## Executive Summary

This research examines atmospheric tension design through the lens of Dredge (2023), a fishing horror game that masterfully combines seemingly incompatible genres (cozy fishing simulation + cosmic horror) to create emergent narrative through environmental storytelling and systemic gameplay.

---

## 1. The Dredge Approach: Juxtaposition as Design

### Core Design Philosophy
**"Cozy fishing meets cosmic horror"**

Dredge demonstrates how juxtaposition of contrasting tones creates tension:
- **Day**: Peaceful, productive, routine (cozy fishing)
- **Night**: Uncanny, dangerous, mysterious (cosmic horror)
- **Transition**: The boundary itself becomes the source of anxiety

### Session 2 Application
**AI Agent Behavior Juxtaposition**:
```
Safe Environment (Day/Market):
- Agents engage in routine economic behavior
- Predictable patterns (work, trade, socialize)
- Player feels comfortable, in control

Uncanny Events (Night/Crisis):
- Agents exhibit unusual behaviors (panic, greed, betrayal)
- Systemic disruptions (market crashes, political upheaval)
- Player feels uncertainty, lack of control
```

---

## 2. Environmental Storytelling Techniques

### Show, Don't Tell
**Dredge Technique**:
- No explicit exposition about the horror
- Players discover through environmental clues
- Ambient details (strange fish, fog, sounds)

**Session 2 Adaptation**:
- **Gossip System**: Agents discuss events player didn't witness
- **Environmental Changes**: NPC reactions hint at off-screen events
- **Gradual Revelation**: Player pieces together society's state through observation

### Fog as Gameplay Mechanic
**Dredge**: Fog limits visibility, creates uncertainty, forces navigation decisions

**Session 2**: Information asymmetry creates similar tension:
- Agents have private knowledge (memories, beliefs)
- Player only sees public behavior
- Gossip reveals fragments of hidden information
- Market prices reflect secret knowledge (insider trading anxiety)

---

## 3. Day/Night Cycle Psychology

### Safety vs. Danger Rhythm
**Dredge Pattern**:
- 12 hours of safe daylight (resource gathering)
- Increasing danger as sun sets
- Climactic tension at night
- Relief at dawn (if survived)

**Session 2 Economic Cycles**:
```
Market Open (Morning):
- Prices stabilize overnight
- Agents trade openly
- Information is public

Peak Activity (Midday):
- Maximum trading volume
- Prices fluctuate with supply/demand
- Opportunities emerge

Market Close (Evening):
- Last-minute deals
- Price uncertainty increases
- Agents form beliefs about next day

Night (Off-Market):
- Gossip spreads private information
- Emergency transactions (black market)
- Beliefs solidify before morning
```

---

## 4. Cosmic Horror Elements in Social Systems

### The Unknowable
**Dredge**: Ancient entities beyond comprehension

**Session 2**: Complex emergent systems beyond individual agent understanding:
- No single agent understands full market dynamics
- Price formation emerges from hundreds of individual beliefs
- Political shifts result from invisible social network effects
- Population changes driven by forces no one controls

### Systemic Dread
**Dredge**: Player realizes they're part of something larger and indifferent

**Session 2**: Agents face systemic forces:
- Economic depressions emerge from collective behavior
- Political revolutions cascade through social networks
- No single agent can prevent market crashes
- Agents can only adapt, not control

---

## 5. Narrative Tension Through Information

### Gradual Revelation
**Dredge**: Player slowly understands the true nature of the archipelago

**Session 2**: Player gradually discovers agent lives:
- **Public Records**: Elections, market transactions, law changes
- **Gossip**: Private opinions, relationships, secrets
- **Direct Observation**: Behaviors, habits, patterns
- **Memory Inspection**: Agent perspectives, beliefs, histories

### Trust and Betrayal
**Dredge**: NPCs warn about night dangers (some trustworthy, some deceptive)

**Session 2**: Information reliability varies:
- **Gossip Degradation**: Each transmission loses accuracy
- **Biased Sources**: Friends tell flattering versions, enemies tell damaging ones
- **Conflicting Accounts**: Different agents remember same event differently
- **Verification**: Player must cross-reference multiple sources

---

## 6. Player Agency in Narrative Discovery

### Non-Linear Discovery
**Dredge**: Players explore islands in any order, piece together story

**Session 2**: Player chooses information gathering methods:
```
Information Discovery Channels:
1. Observation (free but limited)
2. Conversation (requires relationship)
3. Gossip (unreliable but broad)
4. Public Records (accurate but delayed)
5. Memory Inspection (complete but expensive/debug only)
```

### Emergent Narrative
**Dredge**: No authored story—player creates narrative through experience

**Session 2**: Stories emerge from agent interactions:
- **Rags to Riches**: Poor agent becomes wealthy through trading
- **Political Intrigue**: Faction manipulation, vote rigging, scandals
- **Forbidden Love**: Cross-faction relationships, family conflicts
- **Economic Drama**: Market crashes, price manipulation, trade wars

---

## 7. Atmospheric AI Behaviors

### Uncanny Valley of Predictability
**Dredge**: Normalcy makes abnormalities more disturbing

**Session 2**: Agents should feel almost human:
- Mostly predictable (follow routines, economic logic)
- Occasionally surprising (weighted random selection)
- Rarely disturbing (extreme trait combinations, unusual decisions)
- **Key**: Predictable enough to trust, surprising enough to be interesting

### Stress Response Patterns
**Dredge**: Character's boat becomes harder to control when panicked

**Session 2**: Agent behavior degrades under stress:
```
Low Stress (Calm):
- Rational economic decisions
- Long-term planning
- Social cooperation

Medium Stress (Concerned):
- Short-term focus
- Risk aversion increases
- Information gathering priority

High Stress (Panic):
- Impulsive decisions
- Susceptible to manipulation
- Relationship breakdowns

Extreme Stress (Breakdown):
- Irrational behavior
- Complete goal shifts
- Potential agent departure/migration
```

---

## 8. Session 2 Integration Points

### Direct Applications
✅ **Gossip System**: Information spreads with degradation (like Dredge's unreliable NPCs)
✅ **Economic Cycles**: Market creates day/night-like tension
✅ **Memory System**: Agents remember events differently (subjective reality)
✅ **Debug Tools**: Memory inspection reveals hidden information (like game files/logs)

### Innovations Beyond Dredge
✅ **Social Networks**: Information spreads through relationships (not just proximity)
✅ **Price Beliefs**: Economic information asymmetry creates trading tension
✅ **Personality Diversity**: 19 facets create believable individual variation
✅ **Political Systems**: Collective decision-making emergent from individual votes

---

## 9. Design Principles Summary

### From Dredge to Session 2

1. **Juxtaposition Creates Tension**
   - Dredge: Cozy + Horror
   - Session 2: Routine + Crisis, Individual + Systemic

2. **Information Asymmetry Drives Discovery**
   - Dredge: Limited visibility, fog mechanics
   - Session 2: Private memories, gossip degradation, price uncertainty

3. **Environmental Storytelling Over Exposition**
   - Dredge: Discover horror through environment
   - Session 2: Discover society through agent behavior, markets, records

4. **Player Agency in Narrative Construction**
   - Dredge: Choose exploration order, interpretation
   - Session 2: Choose information sources, which agents to follow

5. **Systemic Dread Over Scripted Scares**
   - Dredge: Ancient indifferent entities
   - Session 2: Emergent economic/political forces beyond any control

---

## References

1. **Dredge Launch Interview** - Black Salt Games (May 2023)
2. **"How Dredge Corrupts 'Cozy Fishing' Into 'Cosmic Horror'"** - Xbox Wire (March 2023)
3. **"Dredge invents and perfects the fishing-horror genre"** - Polygon (April 2023)
4. **"Turning players' worst fears against them in Dredge"** - GameDeveloper (October 2024)
5. **Dredge Game Wiki** - Community documentation of mechanics

---

*Research compiled for Session 2 AI System Design verification*  
*Key Insight: Economic agents need both belief formation AND gossip for realistic price discovery*  
*Last updated: January 31, 2026*
