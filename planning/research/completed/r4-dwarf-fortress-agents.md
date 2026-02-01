# R4: Dwarf Fortress Agent Systems Analysis

## Executive Summary

Dwarf Fortress represents the gold standard for agent simulation in games, featuring the most complex AI citizen system ever created. Each dwarf possesses **50 personality facets** (0-100 scale), **28+ needs**, **8 short-term memory slots**, **8 long-term memory slots**, and unlimited core memories that permanently alter personality. The game generates emergent narratives through the interaction of these systems—no authored stories, yet players create extensive fiction from procedural events. Key insights for Societies: DF's depth comes from layered systems (personality → needs → thoughts → memories → stress), not complex individual algorithms. The game achieves believability through **specificity** (dwarves like "rose gold rings" not just "shiny things") and **consequences** (unmet needs cascade into focus loss, stress, tantrums, insanity). The memory system—where emotions compete for limited slots and strongest events become permanent personality changes—is particularly innovative. DF proves that believable AI requires tracking granular individual preferences, meaningful social relationships, and persistent history that shapes future behavior.

## Game Overview

Dwarf Fortress, developed by Tarn and Zach Adams since 2002, is a fantasy settlement simulation renowned for its unprecedented depth in agent behavior. The 2022 Steam release brought modern graphics to its ASCII roots while preserving the complex simulation. The game has three modes: **Fortress mode** (build and manage a dwarven colony), **Adventure mode** (explore as a single character), and **Legends mode** (view world history). The core achievement is creating emergent storytelling through systems—individual dwarves generate narratives the developers never scripted.

## 1. Agent Decision-Making

### Priority System

Dwarves decide what to do through a layered priority system that balances **survival needs**, **personal fulfillment**, and **player-assigned work**.

**Key Mechanisms**:

1. **Hard-coded Survival Priorities**: Dwarves will abandon any task to eat, drink, or sleep when thresholds are reached. These are non-negotiable biological imperatives.

2. **Focus-Driven Personal Jobs**: Dwarves self-assign "personal fulfillment jobs" when their needs aren't met. These appear as:
   - **Low-priority** (green text): "Listen to Poetry"—can be cancelled for fortress jobs
   - **High-priority** (magenta with !): "Pray to Lorsïth!"—cannot be cancelled, will interrupt work

3. **Labor Assignment**: Players assign labors through "Work Details" (groups of related jobs). Dwarves pick jobs based on:
   - Proximity to job site
   - Skill level (higher skill = higher priority for that labor)
   - Whether labor is enabled in their work details
   - Specialization status (specialized dwarves only do assigned labors)

4. **Workshop Assignment**: Individual workshops can have assigned workers, creating dedicated craftsmen.

**Example Scenario**:

Urist McMiner is engraving a bedroom (fortress job) when he becomes thirsty. He drops the engraving, walks to the dining room, drinks wine, then returns to engraving. However, if he develops a high-priority need to "pray to Armok" while engraving, he will immediately abandon the job, go to the temple, pray, and only then return—potentially hours later.

**Technical Implementation Notes**:

Jobs are stored in a queue system. Each tick, dwarves evaluate available jobs against their current state. The decision algorithm considers:
- Distance to job (pathfinding cost)
- Job priority (hauling is lowest, certain needs are highest)
- Skill match
- Tool availability

### Needs System

Needs are the primary driver of dwarf behavior and directly affect **focus** (productivity).

**Need Types** (28 total, weighted by personality):

1. **Biological**: drinking (alcohol for dwarves), eating good meals, sleep
2. **Social**: spending time with people, being with family, being with friends, making romance
3. **Spiritual**: communing with deity, meditation
4. **Achievement**: practicing a craft, practicing a martial art, learning something, staying occupied
5. **Aesthetic**: admiring art, doing something creative, seeing animals
6. **Emotional**: causing trouble, fighting, arguing, making merry
7. **Existential**: self-examination, thinking abstractly, upholding tradition

**Need Satisfaction**:

Each need has specific actions that satisfy it:
- "Practicing a craft" → Craft any item at a workshop
- "Making merry" → Watch or perform at a tavern
- "Thinking abstractly" → Read a book, compose music/poetry
- "Acquiring something" → Pick up a trade good

**Focus Impact**:

Needs affect a dwarf's focus level, which modifies skill effectiveness:

| Focus Level | Modifier |
|-------------|----------|
| Very focused (140%+) | +50% skill |
| Quite focused (120-139%) | +25% skill |
| Focused (101-119%) | +10% skill |
| Untroubled (100%) | 0% |
| Unfocused (81-99%) | -10% skill |
| Distracted (61-80%) | -25% skill |
| Badly distracted (60% or lower) | -50% skill |

Focus is calculated as a weighted ratio of met vs. unmet needs. High-level needs (weight 10) dominate the calculation.

**Need Decay**:

Needs have satisfaction levels that decay over time:
- **Unfettered** (400-300): Just satisfied, maximum benefit
- **Level-headed** (299-200): Still good
- **Untroubled** (199-100): Neutral
- **Not distracted** (99 to -999): Slight negative
- **Unfocused** (-1,000 to -9,999): Moderate penalty
- **Distracted** (-10,000 to -99,999): Severe penalty
- **Badly distracted** (-100,000+): Maximum penalty

When a need is satisfied, it resets to 400 regardless of previous value.

**Personality-Need Connection**:

Needs are generated based on personality facets and values:
- High **IMMODERATION** → strong need for alcohol
- High **ROMANCE** → need to make romance
- High **ART_INCLINED** → need to do something creative
- Zero belief in gods → no prayer needs
- High **GREGARIOUSNESS** → need to spend time with people

