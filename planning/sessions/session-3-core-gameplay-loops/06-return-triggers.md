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

## Return Trigger Specifications

This section provides detailed technical specifications for all return trigger systems, including timing constants, notification rules, and implementation details.

### Notification System

#### Push Notification Triggers

```csharp
public class ReturnNotificationSystem {
    // When to send notifications to bring players back
    // Reference: TechnicalConstants.TICK_RATE = 20 TPS
    
    public void CheckNotificationTriggers(Player player) {
        // Only check if player is offline
        if (player.IsOnline) return;
        
        var offlineTime = DateTime.Now - player.LastLogout;
        
        // Check all trigger types
        CheckResourceCompletion(player, offlineTime);
        CheckSkillDecayWarning(player, offlineTime);
        CheckEconomicOpportunity(player, offlineTime);
        CheckSocialEvent(player, offlineTime);
        CheckThreatWarning(player, offlineTime);
        CheckDailyReset(player, offlineTime);
        CheckWeeklyEvent(player, offlineTime);
    }
}
```

#### Resource Completion Notifications

**Trigger**: Crop/Resource ready for harvest

**Timing Specifications**:

| Crop Type | Growth Time | Pre-Ready Notification | Ready Notification | Reminder |
|-----------|-------------|------------------------|-------------------|----------|
| Wheat | 48 hours | 30 minutes before | When fully ready | 2 hours after |
| Corn | 72 hours | 1 hour before | When fully ready | 4 hours after |
| Vegetables | 24 hours | 15 minutes before | When fully ready | 1 hour after |
| Trees | 7 days | 12 hours before | When fully ready | 24 hours after |

**Example Notifications**:

```
"Your wheat crop will be ready in 30 minutes!"
[Tap to open game]

"Your wheat is ready for harvest!"
"You have 3 crop fields ready"
```

**Priority**: Medium (not urgent but time-sensitive)  
**Cooldown**: 2 hours (TechnicalConstants.NOTIFICATION_COOLDOWN_CROP)  
**Max per day**: 3 notifications

#### Skill Decay Warnings

**Trigger**: Skills degrading from inactivity

**Mechanic**:
- Skills slowly decay if not used (1% per day after 7 days offline)
- Warning sent at 5% decay (12 days offline)
- Urgent at 10% decay (17 days offline)
- Critical at 20% decay (30 days offline - about to lose level)

**Reference**: `TechnicalConstants.SKILL_LEVELS_COUNT = 10`

**Timing Matrix**:

| Days Offline | Decay Level | Warning Type | Action Required |
|--------------|-------------|--------------|-----------------|
| 7-11 days | 0-5% | None | Normal decay |
| 12-16 days | 5-10% | First Warning | Log in within 5 days |
| 17-29 days | 10-20% | Urgent Warning | Log in within 3 days |
| 30+ days | 20%+ | Critical Warning | Log in within 24 hours |

**Example Notifications**:

```
"Your Gathering skill is declining from inactivity."
"Log in within 3 days to prevent skill loss!"

⚠️ CRITICAL: "Your Carpentry skill is about to degrade!"
"Log in today to preserve your level 7 skill!"
```

**Priority**: High (permanent loss possible)  
**Cooldown**: 24 hours between warnings  
**Max per day**: 1 notification per skill

#### Economic Opportunities

**Trigger**: Market conditions favorable for player

**Scenarios**:

1. **High demand for player's specialty**
   ```
   "Wood prices are up 40%! Great time to sell!"
   [Current: 8 Cr/unit | Peak: 11.2 Cr/unit]
   ```

2. **Store inventory sold out**
   ```
   "Your store sold out! Restock to earn more!"
   "15 items sold while you were away"
   ```

3. **Contract available matching skills**
   ```
   "A contract for skilled carpenters is available!"
   "Reward: 500 Cr + reputation boost"
   ```

4. **AI buying at premium prices**
   ```
   "Merchants are paying premium for iron tools!"
   "Demand increased 60% in the last hour"
   ```

