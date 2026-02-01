# 06: Return Triggers

**Focus**: Compelling reasons for players to log in again and maintain engagement  

---

## Overview

This document defines the mechanics that create compelling reasons for players to return to the game. These triggers must balance engagement with ethical design - creating anticipation without manipulation.

---

## Why Log In Tomorrow?

### In-Progress Projects

Players return to complete ongoing work:

| Project Type | Return Trigger | Timeframe |
|--------------|----------------|-----------|
| Construction | Can't wait to see finished structure | Hours to days |
| Agriculture | Crops ready to harvest | Hours |
| Crafting Queue | Items complete and ready | Minutes to hours |
| Research | New technology unlocked | Days |
| Contracts | Deadlines approaching | Hours to days |

**Psychology**: Incomplete tasks create cognitive tension (Zeigarnik effect) that drives return.

### Commitments

Voluntary obligations that create healthy engagement:

**Economic Commitments**
- Store customers waiting for restock
- Contract fulfillment deadlines
- Market opportunities (time-limited)

**Political Commitments**
- Election participation
- Law proposal voting
- Campaign promises made

**Social Commitments**
- Meetings with other players
- Collaborative project milestones
- Community events

**Design Principle**: Commitments should be *chosen*, not imposed.

### Scheduled Events

Predictable events create appointment mechanics:

| Event Type | Frequency | Urgency Level |
|------------|-----------|---------------|
| Elections | Weekly | High |
| Town meetings | As needed | Medium |
| Market openings | Daily | Medium |
| Disaster warnings | Event-driven | Very High |

---

## FOMO (Fear of Missing Out)

### Creating Urgency Ethically

**Legitimate FOMO Sources**

| Source | Mechanic | Ethical? |
|--------|----------|----------|
| World evolution | World changes while offline | Yes - emergent |
| Scheduled events | Elections, disasters | Yes - predictable |
| Social dependencies | Friends need help | Yes - voluntary |
| Resource scarcity | Limited-time opportunities | Yes - natural |

**Implementation Guidelines**
- Provide advance warning when possible
- Allow catch-up for missed opportunities
- Don't punish absence, reward presence
- Make FOMO feel like opportunity, not obligation

### Balance Requirements

**Too Little FOMO**
- Players forget about the game
- No sense of ongoing world
- Disconnection from community

**Too Much FOMO**
- Anxiety and stress
- Burnout and churn
- Resentment toward game

**Sweet Spot**
- Anticipation and excitement
- "Just one more thing" feeling
- Natural engagement rhythm

---

## Obligation vs. Choice

### Healthy Obligations

These are chosen by the player:

- **Chosen contracts**: Voluntary economic commitments
- **Self-set projects**: Personal goals with deadlines
- **Social bonds**: Relationships with real people
- **Political positions**: Elected or appointed roles

### Unhealthy Obligations (Avoid)

These feel like chores:

- **Daily login rewards**: Punishment for missing days
- **Maintenance tasks**: Mandatory daily upkeep
- **Time-limited chores**: Artificial urgency
- **Punishment for absence**: Lost progress, missed exclusives

### Design Principle

```
Good: "I want to check on my crops"
Bad: "I have to log in or my crops die"

Good: "I should vote in the election"
Bad: "I must vote or lose citizenship"
```

---

## Return Trigger Types by Time Scale

### Immediate (Same Day)

| Trigger | Archetype | Implementation |
|---------|-----------|----------------|
| Crafting complete | All | Push notification |
| Contract accepted | Economist | In-game mail |
| Election results | Politician | Announcement |
| Friend online | Socializer | Notification |

### Daily

| Trigger | Archetype | Implementation |
|---------|-----------|----------------|
| Market reset | Economist | Daily price updates |
| Resource respawn | All | New gathering spots |
| Town events | Socializer | Scheduled activities |
| Meteor countdown | All | Visual reminder |

### Weekly

| Trigger | Archetype | Implementation |
|---------|-----------|----------------|
| Elections | Politician | 7-day cycle |
| Server events | All | Special weekends |
| Progress reports | All | Weekly summaries |
| Season changes | Environmentalist | World state shift |

---

## Notification Strategy

### Critical Notifications

**Trigger immediately with sound + popup:**
- Election results announced
- Contract deadlines (24h warning, then 1h)
- Disasters (meteor impact imminent)
- Direct messages from other players

### Important Notifications

**Show in sidebar, no sound:**
- Market price changes >20%
- Skill level ups
- Project completions by collaborators
- Law changes affecting player

### Background Notifications

**Log only, check at will:**
- Routine AI agent activities
- Minor economic shifts
- Weather changes
- General world news

---

## Offline Progression

### World Continues

The world evolves while players are offline:

**Natural Evolution**
- Resources regenerate
- AI agents continue activities
- Projects progress (if automated)
- Economy fluctuates

**Meaningful Change**
- Elections happen
- Laws pass or fail
- Environmental changes
- Social dynamics shift

### Catch-Up Mechanics

Ensure returning players aren't left behind:

| Time Away | Catch-Up Support |
|-----------|------------------|
| 1 day | Full summary on login |
| 1 week | Accelerated catch-up XP |
| 1 month | Welcome back quest chain |
| Long-term | Fresh start option |

---

## Retention Metrics

### Target Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Day 1 Retention | 60% | Return within 24h |
| Day 7 Retention | 30% | Return within 7 days |
| Day 30 Retention | 15% | Return within 30 days |
| Session Length | 45-90 min | Average per login |
| Sessions per week | 3-5 | Healthy engagement |

### Achieving Targets Ethically

**Do:**
- Create meaningful progression
- Build social connections
- Provide engaging content
- Respect player time

**Don't:**
- Use dark patterns
- Create artificial scarcity
- Punish absence
- Manipulate with guilt

---

## Technical Implementation

### Session 1: Infrastructure

- Push notification system
- Email summaries for inactive players
- Graceful state updates on login
- Efficient world state sync

### Session 2: AI Triggers

- AI agents can "message" players
- Dynamic event generation
- Personalized return reasons
- Relationship-based notifications

---

## Navigation

- [Session 3 Index](./[AGENTS-READ-FIRST]-index.md)
- [← 05: Progression Feel](./05-progression-feel.md)
- [→ 07: UI/UX Paths](./07-ui-ux-paths.md)
- [RESEARCH-INDEX.md](./RESEARCH-INDEX.md) - Research sources

---

## Cross-References

- **Behavioral Design**: See RESEARCH-INDEX.md (Nir Eyal - Hooked)
- **Ethical Design**: See RESEARCH-INDEX.md for ethical engagement resources
- **Notification Systems**: See [Session 1: Client Architecture](../session-1-technical-architecture/02-client-server-architecture.md)