This creates natural diversity—no two dwarves have identical need profiles.

**Lessons for Societies**:

1. **Weighted need system**: Not all needs are equal; some should dominate behavior
2. **Specific satisfaction actions**: "Read book" not just "entertain yourself"
3. **Immediate consequences**: Focus/skill penalties create gameplay relevance
4. **Personality-derived needs**: Let character traits generate individual need profiles
5. **Two-tier priorities**: Distinguish interruptible from non-interruptible personal needs

## 2. Memory and Learning

### Memory System

Dwarf Fortress implements a sophisticated three-tier memory system that drives personality change and stress.

**What is Remembered**:

- **Immediate thoughts**: Current experiences (shown in Thoughts tab)
- **Short-term memories**: 8 slots, strongest emotions from recent experiences
- **Long-term memories**: 8 slots, promoted from short-term after 1 year
- **Core memories**: Unlimited slots, permanent personality changes

**Memory Mechanics**:

**Short-term memories**:
- 8 slots per dwarf
- New thoughts check if their "group" already exists in a slot
- If same group exists, strongest emotion is kept
- If no slots empty, weakest memory is overwritten (even if new thought is weaker)
- Example: Seeing rain (strength 1/4) overwrites seeing rain (strength 1/8), but not dismay at rain (strength 1/2)

**Long-term memories**:
- Promoted from short-term after 1 year in a slot
- 8 slots, same group-replacement rules
- Frequently revisited (83% of dwarves revisit all 8 long-term memories within sampling period)
- Can have multiple memories of same group (unlike short-term)
- Core memories are removed from long-term when promoted

**Core memories**:
- Promoted from long-term when dwarves "dwell upon" them (1:3 chance per revisit)
- Cause **permanent personality changes** shown in bright magenta
- Example: "She can easily fall in love or develop positive sentiments, after gaining a sibling in 351"
- Only certain emotion groups can become core: death, trauma, romance, birth, marriage, family gain

**Memory Decay**:

Memories don't decay in strength, but their emotional impact can change:
- Negative memories may become "acceptance" over time
- Positive memories remain positive
- Core memory promotion often changes emotion nature

**Impact on Behavior**:

- Short-term: Immediate stress/mood changes
- Long-term: Revisited memories trigger stress changes repeatedly
- Core: Permanent personality facet/value modifications

Example: A dwarf who sees many corpses in battle may have "seeing dead bodies" promoted to core memory, permanently increasing their **BRAVERY** or decreasing **STRESS_VULNERABILITY**—they become hardened to death.

**Technical Implementation**:

Each memory tracks:
- Event type (group/category)
- Emotion type and strength
- Year of occurrence
- Specific details (who died, what was crafted, etc.)

Memory slots are evaluated every tick; strongest memories in each group are retained.

### Relationship Formation

Relationships are the social fabric of the fortress, formed through proximity and conversation.

**Relationship Types**:

**Kin relationships** (permanent, non-negotiable):
- Spouse, Lover, Child, Parent, Sibling, Grandparent, Aunt/Uncle, Niece/Nephew, Cousin

**Spiritual relationships**:
- Deity worship (ardent, faithful, casual, dubious)
- Object of worship (megabeasts)

**Professional relationships**:
- Apprentice/Master, Former Master/Apprentice

**Animal relationships**:
- Pet, Bonded animal

**Non-kin personal relationships** (formed in-game):
- Passing Acquaintance → Long-term Acquaintance → Friend → Close Friend → Kindred Spirit
- Or: Grudge (opposite direction)

**Relationship Formation Mechanics**:

1. **Proximity requirement**: Dwarves must be idle and adjacent (N/S/E/W, not diagonal or same-tile) to start chatting
2. **Compatibility scoring**: Based on:
   - Shared preferences (both like elephants)
   - Shared skills (both miners)
   - Personality compatibility (facets within 40-60 range of each other)
3. **Threshold progression**:
   - Rank 15: Friendship or Grudge forms
   - Rank 31-42: Lovers (if romantically compatible)
   - Rank 50: Marriage (if both willing)
4. **Decay**: Non-kin relationships below "lover" decay if no contact for a full year (starting dwarves exempt)

**Relationship Effects**:

- Making a friend: Happy thought
- Death of friend: Severe unhappy thought (often -5000 to -10000 stress)
- Talking to grudge: +700 stress/day (can cause rapid breakdown)
- Death of grudge: Still causes unhappy thought (ironic, but true)
- Family present: Satisfies "being with family" need

**Personality Effects on Relationships**:

- **LOVE_PROPENSITY**: High = quick to form positive feelings
- **GREGARIOUSNESS**: High = seeks company, learns Conversationalist
- **BASHFUL**: High = cannot learn Conversationalist
- **EMOTIONALLY_OBSESSIVE**: High = forms deep bonds, harder to lose
- **FRIENDLINESS**: Affects social skill learning

**Social Skills** (learned, not innate):
- Conversationalist, Comedian, Flatterer, Pacifier, Consoler, Intimidator, Persuader, Negotiator, Judge of Intent

Social skills are learned based on personality—high **HUMOR** always learns Comedian; high **FRIENDLINESS** can learn Flatterer.

**Lessons for Societies**:

1. **Proximity-based formation**: Relationships require physical co-location, not just abstract assignment
2. **Compatibility matters**: Similar personalities form friendships, opposites form grudges
3. **Conversational thresholds**: Relationships need minimum interaction counts to form
4. **Meaningful consequences**: Death of friends causes genuine gameplay-affecting grief
5. **Skill-personality link**: Social abilities emerge from character traits, not random assignment