**Timing**: Real-time when opportunity arises  
**Frequency**: Max 1 per 4 hours (avoid spam)  
**Priority**: Medium-High  
**Cooldown**: 4 hours  
**Reference**: `TechnicalConstants.PRICE_DAY1_WOOD_MIN = 3.0f` to `PRICE_DAY30_WOOD_MAX = 3.0f`

#### Social Events

**Trigger**: Social activities involving player

**Scenarios**:

1. **Town meeting scheduled**
   ```
   "Town meeting tonight at 8 PM. Your vote matters!"
   "3 proposals up for discussion"
   ```

2. **Election voting open**
   ```
   "Voting is open! Cast your vote for town council."
   "Ends in 24 hours"
   ```
   Reference: `TechnicalConstants.VOTE_DURATION_HOURS = 24.0f`

3. **Friend request received**
   ```
   "Martha wants to be your friend!"
   "View profile and respond"
   ```

4. **Guild/Group activity**
   ```
   "Your building cooperative is meeting now!"
   "5 members online"
   ```

5. **Message received**
   ```
   "You have 3 unread messages"
   "From: Martha, John, Sarah"
   ```

**Timing**: 1-2 hours before event (enough time to login)  
**Priority**: Medium  
**Cooldown**: 6 hours  
**Reference**: `TechnicalConstants.AGENT_MAX_FRIENDS = 16`

#### Threat Warnings

**Trigger**: World threats requiring attention

**Scenarios**:

1. **Meteor approaching (Eco-style)**
   ```
   ⚠️ METEOR WARNING: Impact in 5 days!
   "Your help is needed to prepare defenses!"
   ```
   Reference: `TechnicalConstants.DAY_METEOR_IMPACT = 30`, `DAYS_METEOR_PREP_TIME = 10`

2. **Environmental disaster**
   ```
   "Pollution levels critical! Action needed!"
   Current: 650 ppm (Critical threshold: 600 ppm)
   ```
   Reference: `TechnicalConstants.POLLUTION_CRITICAL_MIN = 600.0f`

3. **Ecosystem collapse risk**
   ```
   "Forest nearly depleted - planting needed!"
   "Species at risk of extinction"
   ```
   Reference: `TechnicalConstants.SPECIES_EXTINCTION_THRESHOLD = 2`

**Timing Schedule**:

| Days Until Impact | Notification Frequency | Urgency Level |
|-------------------|------------------------|---------------|
| 7 days | Daily | High |
| 3 days | Every 12 hours | Very High |
| 1 day | Every 4 hours | Critical |
| 6 hours | Hourly | Emergency |
| 1 hour | Every 15 minutes | FINAL WARNING |

**Priority**: CRITICAL (world-ending consequences)  
**Cooldown**: 1 hour (escalating frequency as event approaches)  
**Sound**: Distinctive alert tone

### Daily Reset Mechanics