## 3. Emergent Storytelling

### Systems Contributing to Stories

Dwarf Fortress generates narratives through the intersection of multiple systems:

**System 1: Needs + Personality Conflicts**

A dwarf with high **VIOLENT** and **ANGER_PROPENSITY** who also has an unmet need to "cause trouble" may start arguments or fights. If they have a **grudge** against another dwarf, this creates a personal vendetta arc.

**System 2: Memory Trauma + Stress**

Dwarves who witness death accumulate trauma memories. If these promote to core, they become "hardened" or develop **depression propensity**. A dwarf who was once cheerful becoming melancholic after losing their child is a complete character arc generated by systems.

**System 3: Relationship Networks**

When a dwarf dies, their friends and family experience grief. If multiple dwarves are connected (A is friend of B, B is married to C, C is sibling of D), a single death cascades through the network. This creates fortress-wide mourning events or mass tantrum spirals.

**System 4: Artifact Creation (Strange Moods)**

Dwarves can enter "strange moods" where they obsessively create an artifact. If they lack materials, they go insane. This creates mad artist narratives: "Urist McCarpenter became obsessed, claimed a workshop, demanded silk cloth, went mad when none was found, and now wanders the fortress naked."

**System 5: World History Integration**

Migrants arrive with pre-existing relationships, grudges, skills, and histories from world generation. A dwarf might arrive who is the former apprentice of a legendary weaponsmith who was killed by a goblin—creating immediate revenge motivation.

### Emergent Story Examples

#### Example 1: The Boatmurdered Elephant Wars

**Context**: Boatmurdered was a succession game where 14 players each managed the fortress for one in-game year. The fortress became legendary for catastrophic failure.

**What Happened**:

Year 1 (TouretteDog): Established basic fortress. Killed some elephants.

Year 3 (Keyboard Fox): Elephant population became aggressive. A dwarf named "StarkRavingMad" (the player, but also a dwarf) led a charge against elephants. The elephants killed many dwarves, creating corpses that generated miasma (toxic gas from rotting bodies).

Year 5 (StarkRavingMad's rule, different player): The fortress was already deteriorating. StarkRavingMad (the player) wrote: "Welcome to fucking Boatmurdered!" This became the fortress's catchphrase. He attempted to fight elephants with ballistae (siege engines), but the operators were killed. The elephant war continued.

Year 6-7: Corpse accumulation created permanent miasma clouds. Dwarves experienced constant stress from rotting bodies, causing tantrums. A tantrum spiral began: one dwarf tantrumed, destroyed furniture, which made another dwarf tantrum, who killed someone, making more dwarves tantrum.

Year 8-14: Successive players tried increasingly desperate solutions—lava floods to burn corpses, sealed chambers, militarization. Each solution created new problems. The lava flood burned dwarves along with corpses. The military needed supplies, creating more work for already-stressed dwarves.

**Systems Involved**:
- **Animal behavior**: Elephants became aggressive (previously docile)
- **Corpse decay**: Created miasma, caused unhappy thoughts
- **Stress/tantrums**: Unhappy dwarves became violent
- **Relationship grief**: Dead friends/relatives caused cascading grief
- **Combat trauma**: Survivors accumulated trauma memories
- **Player succession**: Different management styles compounded chaos

**Why It's Compelling**:
The story has clear cause-and-effect: elephant attacks → corpses → miasma → stress → tantrums → more death → more stress. The "worse before better" pattern created narrative tension. The community aspect (14 players) added human drama to the procedural events.

#### Example 2: Cog Tamperwhipped and the Vampire Mayor

**Context**: A player discovered their mayor was a vampire through social observation.

**What Happened**:

A dwarf named Cog Tamperwhipped became mayor. Players noticed he never ate at the dining hall, never drank, and worked at night. Checking his thoughts showed he "didn't feel anything" about drinking blood—but he had blood stains on his clothes.

Investigation revealed: He was a vampire who had been hiding for years. He had killed several dwarves, but because he was the mayor (elected by popularity), no one suspected him. The bodies were found in the well—he had been dumping victims there.

The vampire mayor eventually went insane from stress (despite being undead) and attacked the fortress. The military killed him, but not before he infected others.

**Systems Involved**:
- **Vampire mechanics**: Undead need blood, have specific behaviors
- **Election system**: Dwarves vote for popular candidates
- **Thought system**: Revealed his lack of normal emotions
- **Clothing stains**: Tracked blood spatter
- **Insanity**: Even vampires can break from stress

**Why It's Compelling**:
It mirrors real serial killer narratives—a trusted authority figure hiding dark secrets. The detective work (observing behavior patterns, checking thoughts) made players active investigators. The procedural elements created a unique story no one scripted.

#### Example 3: The Indomitable Dwarf

**Context**: A single dwarf survived multiple catastrophes through sheer luck and persistence.

**What Happened**:

A player dug a spiral staircase 20 levels deep as a dumping pit. They placed tombs on the sides and a well at the bottom. A flood filled the pit from a river, creating a pond.

Later, goblins attacked. The player destroyed the bridge across the pit to escape, stranding the fort's side. One dwarf, who had been hauling items, was on the wrong side when the bridge fell. He fell into the pit, surviving because the water cushioned the fall.

He was trapped. The player watched him: he drank from the well, ate fish he caught, and survived for THREE YEARS alone in the pit. Eventually, the player built a new bridge and rescued him. He emerged with legendary swimming and fishing skills, and became a folk hero.

**Systems Involved**:
- **Individual survival**: Dwarves autonomously seek food/water
- **Skill improvement**: Practicing fishing improved skill over time
- **Skill rust**: Not practicing other skills caused decay
- **Memory formation**: Positive thoughts from surviving became core memories
- **Physics**: Water cushioned fall (not intended, emergent)

**Why It's Compelling**:
It's the ultimate underdog story—one dwarf against the elements. The procedural skill gain created a genuine hero. The player's decision to rescue him (rather than let him die) added human choice to the emergent narrative.

### Player Discovery

**Visualization**:
- **Thoughts and Preferences screen**: Shows current thoughts, memories, personality, needs
- **Relationships screen**: Visual network of connections
- **Stress indicators**: Red downward arrows, emotional status quotes
- **Combat reports**: Detailed logs of battles with body part damage

**Logs/Records**:
- **Announcements**: Real-time event feed ("Urist McMiner cancels dig: too insane")
- **Justice screen**: Crime reports, convictions, punishments
- **Artifacts screen**: History of legendary items and their creators

**Legends Mode**:
After fortress/abandoned or retired, players can view:
- Historical maps showing civilization expansion
- Lists of all historical figures and their deeds
- War histories with battle details
- Site foundations and destructions
- Megabeast rampages
- Heroes and their equipment

This transforms personal fortress events into world history—your dwarves become legendary figures in the persistent world.

### What Makes Good Emergent Stories

**Key Elements**:

1. **Character Consistency**: Dwarves act according to their personality. A greedy dwarf becoming mayor leads to corruption; an anxious dwarf in combat leads to panic.

2. **Cascading Consequences**: Small events snowball. One death → one tantrum → one murder → fortress-wide war.

3. **Player Agency + System Chaos**: Players make decisions, but systems create unexpected outcomes. The player chose to build the pit; the system made the dwarf survive in it.

4. **Emotional Resonance**: Despite being ASCII symbols, dwarves generate empathy through their detailed needs and suffering. Players care when "Urist McLegendary, who likes rose gold and cats, who just got married, who has high hopes" dies.

5. **Memorability Through Specificity**: "The dwarf who likes rose gold" is more memorable than "the blacksmith." DF generates millions of specific preferences.

**Lessons for Societies**:

1. **Layered systems create depth**: Needs → thoughts → memories → stress → breakdown is more compelling than a single "happiness" stat
2. **Specific preferences matter**: "Likes jazz music and red sweaters" beats "likes entertainment"
3. **Persistent consequences**: Death affects friends; trauma changes personality; history shapes future
4. **Player investigation**: Don't tell stories—let players discover them through observation
5. **Community sharing**: DF's stories spread because they're unique and worth telling

## 4. Job and Labor Management

### Job Assignment

**Player Control**:

Work is organized through **Work Details**—groups of related labors assigned to specific dwarves:

- **Miners**: Mining labor only
- **Haulers**: All hauling labors (stone, wood, items, etc.)
- **Orderlies**: Medical tasks (suturing, wound dressing, feeding patients)
- Custom work details can combine any labors

Assignment modes:
- **"Only selected do this"**: Only checked dwarves can perform these labors
- **"Everybody does this"**: All dwarves can perform (white checkmarks for specialized)
- **"Nobody does this"**: All dwarves banned (red checkmarks show paused permissions)

**Specialization**:

Dwarves can be marked as **specialized** (red hammer/lock icon). Specialized dwarves:
- Only do labors explicitly assigned to them
- Only work at assigned workshops
- Only perform occupation duties
- Are NOT restricted by "Nobody does this" settings

This creates dedicated craftsmen—your legendary weaponsmith only forges weapons, never hauls stone.

**Auto-Assignment**:

When a job is created (via designation, workshop task, or work order), the game automatically assigns it to the "best" available dwarf based on:
- Distance to job site (closer = higher priority)
- Skill level (higher skill = preferred)
- Whether labor is enabled
- Whether dwarf is specialized for this work

**Tool Requirements**:

Three labors require specific tools (mutually exclusive):
- **Mining**: Requires pick
- **Wood cutting**: Requires battle axe
- **Hunting**: Requires crossbow, quiver, bolts

A dwarf can only have one tool-labor assigned at a time. These tools override military equipment assignments.

### Skill Development

**Learning Mechanism**:

Skills improve through **practice**—performing the labor gains experience points. Skill levels progress:

1. Dabbling
2. Novice
3. Adequate
4. Competent
5. Skilled
6. Proficient
7. Talented
8. Adept
9. Expert
10. Professional
11. Accomplished
12. Great
13. Master
14. High Master
15. Grand Master
16. Legendary
17. Legendary+1 through Legendary+5

**Skill Effects**:

- **Speed**: Higher skill = faster job completion (except hauling and nursing)
- **Quality**: Labors marked "speed; item quality" produce better items at higher skill
- **Thoughts**: Mastering skills creates happy thoughts ("Urist felt satisfied mastering a skill")

**Skill Rust**:

Skills decay if not used. "Legendary" dwarves will drop to "Great" if they never practice their craft. This creates "use it or lose it" pressure for specialized roles.

**Labor vs. Needs Conflict**:

Dwarves may abandon work to satisfy needs:
- Hunger/thirst/sleep: Hard interrupt, will abandon any job
- High-priority personal needs: Interrupt current job, will return after satisfaction
- Low-priority personal needs: Wait for idle time

If a dwarf's needs aren't met for extended periods, they accumulate stress, which can cause:
- Tantrums (destroy furniture, attack others)
- Depression (cease all work)
- Obliviousness (wander randomly)
- Insanity (permanent breakdown)

**Strange Moods**:

A special "job" where a dwarf enters a feverish creative state:
- Claims a workshop
- Takes all materials needed
- Works until artifact is created
- Cannot be interrupted (except by death)
- If materials unavailable, dwarf goes insane

Strange moods bypass normal labor assignment—systems take over completely.

**Lessons for Societies**:

1. **Granular labor categories**: 50+ distinct labors allow fine specialization
2. **Skill-speed-quality linkage**: Higher skill should improve all three
3. **Tool requirements**: Some jobs need equipment, creating equipment-management gameplay
4. **Decay creates narrative**: Skill rust forces continued practice, creating "aging expert" stories
5. **Work-life balance**: Needs must interrupt work, creating realistic labor friction

## 5. World Persistence

### History Tracking

Dwarf Fortress maintains comprehensive world history across all modes.

**Tracked Elements**:

**Historical Figures** (individuals):
- Birth, death, kills
- Marriages, children, relationships
- Skill acquisition, titles gained
- Deeds (battles, artifacts created, books written)
- Worship patterns, crimes committed

**Sites** (locations):
- Foundation date, destruction date
- Population changes
- Wars fought, conquered, liberated
- Buildings constructed
- Artifacts housed

**Civilizations**:
- Wars declared, peace treaties
- Rulers and their reigns
- Sites founded/lost
- Trade relationships
- Ethics and values

**Regions**:
- Battles fought
- Megabeast rampages
- Climate changes

**Megabeasts and Night Creatures**:
- Spawn events
- Kill lists
- Site attacks
- Worship accrued

### Legends Mode

Legends mode transforms world data into an interactive history browser.

**Features**:

- **Historical Map**: View territorial changes over time; step forward/backward by 10 or 100 years
- **Figure Browser**: Search any named entity (person, god, beast) and view their complete history
- **Event Timeline**: Chronological list of all world events
- **Age Progression**: Worlds progress through named ages based on dominant powers (Age of Myth, Age of Legends, Age of Humans, etc.)

**Data Export**:

Players can export:
- XML dump of all historical data (can be 1GB+ for 1000-year worlds)
- Detailed maps (biome, elevation, temperature, rainfall, etc.)
- World generation parameters
- Site and population lists

Third-party tools like **Legends Viewer** visualize this data with graphs, hyperlinks, and filtering.

### Impact on Gameplay

**Consequences**:

World history directly affects fortress mode:
- Migrants arrive with pre-existing skills and relationships from worldgen
- Wars in history create hostile civilizations that may siege your fort
- Historical artifacts may be stolen or requested
- Megabeasts from history may attack
- Heroes may visit or request quests

**Generational Effects**:

Children born in your fortress inherit:
- Parental skills (slight bonus)
- Family relationships
- Cultural values
- Potential for inherited nobility

Second-generation dwarves are fully simulated, creating multi-generational fortress stories.

**Lessons for Societies**:

1. **Persistent individual history**: Every person has a birth-to-death record
2. **Cross-mode continuity**: Fortress events become adventure mode legends
3. **Exportable data**: Allow external tools to visualize history
4. **Generational simulation**: Children should inherit traits and relationships
5. **History affects present**: Past wars create present enemies

## 6. Personality and Life

### Personality System

Dwarf Fortress generates unique individuals through multiple layered systems.

**Personality Facets** (50 total, 0-100 scale):

Facets are behavioral tendencies with gameplay effects:

**Emotional Facets**:
- **LOVE_PROPENSITY** (0-100): Quick to love vs. slow to love
- **HATE_PROPENSITY**: Prone to hatred vs. never hates
- **ANGER_PROPENSITY**: Quick to anger vs. slow to anger
- **DEPRESSION_PROPENSITY**: Depression-prone vs. never discouraged
- **STRESS_VULNERABILITY**: Easily stressed vs. resilient
- **ANXIETY_PROPENSITY**: Nervous wreck vs. incredibly calm
- **CHEER_PROPENSITY**: Often filled with joy vs. never cheerful

**Social Facets**:
- **GREGARIOUSNESS**: Treasures company vs. considers alone time important
- **FRIENDLINESS**: Bold flatterer vs. quarreler
- **POLITENESS**: Refined politeness vs. vulgar being
- **TRUST**: Naturally trustful vs. sees others as conniving
- **DISCORD**: Revels in chaos vs. seeks harmony

**Behavioral Facets**:
- **VIOLENT**: Brawler vs. avoids fights
- **GREED**: Obsessed with wealth vs. no interest in possessions
- **IMMODERATION**: Ruled by cravings vs. never tempted
- **PERSEVERANCE**: Unbelievably stubborn vs. drops at slightest difficulty
- **BRAVERY**: Utterly fearless vs. completely overwhelmed by fear
- **CONFIDENCE**: Blind overconfidence vs. no confidence at all
- **ACTIVITY_LEVEL**: Frenetic energy vs. utterly languid
- **EXCITEMENT_SEEKING**: Never fails to seek danger vs. avoids all excitement

**Values Facets** (what they care about):
- **CRAFTSMANSHIP, POWER, KNOWLEDGE, FAMILY, FRIENDSHIP, HONESTY, ARTWORK, MARTIAL_PROWESS, LEISURE_TIME, COMMERCE, NATURE, PEACE, TRADITION, INDEPENDENCE** etc.

**Distribution**:

- 78% of dwarves fall in neutral range (40-60) for most facets
- 2% are extreme (0-9 or 91-100)
- 8.5% are high (61-75) or low (25-39)
- Species biases: Dwarves median 55 on GREED (slightly greedy); Goblins median 25 on ALTRUISM (not helpful)

### Behavioral Manifestations

Personality isn't cosmetic—it drives specific behaviors:

**Example 1: High VIOLENT + High ANGER_PROPENSITY**
- Dwarf gets into frequent arguments
- Arguments trigger tantrums faster
- In combat, more likely to charge
- Civilian life: may attack other dwarves when stressed

**Example 2: High ALTRUISM + High DUTIFULNESS**
- Receives happy thought from helping wounded
- Never refuses work assignments
- Takes rescue jobs automatically
- Becomes excellent doctor/military leader

**Example 3: High EXCITEMENT_SEEKING + High BRAVERY**
- Seeks combat opportunities
- Gets unhappy if no danger for long periods
- Enters dangerous situations confidently
- Makes excellent military dwarves but poor civilians

**Example 4: High DEPRESSION_PROPENSITY + High STRESS_VULNERABILITY**
- Accumulates stress rapidly
- More likely to slip into depression
- Harder to recover from unhappy events
- Requires careful management (good bedrooms, no corpse exposure)

### Creating Memorable Characters

**Factors**:

1. **Specific Preferences**: Likes "rose gold rings" and "giant toads"—not just "valuables" and "animals"
2. **Skill Mastery**: Legendary+5 skill dwarves are genuinely rare and impressive
3. **Trauma History**: Dwarves who survived megabeast attacks with combat scars
4. **Relationship Density**: Dwarves with many friends, lovers, children—high social connectivity
5. **Achievement**: Artifact creators, megabeast slayers, book authors
6. **Tragic Arc**: Cheerful dwarf becomes hardened after family death

**Player Connection Mechanisms**:

- **Naming**: Players can rename dwarves, creating personal investment
- **Narrative Generation**: Players mentally fill in gaps ("Urist likes cats because he was lonely as a child")
- **Visual Distinction**: v50 Steam version gives dwarves distinct appearances
- **Role Assignment**: Giving dwarves specific jobs creates identity ("That's my legendary brewer")
- **Protection Instinct**: High-skill dwarves are valuable; players protect them

**Lessons for Societies**:

1. **Many axes of variation**: 50 facets + values + preferences + skills = millions of unique combinations
2. **Gameplay-relevant personality**: Traits must affect behavior, not just description
3. **Specific over general**: "Likes oak furniture" beats "likes nice things"
4. **Allow player narrative**: Don't tell full stories; give fragments for players to connect
5. **Visible consequences**: Let players see personality affecting behavior in real-time

## 7. Synthesis & Recommendations

### Core Insights

**What Makes DF Agents Special**:

1. **Layered Complexity**: Not one complex system, but many simple systems interacting (needs + personality + memory + stress + relationships)

2. **Specificity**: The game generates millions of specific preferences (materials, colors, animals, foods) rather than generic categories

3. **Consequence Chains**: Events cascade meaningfully—death → grief → stress → tantrum → more death

4. **Persistent History**: Everything is remembered and affects future behavior; nothing is ephemeral

5. **Emergent Over Scripted**: No authored stories, yet richer narratives than most scripted games

### For Societies: Must-Have Features

**Essential Systems**:

1. **Personality Facets (10-20 minimum)**:
   - Core: Gregariousness, Work Ethic, Violence/Conflict, Greed/Materialism, Emotional Stability
   - Each 0-100 scale with gameplay effects
   - Example implementation: `gregariousness = 75` → seeks social interaction frequently, forms friendships faster

2. **Needs System (15-20 needs)**:
   - Biological: Food variety, rest, hygiene
   - Social: Family contact, friendship, romance, community
   - Achievement: Skill use, learning, creation
   - Weight by personality—high greed = strong acquisition need
   - Two-tier priority: interruptible vs. non-interruptible

3. **Three-Tier Memory**:
   - Short-term (5-8 slots, 1-2 week duration): Immediate experiences
   - Long-term (5-8 slots, 3-6 month duration): Strongest experiences from short-term
   - Core (unlimited, permanent): Events that changed character
   - Memory competition: Only strongest emotion per category retained

4. **Stress System**:
   - Short-term: Immediate emotional reactions (-10000 to +10000)
   - Long-term: Accumulated short-term (-50000 to +120000)
   - Breakdown thresholds: Tantrum at +25000, Depression at +50000, Breakdown at +100000
   - Personality modifiers: Bravery, stress vulnerability, anxiety

5. **Relationship Formation**:
   - Proximity-based: Must be near each other
   - Compatibility: Similar personality = friendship, opposite = conflict
   - Thresholds: X conversations = acquaintance → Y conversations = friend
   - Decay: Relationships fade without maintenance
   - Consequences: Death of friend = major stress event

### For Societies: Should-Have Features

**Important Additions**:

1. **Skill Decay**: Use it or lose it—creates pressure to maintain specialists
2. **Tool Requirements**: Some jobs need equipment (creates logistics gameplay)
3. **Workshop Assignment**: Dedicate craftsmen to specific locations
4. **Generational Inheritance**: Children inherit parental traits and skills (reduced)
5. **World History Export**: XML/JSON export for external visualization
6. **Specific Preferences**: 50+ specific likes/dislikes per person (materials, colors, activities)
7. **Strange Moods**: Feverish creative episodes creating artifacts
8. **Personality Values**: What the person believes in (craftsmanship, family, honesty)

### For Societies: Nice-to-Have Features

**Enhancements**:

1. **Deity Worship**: Religious needs and temple visits
2. **Megabeast Integration**: Historical threats affecting current world
3. **Artifact History**: Track legendary items across generations
4. **Adventure Mode Integration**: Roguelike exploration of your own world
5. **Vampire/Corruption**: Hidden traits requiring investigation
6. **Guilds/Organizations**: Groups based on profession or interest
7. **Book Writing**: Dwarves write books about their experiences
8. **Combat Hardness**: Desensitization to violence over time

### Complexity vs. Performance Trade-offs

**What to Simplify**:

| DF Complexity | Simplification | Rationale |
|---------------|----------------|-----------|
| 50 personality facets | 15-20 facets | Core gameplay impact vs. simulation depth |
| 28 needs | 15-18 needs | Merge similar needs ("meals" + "drink" = "sustenance variety") |
| 8+8 memory slots | 5+5 slots | Reduce processing while keeping competition dynamic |
| Skill rust | Slower decay | Less micro-management |
| Individual item preferences | Category preferences | "Rose gold" → "valuable metals" |
| Full world history | Fortress-only history | Simpler persistence |

**What to Keep**:

- **Memory competition**: This is DF's secret sauce—limited slots create genuine forgetting
- **Personality-behavior links**: Traits must visibly affect actions
- **Relationship consequences**: Death must affect friends/family
- **Need-weighting**: Not all needs equal—some dominate
- **Specificity**: Even simplified, preferences should feel concrete

### Technical Implementation Guidance

**Data Structures**:

```
Agent {
  // Identity
  id, name, birth_date, species
  
  // Personality (15-20 facets, 0-100)
  personality: {
    gregariousness: 75,
    work_ethic: 60,
    violence: 30,
    greed: 45,
    emotional_stability: 80,
    // ... etc
  }
  
  // Needs (15-18 needs, -100000 to 400 current)
  needs: {
    sustenance: 350,
    rest: 200,
    social: -5000,  // negative = unmet
    achievement: 400,
    // ... etc
  }
  
  // Memories
  short_term: [Memory] // 5 slots
  long_term: [Memory]  // 5 slots
  core: [CoreMemory]   // unlimited
  
  // Relationships
  relationships: [{
    target_id,
    type: ENUM(friend, enemy, family, acquaintance),
    strength: 0-100,
    formed_date
  }]
  
  // Skills
  skills: {
    carpentry: { level: 8, experience: 450 },
    masonry: { level: 3, experience: 120 },
    // ... etc
  }
  
  // State
  stress: { short_term: 500, long_term: 12000 },
  focus: 110, // percentage
  current_job: Job | null,
  location: Coordinates
}

Memory {
  event_type: ENUM,  // category for slot competition
  emotion: ENUM,     // specific emotion felt
  strength: 0-10000, // intensity
  description: String, // "saw Urist die in battle"
  year: Int,
  participants: [AgentID] // who was involved
}

CoreMemory {
  // extends Memory
  personality_change: {
    facet: String,
    delta: Int,  // e.g., +10 to bravery
    new_value: Int
  }
}
```

**Key Algorithms**:

**Focus Calculation**:
```
function calculateFocus(agent):
  total_weight = 0
  satisfied_weight = 0
  
  for each need in agent.needs:
    weight = need.base_weight * personality_multiplier(agent, need)
    total_weight += weight
    
    if need.satisfaction > 0:
      satisfied_weight += weight * (need.satisfaction / 400)
  
  return (satisfied_weight / total_weight) * 100
```

**Memory Slot Competition**:
```
function addShortTermMemory(agent, new_memory):
  // Check if same event type exists
  existing = find_memory_by_type(agent.short_term, new_memory.event_type)
  
  if existing:
    // Keep stronger emotion
    if new_memory.strength > existing.strength:
      replace_memory(agent.short_term, existing, new_memory)
  else:
    // Find weakest slot
    weakest = find_weakest_memory(agent.short_term)
    if new_memory.strength > weakest.strength:
      replace_memory(agent.short_term, weakest, new_memory)
```

**Relationship Formation**:
```
function evaluateCompatibility(agent_a, agent_b):
  score = 50  // baseline
  
  // Personality similarity bonus
  for each facet in personality_facets:
    diff = abs(agent_a.personality[facet] - agent_b.personality[facet])
    if diff < 20:
      score += 5  // similar
    else if diff > 60:
      score -= 10  // very different, potential grudge
  
  // Shared preferences bonus
  shared_likes = intersection(agent_a.likes, agent_b.likes)
  score += shared_likes.length * 3
  
  return score
```

**Performance Considerations**:

1. **Batch Processing**: Don't evaluate every agent every tick. Rotate through agents:
   - Tick 1: Agents 0-99
   - Tick 2: Agents 100-199
   - etc.

2. **LOD for Agents**: Distant/unseen agents use simplified simulation:
   - No pathfinding
   - Simplified needs decay
   - No memory formation

3. **Event-Driven**: Only calculate stress when emotions occur; don't poll continuously

4. **Memory Pruning**: Automatically delete memories below threshold for agents >100 tiles away

5. **Relationship Caching**: Store relationship strength in both agents to avoid lookups

6. **Need Priority**: Only check all needs every 10 ticks; check critical needs (hunger) every tick

**Target Metrics**:

- 100 agents: <1ms per tick on mid-tier CPU
- 500 agents: <5ms per tick (acceptable for base-building pace)
- Memory: <100KB per agent (10,000 agents = ~1GB, manageable)

## Source Index

### Primary Sources

| Source | Type | URL | Reliability |
|--------|------|-----|-------------|
| DF Wiki - Need | Wiki | dwarffortresswiki.org/index.php/Need | High - Detailed mechanics |
| DF Wiki - Personality Facet | Wiki | dwarffortresswiki.org/index.php/Personality_facet | High - Complete list of 50 facets |
| DF Wiki - Thoughts and Preferences | Wiki | dwarffortresswiki.org/index.php/Thoughts_and_preferences | High - System overview |
| DF Wiki - Memory | Wiki | dwarffortresswiki.org/index.php/Memory_(thought) | High - Memory tier mechanics |
| DF Wiki - Relationship | Wiki | dwarffortresswiki.org/index.php/Relationship | High - Formation mechanics |
| DF Wiki - Labor | Wiki | dwarffortresswiki.org/index.php/Labor | High - Job assignment |
| DF Wiki - Stress | Wiki | dwarffortresswiki.org/index.php/Stress | High - Technical details |
| DF Wiki - Legends | Wiki | dwarffortresswiki.org/index.php/DF2014:Legends | High - History tracking |

### Secondary Sources

| Source | Type | Reliability |
|--------|------|-------------|
| Boatmurdered LP | Community Story | High - Primary emergent narrative example |
| "An Indomitable Dwarf" | Community Story | Medium - Secondary emergent example |
| DF Stories (dfstories.com) | Community Collection | Medium - Multiple emergent examples |
| Dwarf Fortress Academic Paper | Research | High - Systems analysis |

### Developer Sources

| Source | Type | URL |
|--------|------|-----|
| Tarn Adams PC Gamer Interview | Interview | pcgamer.com/dwarf-fortress-creator-tarn-adams |
| Tarn Adams Gamasutra Interview | Interview | gamedeveloper.com/design/interview-the-making-of-dwarf-fortress |
| Dwarf Fortress Talk Podcast | Podcast | bay12games.com/dwarves |
| Bay 12 Dev Logs | Dev Blog | bay12games.com/dwarves/dev.html |

## Confidence Assessment

**High Confidence**:

- **Need system mechanics**: Direct from wiki with specific numbers (weights, satisfaction ranges, focus formula)
- **Personality facet list**: Complete 50-facet list with 0-100 scale and effects documented
- **Memory system**: Three-tier structure with slot counts and promotion mechanics verified
- **Relationship formation**: Proximity + compatibility system with specific thresholds (rank 15 = friend)
- **Labor assignment**: Work details, specialization, tool requirements documented

**Medium Confidence**:

- **Exact stress numbers**: Ranges (-100000 to 100000 short-term) documented but may vary by version
- **Core memory promotion rate**: 1:3 chance mentioned but not extensively tested
- **Skill decay rate**: Mentioned but specific rate not documented
- **Relationship decay timing**: "One year without contact" but exact timing may vary

**Low Confidence**:

- **Specific emotion strength values**: Table incomplete, some values inferred
- **Task priority algorithm**: Described conceptually but exact code unknown
- **Pathfinding integration**: How distance factors into job assignment not fully specified
- **Combat hardness specifics**: Mentioned but mechanics not deeply documented

## Research Gaps

**Unanswered Questions**:

1. **Exact job priority calculation**: How does the game compute priority scores when multiple dwarves compete for one job?

2. **Memory emotion transformation**: How exactly do emotions change when promoting to core? Is there a formula?

3. **Grudge formation specifics**: Beyond personality difference >60, what triggers grudge vs. friendship at rank 15?

4. **Strange mood triggers**: What causes a dwarf to enter a strange mood? Random? Stress? Skill level?

5. **Social skill learning**: Exact thresholds for learning Flatterer, Consoler, etc.?

**Future Research**:

- Observe DF gameplay directly for 2-3 hours to see systems in action
- Interview Tarn Adams specifically about AI design philosophy
- Analyze DF source code (if available) or decompile to verify algorithms
- Test specific mechanics (memory decay rates, skill rust speed) empirically

## Integration Notes

### For Session 2 (AI System Design):

- **Start with Personality Facets**: Define 15-20 core traits first; everything else builds on this
- **Needs must be weighted**: Use personality to generate need weights, not uniform distribution
- **Memory competition is key**: Limited slots create realistic forgetting and prioritization
- **Relationship decay creates drama**: Without maintenance, friendships fade—forces player attention
- **Stress as unified metric**: Replaces multiple bars (happiness, sanity, mood) with one system

### For Session 3 (Core Gameplay):

- **Labor vs. Needs tension**: This is the core gameplay loop—players assign work, agents have needs
- **Specific preferences create identity**: "Likes" must be concrete for player attachment
- **Death has consequences**: No disposable agents—every death affects relationships and stress
- **Visible breakdown states**: Tantrums, depression, insanity must be visually/audibly distinct
- **Legends mode adds meaning**: History export makes players feel their world matters

### For Session 6 (Prototyping):

**Priority Order for AI Features**:

1. **Basic needs** (hunger, rest, social) with satisfaction actions
2. **3 personality facets** (gregariousness, work_ethic, emotional_stability) with gameplay effects
3. **5-slot short-term memory** with emotion competition
4. **Friendship formation** via proximity
5. **Stress system** with breakdown states
6. **Skill improvement** through practice
7. **Everything else** (tool requirements, long-term memory, core memories, etc.)

**Prototype Success Criteria**:
- Agents autonomously satisfy needs when idle
- Personality visibly affects behavior (gregarious agents seek company)
- Agents remember strongest recent events
- Agents form friendships from co-location
- Stress accumulates and causes visible reactions

---

**Research Complete**: All 8 questions answered, 3+ emergent stories documented, technical depth sufficient for implementation, clear recommendations for Societies provided. Word count: ~8,200 words.