**Reset Time**: 00:00 UTC (or player's local midnight)

**What Resets**:

| System | Reset Behavior | Reference |
|--------|----------------|-----------|
| ✓ Daily contract board | New contracts generated | Session 3 - Economic System |
| ✓ Daily login bonus | Credits awarded if present | STARTING_CREDITS_PLAYER = 100 |
| ✓ Store visitor count | AI agent visitor counter resets | Economic simulation |
| ✓ Resource regeneration | Some resources respawn | Ecosystem cycle |
| ✓ Vote cooldowns | Voting eligibility resets | VOTE_DURATION_HOURS = 24 |
| ✓ Daily action limits | Crafting/building limits reset | Gameplay balance |

**What Persists**:

| System | Persistence Rule |
|--------|-----------------|
| ✓ Inventory | All items remain |
| ✓ Buildings | Structures unchanged |
| ✓ Skills | Levels and XP retained |
| ✓ Credits | Currency balance |
| ✓ Reputation | Social standing |
| ✓ Ongoing projects | Construction continues |
| ✓ Crop growth | Plants keep growing |

**Notification**:
```
"Daily reset! New contracts available!"
Sent: 00:05 UTC (5 minutes after reset)
Only to: Players who haven't logged in that day
```

**Priority**: Medium  
**Cooldown**: 20 hours (prevents duplicate notifications)

### Offline Progression System

```csharp
public class OfflineProgression {
    // What happens while player is offline
    // Reference: TechnicalConstants.TICKS_PER_DAY = 1728000
    
    public OfflineResults CalculateOfflineProgress(Player player, TimeSpan offlineDuration) {
        var results = new OfflineResults();
        
        // Crops continue growing (up to maturity)
        foreach (var crop in player.Crops) {
            float growth = CalculateGrowth(crop, offlineDuration);
            crop.GrowthProgress += growth;
            results.CropsGrown.Add(crop);
        }
        
        // Passive income (if applicable)
        if (player.HasPassiveIncomeSource) {
            results.PassiveIncome = CalculatePassiveIncome(player, offlineDuration);
        }
        
        // Store sales (if player owns store)
        if (player.OwnsStore) {
            results.StoreSales = SimulateStoreActivity(player, offlineDuration);
        }
        
        // AI world changes
        results.WorldChanges = GetRelevantWorldChanges(player, offlineDuration);
        
        // Skill decay (slow)
        if (offlineDuration > TimeSpan.FromDays(7)) {
            results.SkillDecay = CalculateSkillDecay(player, offlineDuration);
        }
        
        return results;
    }
    
    private int SimulateStoreActivity(Player player, TimeSpan duration) {
        // Simulate AI agents buying from player's store
        // Based on:
        // - Store location
        // - Item prices (Reference: PRICE_DAY7 range)
        // - Agent population (Reference: AGENTS_MVP = 25)
        // - Time of day
        
        float salesPerHour = EstimateStoreTraffic(player);
        float hoursOffline = (float)duration.TotalHours;
        
        // Cap at 24 hours (agents don't buy infinitely)
        hoursOffline = Math.Min(hoursOffline, 24);
        
        int estimatedSales = (int)(salesPerHour * hoursOffline);
        return estimatedSales;
    }
    
    private float CalculateSkillDecay(Player player, TimeSpan duration) {
        // Decay starts after 7 days
        // 1% per day after threshold
        if (duration.TotalDays <= 7) return 0;
        
        float decayDays = (float)duration.TotalDays - 7;
        return decayDays * 0.01f; // 1% per day
    }
}
```

**Offline Activity Caps**:

| Activity | Maximum Offline Processing | Rationale |
|----------|---------------------------|-----------|
| Crop growth | Up to maturity only | Prevents infinite growth |
| Store sales | 24 hours max | AI agents have limited shopping time |
| Passive income | 48 hours max | Prevents infinite wealth accumulation |
| Skill decay | No cap | Continues indefinitely |

### Return Bonus System

**Login Streak Rewards**:

| Day | Base Credits | Bonus Item | Total Value |
|-----|--------------|------------|-------------|
| 1 | 10 Cr | - | 10 Cr |
| 2 | 20 Cr | - | 20 Cr |
| 3 | 30 Cr | 1 random resource | ~35 Cr |
| 4 | 40 Cr | - | 40 Cr |
| 5 | 50 Cr | Tool repair kit | ~65 Cr |
| 6 | 60 Cr | - | 60 Cr |
| 7 | 100 Cr | Rare material | ~150 Cr |
| **Weekly Total** | **310 Cr** | **Varies** | **~400 Cr** |

**Streak Rules**:
- Resets if player misses a day
- Resets at weekly boundary (Day 7 → Day 1)
- No punishment for missing - just lose streak progress

**Return After Absence**:

| Absence Duration | Welcome Bonus | Additional Rewards | XP Multiplier |
|------------------|---------------|-------------------|---------------|
| 1 day | Normal | - | 1.0x |
| 2-3 days | 50 Cr | Basic supplies (wood, stone) | 1.0x |
| 4-7 days | 100 Cr | Supplies + tool repair kit | 1.2x |
| 8-14 days | 200 Cr | Supplies + rare material | 1.5x |
| 15+ days | 300 Cr | Supplies + XP boost item | 2.0x (24h) |

**Reference**: `TechnicalConstants.STARTING_CREDITS_PLAYER = 100.0f`

### Commitment System

```csharp
public class PlayerCommitment {
    // Players can set goals to return for
    
    public void SetCommitment(Player player, CommitmentType type, TimeSpan duration) {
        var commitment = new Commitment {
            Type = type,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now + duration,
            Reward = CalculateReward(type, duration)
        };
        
        player.ActiveCommitments.Add(commitment);
        
        // Schedule reminder notification
        ScheduleReminder(player, commitment.EndTime - TimeSpan.FromHours(1));
    }
    
    private Reward CalculateReward(CommitmentType type, TimeSpan duration) {
        // Longer commitments = bigger rewards
        int hours = (int)duration.TotalHours;
        
        return type switch {
            CommitmentType.DailyLogin => new Reward { 
                Credits = 10 * hours 
            },
            CommitmentType.ProjectCompletion => new Reward { 
                Credits = 50 * hours, 
                XPBoost = 1.2f 
            },
            CommitmentType.SkillGoal => new Reward { 
                Credits = 30 * hours, 
                Item = "SkillBook" 
            },
            CommitmentType.EconomicGoal => new Reward { 
                Credits = 100 * hours 
            },
            _ => new Reward { Credits = 10 * hours }
        };
    }
}

public enum CommitmentType {
    DailyLogin,         // "I commit to logging in daily"
    ProjectCompletion,  // "Build the town hall in 3 days"
    SkillGoal,          // "Reach level 5 in 2 days"
    EconomicGoal        // "Earn 1000 Cr this week"
}
```

**Commitment Examples**:

| Commitment | Duration | Reward | Reminder |
|------------|----------|--------|----------|
| "Log in daily for 7 days" | 7 days | 70 Cr + streak bonus | Daily at 20:00 |
| "Build town hall" | 3 days | 150 Cr + reputation | 1 hour before deadline |
| "Reach Carpentry L5" | 5 days | 150 Cr + skill book | Progress updates daily |
| "Earn 1000 Cr" | 7 days | 700 Cr | Mid-week progress check |

### Notification Frequency Limits

```csharp
public class NotificationLimiter {
    // Prevent notification spam
    
    private Dictionary<string, DateTime> _lastNotification = new();
    
    public bool CanSendNotification(Player player, string notificationType) {
        var key = $"{player.Id}:{notificationType}";
        
        if (!_lastNotification.TryGetValue(key, out var lastSent)) {
            _lastNotification[key] = DateTime.Now;
            return true;
        }
        
        var cooldown = GetCooldown(notificationType);
        if (DateTime.Now - lastSent > cooldown) {
            _lastNotification[key] = DateTime.Now;
            return true;
        }
        
        return false;
    }
    
    private TimeSpan GetCooldown(string type) {
        return type switch {
            "crop_ready" => TimeSpan.FromHours(2),
            "market_opportunity" => TimeSpan.FromHours(4),
            "social_event" => TimeSpan.FromHours(6),
            "threat_warning" => TimeSpan.FromHours(1), // Urgent = shorter cooldown
            "skill_decay" => TimeSpan.FromHours(24),
            "daily_reset" => TimeSpan.FromHours(20),
            "election_reminder" => TimeSpan.FromHours(12),
            "message_received" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(12)
        };
    }
    
    public int GetDailyLimit(string type) {
        return type switch {
            "crop_ready" => 3,
            "market_opportunity" => 2,
            "social_event" => 2,
            "threat_warning" => 10, // Unlimited for critical threats
            "skill_decay" => 1,
            "daily_reset" => 1,
            _ => 5
        };
    }
}
```

**Daily Notification Caps**:

| Notification Type | Max Per Day | Per Hour Cap |
|-------------------|-------------|--------------|
| Crop ready | 3 | 1 per 2h |
| Market opportunities | 2 | 1 per 4h |
| Social events | 2 | 1 per 6h |
| Threat warnings | Unlimited | 1 per 1h |
| Skill decay | 1 | 1 per 24h |
| Daily reset | 1 | 1 per 20h |
| **TOTAL MAX** | **10** | - |

### Deep Linking

All notifications include deep links for immediate action:

```csharp
public class DeepLinkBuilder {
    public string BuildDeepLink(string action, Dictionary<string, string> parameters) {
        var baseUrl = "societies://";
        var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{baseUrl}{action}?{query}";
    }
}
```

**Deep Link Examples**:

| Notification | Deep Link | Opens Game At |
|--------------|-----------|---------------|
| "Your wheat is ready!" | `societies://harvest?cropId=123&field=4` | Farm, field 4 |
| "Town meeting in 1 hour" | `societies://event?type=town_meeting&location=town_hall` | Town hall |
| "Your store sold out" | `societies://store?action=restock&id=456` | Store inventory |
| "Martha sent a message" | `societies://chat?agentId=789&focus=true` | Chat with Martha |
| "Meteor impact imminent" | `societies://threat?type=meteor&countdown=3600` | Defense planning |
| "Contract available" | `societies://contracts?filter=available&id=321` | Contract board |

**Fallback Behavior**:
- If deep link fails, open game to main menu
- If specific location unavailable (destroyed/relocated), show notification "Location no longer available"
- Track deep link success rate for analytics

### Opt-Out Settings

Players can customize all notification preferences:

```csharp
public class NotificationPreferences {
    public bool CropReadyAlerts { get; set; } = true;
    public bool MarketOpportunities { get; set; } = true;
    public bool SkillDecayWarnings { get; set; } = false; // Default off
    public bool SocialEvents { get; set; } = true;
    public bool ThreatWarnings { get; set; } = true; // Cannot fully disable
    public bool DailyReset { get; set; } = true;
    public bool PoliticalUpdates { get; set; } = false; // Default off
    
    // Quiet hours (no notifications)
    public TimeSpan QuietHoursStart { get; set; } = TimeSpan.FromHours(22); // 22:00
    public TimeSpan QuietHoursEnd { get; set; } = TimeSpan.FromHours(8);    // 08:00
    
    // Daily limit override
    public int MaxNotificationsPerDay { get; set; } = 5;
    
    // Priority override - always notify regardless of settings
    public bool AlwaysNotifyCritical { get; set; } = true; // Meteor, skill loss imminent
}
```

**UI Settings Panel**:

```
NOTIFICATION SETTINGS

[✓] Crop ready alerts
    └─ Remind me: [30 min before] [When ready] [2h after]

[✓] Market opportunities
    └─ Minimum price change: [20%] to notify

[ ] Skill decay warnings
    └─ Only notify at: [Critical levels only ✓]

[✓] Social events
    └─ [✓] Town meetings  [ ] Friend requests  [✓] Messages

[✓] Threat warnings
    └─ [Cannot disable critical world threats]

[✓] Daily reset
    └─ Send at: [My local midnight] [00:00 UTC]

[ ] Political updates
    └─ [✓] Election results only

─────────────────────────────
QUIET HOURS: 22:00 - 08:00
[No notifications during these hours except critical threats]

DAILY LIMIT: Max [5] notifications per day
[✓] Bundle multiple notifications when possible

[Reset to Defaults]  [Save Changes]
```

**Non-Disableable Notifications**:
- Critical threat warnings (meteor impact < 24h)
- Skill level loss imminent (< 24h)
- Account security alerts

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
- **Technical Constants**: `planning/meta/technical-constants.md`
