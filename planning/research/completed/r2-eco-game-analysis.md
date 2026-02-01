# R2: Eco Game Analysis - Design Patterns for Societies

## Executive Summary

**Eco** is a multiplayer environmental simulation game developed by Strange Loop Games that challenges players to "Solve the Tragedy of the Commons." Players must build a civilization, develop technology from the Stone Age to the Space Age, and collaborate to stop a meteor impact—all while managing a fully simulated ecosystem that reacts dynamically to their actions.

**Key Design Philosophy**: Eco operates at the intersection of three pillars: **Economy** (production, trade, specialization), **Ecology** (environmental simulation, pollution, species interactions), and **Government** (player-created laws, voting, collective decision-making). The game’s core innovation is making environmental consequences tangible, visible, and unavoidable, forcing genuine collaboration among players with conflicting individual incentives.

**Critical Findings for Societies**:

1. **Data-Driven Governance**: Eco’s law system requires players to use scientific data from the simulation to argue for regulations. This transforms abstract environmental concerns into concrete, measurable problems that demand collective solutions.

2. **Visible Consequence Chains**: Pollution and ecosystem damage are visualized through heatmaps, graphs, and direct gameplay effects (plants won't grow, species die). This visibility makes environmental management engaging rather than punitive.

3. **Specialization-Driven Interdependence**: The skill system forces players to specialize, creating natural economic interdependence. No single player can master all professions, making trade and cooperation essential.

4. **Time Pressure as Catalyst**: The 30-day meteor countdown provides urgency that drives collaboration. Without this external threat, the game would devolve into individual resource accumulation.

5. **Player-Created Systems**: From currency to laws to government structure, Eco lets players build their own systems rather than imposing rigid rules. This emergent complexity creates unique server cultures.

**Primary Concern for Adaptation**: Eco’s systems require significant player coordination and server population to function. Small groups or solo play lose the tension between individual and collective interests that makes the game compelling.

---

## Game Overview

Eco is set in a fully simulated world where a meteor is on a collision course, giving players 30 real-time days to develop technology to destroy it. The world contains thousands of simulated plants and animals across multiple biomes, each with growth cycles, predator-prey relationships, and environmental requirements.

**Core Loop**:
1. **Harvest** resources from the ecosystem
2. **Craft** items and build infrastructure
3. **Research** new technologies (requires resource investment)
4. **Develop** skills through specialized progression
5. **Collaborate** via trade, contracts, and government
6. **Vote** on laws to manage environmental impact
7. **Destroy** the meteor before impact—or fail together

**Unique Features**:
- **Simulated Ecosystem**: Plants grow based on temperature, rainfall, and soil nutrients. Animals have populations that fluctuate based on food availability and hunting pressure.
- **Pollution Propagation**: Ground pollution follows hydrology (water flow); air pollution spreads through wind and can cause sea level rise.
- **Player Government**: Laws are physically binding—servers reject actions that violate them.
- **Emergent Economy**: Players create their own currencies, stores, and contracts with no NPCs or pre-set prices.

---

## 1. Environmental Systems

### Pollution Propagation

**How It Works**:

Eco features two distinct pollution types with different propagation mechanics:

**Ground Pollution**:
- **Sources**: Tailings (ore processing byproduct), Garbage (dropped items), Sewage (from Blast Furnaces and machinery)
- **Propagation**: Follows hydrology rules—spreads through water flow and "seeps" into nearby blocks
- **Mitigation**: Burial reduces pollution; every 5 solid blocks above reduces pollution by 50% (exponentially)
- **Persistence**: Garbage blocks never decay; tailings must be carefully stored or buried

**Air Pollution (CO2)**:
- **Sources**: Smog from burning fuels at crafting tables, vehicle emissions, industrial machinery
- **Propagation**: Spreads through air, affected by wind patterns; dissipates over time if given space
- **Mitigation**: Trees absorb CO2 at varying rates (e.g., Oak: -0.15/day, Cedar: -0.075/day)
- **Catastrophic Risk**: Prolonged air pollution causes permanent sea level rise

**Algorithm Description**:
- Ground pollution uses a cellular automata approach with water flow simulation
- Air pollution disperses based on atmospheric conditions with falloff over distance
- Pollution blocks plant growth by checking pollution levels at growth locations
- Some plants have resistance thresholds allowing limited growth in mild pollution

**Player Experience**:

Players discover pollution through:
- **Visual cues**: Plants refusing to grow, yellowed/dead vegetation
- **Heatmaps**: Map layers showing pollution spread (color gradients from clean to contaminated)
- **Direct consequences**: Harvest yield reduction, ecosystem collapse alerts
- **Graphs**: Time-series data showing pollution trends over days

**Visualizations**:

The Map interface provides layered data visualization:
- **Pollution heatmaps**: Ground and air pollution shown as color gradients
- **Spread visualization**: Shows how pollution propagates from source points
- **Impact indicators**: Areas where plants won't grow highlighted

**Strengths**:
- **Physical realism**: Hydrology-based spread makes pollution feel logical and predictable
- **Multiple vectors**: Different pollution types require different management strategies
- **Cascading consequences**: Small pollution sources can accumulate into ecosystem collapse
- **Mitigation complexity**: Burial systems create engineering challenges that are engaging to solve
- **Data visibility**: Heatmaps make invisible threats visible and actionable

**Weaknesses**:
- **Punishing for new players**: Accidental pollution (e.g., dropping items) can have permanent consequences
- **Mitigation grind**: Burying tailings requires significant labor without immediate reward
- **Server-dependent**: Pollution management requires coordinated effort; solo players struggle
- **Unclear thresholds**: Players can't easily predict when pollution will become critical

**Recommendations for Societies**:

1. **Adopt Multi-Vector Pollution**: Implement distinct pollution types (ground, air, water) with different propagation rules to create varied environmental challenges.

2. **Implement Visual Heatmaps**: Use map layers to show pollution/economic data. Players should be able to toggle between temperature, pollution, population density, wealth distribution, etc.

3. **Create Mitigation Gameplay**: Make pollution management an active gameplay loop (building treatment facilities, reforestation projects) rather than just avoidance.

4. **Add Warning Systems**: Provide early warnings before critical thresholds (e.g., "Air quality declining—sea level rise possible in 5 days") so players can respond proactively.

5. **Scale Consequences**: Consider making pollution effects more gradual and recoverable in early game, escalating to permanent damage in later phases.

**Sources**:
- https://wiki.play.eco/en/Pollution
- https://wiki.play.eco/en/Garbage
- https://www.youtube.com/watch?v=qiUSODHtozA

---

## 2. Ecosystem Simulation

### System Architecture

**How It Works**:

Eco’s ecosystem is a fully simulated environment where every plant and animal is an individual agent with needs, behaviors, and life cycles. The simulation runs continuously even when players are offline.

**Key Components**:

**Plants**:
- Growth depends on: temperature, humidity, soil nutrients, pollution levels, space
- Different species have different requirements (e.g., some thrive in cold, others in heat)
- Growth rates affected by seasons and daily cycles
- Harvesting reduces populations which must regenerate naturally

**Animals**:
- Multiple species across trophic levels (herbivores, carnivores, omnivores)
- Population dynamics based on: food availability, predation, hunting pressure, habitat quality
- Animals have territories and movement patterns
- Reproduction rates adjust based on population density (logistic growth)

**Environmental Factors**:
- **Climate blocks**: World divided into blocks tracking temperature, humidity, ground water, wind
- **Day/night cycles**: Affect plant growth and animal behavior
- **Seasons**: Temperature and rainfall vary seasonally, affecting crop viability
- **Soil nutrients**: Nitrogen, phosphorus levels tracked and depleted by farming

**Species Interaction Model**:

**Predator-Prey Dynamics**:
- Wolves hunt elk; elk populations affect wolf survival
- Over-hunting elk leads to wolf starvation, which then allows elk overpopulation
- Classic Lotka-Volterra dynamics create oscillating population cycles

**Plant-Animal Relationships**:
- Animals graze on plants, reducing plant populations
- Plant availability directly impacts herbivore carrying capacity
- Pollinator species (implied) affect plant reproduction

**Competition**:
- Plants compete for space, light, and nutrients
- Animals compete for territory and food
- Invasive dynamics: removing predators can cause herbivore population explosions that decimate vegetation

**Consequence Chain Analysis**:

**Example Cascade**:
1. Players hunt wolves extensively (high pelt value)
2. Wolf population crashes
3. Elk population explodes without predation
4. Elk overgraze vegetation
5. Plant diversity collapses
6. Soil erosion increases
7. Farming yields drop
8. Food shortage affects player civilization

**Biodiversity Tracking**:

The game tracks biodiversity through:
- **Population counts**: Per species, visible in web interface graphs
- **Species extinction**: Permanent removal of species from the world
- **Ecosystem health metrics**: Composite scores based on diversity and stability
- **Biome diversity**: Different regions have different species compositions

**What Makes the Ecosystem Feel Alive**:

1. **Visible activity**: Animals move, graze, flee from players; plants visibly grow over time
2. **Reactivity**: Clear cause-effect chains (hunting → population decline → ecosystem changes)
3. **Unpredictability**: Natural population cycles create variation that players must adapt to
4. **Interdependence**: No species exists in isolation; all are connected through food webs
5. **Persistence**: The ecosystem continues operating even when players are offline, creating a living world

**Strengths**:
- **Emergent complexity**: Simple rules create complex, believable ecosystem behavior
- **Educational value**: Players learn real ecology concepts through gameplay
- **Tangible feedback**: Population graphs show clear trends from player actions
- **Long-term consequences**: Over-hunting effects appear days later, teaching planning
- **Visual diversity**: Multiple biomes with different species create varied experiences

**Weaknesses**:
- **Server performance**: Full ecosystem simulation is computationally expensive
- **Balancing difficulty**: Natural population cycles can conflict with gameplay needs
- **Player frustration**: Accidental extinction (e.g., over-hunting) is permanent and punishing
- **Complexity barrier**: New players don't understand why crops are failing without researching soil nutrients
- **Scale limitations**: Simulation resolution limited by performance; small-scale interactions can feel artificial

**Recommendations for Societies**:

1. **Implement Tiered Simulation**: Use detailed agent-based simulation for visible areas, simplified statistical models for distant regions to balance performance and realism.

2. **Create Ecosystem Dashboards**: Provide clear visualizations of food webs, population trends, and biodiversity metrics. Players should see their impact.

3. **Add Recovery Mechanics**: Allow ecosystem restoration projects (reintroduction programs, habitat restoration) so player mistakes aren't permanently punishing.

4. **Simplified Early Game**: Start with robust, forgiving ecosystems that can handle player learning curves, gradually introduce fragility as players advance.

5. **Resource Regeneration**: Implement renewable resource systems (managed forests, sustainable farming) as alternatives to extraction.

**Sources**:
- https://wiki.play.eco/en/Eco_Wiki
- https://www.gamedeveloper.com/design/the-design-pillars-of-eco
- https://play.eco/

---

## 3. Governance Systems

### Law Creation Workflow

**How It Works**:

Eco’s government system is entirely player-created and managed. As technology levels increase, so does the capability to affect the environment—requiring collective management through laws.

**Law Structure**:

Laws are defined in **Courts** (craftable objects placed in the world):
- Each Court can hold up to 3 separate laws
- Each law can have unlimited sections
- Each section contains: **Triggers**, **Conditions**, **Actions**

**Triggers** (When the law activates):
- Examples: "When hunting an animal," "When placing a block," "When selling an item"

**Conditions** (Who/what the law applies to):
- Examples: "Only if player has Hunting specialty," "Only in Protected District," "Only for Elk population below 50"

**Actions** (What happens):
- **Prevent**: Blocks the action entirely
- **Tax**: Charges a fee for the action
- **Confiscate**: Takes items/resources
- **Custom**: Can trigger complex behaviors

**Law Creation Process**:

1. **Drafting**: Player proposes law at a Court using web interface
2. **Composition**: Uses dropdown menus to build triggers/conditions/actions (no coding required)
3. **Argumentation**: Proposer must justify law using ecosystem data from graphs/statistics
4. **Review Period**: Other players see proposed law and discuss
5. **Voting**: Democracy by default—majority vote required to pass
6. **Enactment**: Law becomes physically binding—server rejects prohibited actions

**Example Laws**:
- "Prevent hunting if Elk population < 50" (conservation)
- "Tax 10% on all Iron Ore sales" (resource management)
- "Only Masons can place Mortared Stone in District A" (zoning)
- "Confiscate tailings not stored underground" (pollution control)

**UI/UX Analysis**:

**Web Interface Law Editor**:
- Accessible in-game via Laws window or hotkey (L)
- Opens browser-based interface
- Visual dropdown menus for all triggers/conditions/actions
- Graph integration: Players can embed statistical data as evidence
- Preview mode: Shows what the law would affect before passing

**Strengths**:
- **Accessibility**: Dropdown menus make law creation possible for non-technical players
- **Data integration**: Embedding graphs allows evidence-based governance
- **Flexibility**: "If-then" structure supports wide variety of laws (taxes, restrictions, zoning)
- **Physical enforcement**: Laws actually prevent actions rather than just penalizing after
- **Transparency**: All proposed laws visible to all players with full text

**Weaknesses**:
- **Complexity ceiling**: Dropdown system limits complexity; can't create truly novel mechanics
- **Learning curve**: Understanding what triggers/conditions are available requires experimentation
- **Trolling vulnerability**: Majority vote means large groups can oppress minorities
- **Enforcement limitations**: Some actions hard to detect/verify automatically
- **Interface friction**: Web-based UI breaks immersion; requires alt-tabbing

**Enforcement Mechanisms**:

**Automatic Enforcement** (Current):
- Server checks all actions against active laws
- Prohibited actions are rejected immediately
- No penalty—just prevention

**Future Criminal Justice System** (Planned):
- Breaking laws creates fines, jail time, or execution (perma-ban)
- Requires player-enforced policing or automated detection

**Districts and Demographics**:

- **Districts**: Geographic zones with specific rules (e.g., "No logging in Old Growth Forest")
- **Demographics**: Dynamic player groups (e.g., "All players with Level 2 Logging") used to target laws

**Recommendations for Societies**:

1. **Simplified Law Builder**: Use visual flowcharts or node-based editors for creating rules. Make it intuitive without sacrificing power.

2. **Template System**: Provide pre-made law templates ("Resource Conservation," "Zoning," "Taxation") that players can customize.

3. **Graduated Enforcement**: Start with warnings, escalate to fines, then restrictions. Don't immediately perma-ban for violations.

4. **AI Enforcement**: Use AI agents to monitor compliance and report violations, reducing player enforcement burden.

5. **Embedded Analytics**: Integrate data visualization directly into law proposal interface so evidence is easy to include.

6. **Constitutional Framework**: Allow players to define meta-rules (how laws are created, who can vote, term limits) to prevent tyranny.

**Sources**:
- https://wiki.play.eco/en/Laws
- https://wiki.play.eco/en/Government
- https://www.moddb.com/games/eco-global-survival-game/news/how-player-created-laws-work-in-eco

---

## 4. Voting Systems

### Voting Mechanics

**How It Works**:

Voting in Eco is integrated into the government system for passing laws, electing leaders, and changing constitutional structures.

**Who Can Vote**:
- By default: All players on the server
- Can be restricted: Laws can define demographics (e.g., "Only property owners can vote")
- Constitution determines: Voting rights can be modified through constitutional amendments

**What Can Be Voted On**:
- Laws (proposed, amendments, repeals)
- Constitutional changes (voting procedures, government structure)
- Elected titles (World Leader, district governors, etc.)
- District boundaries and definitions
- Currency and economic policies (if government-controlled)

**Vote Counting**:
- **Default**: Simple majority (50% + 1)
- **Supermajority**: Can be required for constitutional changes (configurable)
- **Minimum votes**: Laws require minimum total vote count to pass (prevents tiny participation)
- **Voting period**: Typically 24-48 hours (configurable)

**Player Experience**:

**Voting UI Flow**:
1. **Notification**: Players receive notification when vote begins (login message, UI alert)
2. **Review**: Access proposed law/election via Laws window (L) or Government menu
3. **Discussion**: Players can debate in chat or via in-game communication
4. **Cast Vote**: Simple Yes/No for laws; candidate selection for elections
5. **Results**: Vote counts visible in real-time; results announced when period ends

**Ballot Box System**:
- Physical object placed in world for voting
- Alternative to web interface
- Creates physical space for civic engagement

**Engagement Mechanisms**:
- **Login prompts**: Players see active votes when joining server
- **Reminders**: Periodic notifications about pending votes
- **Stakes**: Laws affect everyone, creating natural motivation to participate
- **Debate**: Discussion happens organically via chat/voice as players work

**Results Communication**:
- **Immediate feedback**: Vote counts update in real-time
- **Announcement**: Server-wide message when law passes/fails
- **History**: Past votes and results viewable in government interface
- **Enactment**: Passed laws immediately take effect (or on specified date)

**Strengths**:
- **Simple interface**: Yes/No voting is immediately understandable
- **Real-time feedback**: Seeing vote counts encourages participation
- **Flexible democracy**: Constitution allows evolving voting rules (quadratic voting, representative democracy, etc.)
- **Physical presence**: Ballot boxes create civic spaces
- **Mandatory engagement**: Laws affecting everyone drives voter turnout naturally

**Weaknesses**:
- **Tyranny of majority**: 51% can impose will on 49% with no protections
- **Low participation**: Many players ignore votes that don't immediately affect them
- **No deliberation**: Limited structured debate before voting
- **Time zone issues**: 24-hour voting period excludes players in different time zones
- **Voter apathy**: Frequent votes on minor issues leads to fatigue

**Recommendations for Societies**:

1. **Weighted Voting**: Consider vote weighting based on expertise (e.g., farmers have more say on agricultural laws) or stake (residents affected most get more weight).

2. **Deliberation Periods**: Require structured discussion phases before voting, with summary documents of arguments for/against.

3. **Delegation**: Allow players to delegate votes to representatives for specific policy areas (liquid democracy).

4. **Quadratic Voting**: Let players allocate multiple votes to issues they care about most, with cost increasing quadratically.

5. **Consensus Thresholds**: For major changes, require supermajority (66% or 75%) to ensure broad buy-in.

6. **Visual Participation**: Show voter turnout rates, make abstention visible, celebrate high participation.

**Sources**:
- https://wiki.play.eco/en/Government
- https://wiki.play.eco/en/Ballot_Box
- https://wiki.play.eco/en/Laws

---

## 5. Economic Systems - Skills

### Progression System Analysis

**How It Works**:

Eco’s skill system creates specialization and interdependence through a three-tier structure: **Professions**, **Specialties**, and **Talents**.

**Professions**:
- General areas of expertise (Smith, Farmer, Carpenter, Hunter, etc.—10 total)
- All unlocked from the start, no cost
- Organize specialties into thematic groups
- Help players coordinate roles

**Specialties**:
- Specific skill trees within professions (e.g., Carpentry, Logging, Paper Milling under Carpenter)
- Discovered through **research** (requires resource investment)
- Learned by spending **Skill Points (SP)**
- Cost increases exponentially: 0, 0, 5, 15, 50, 100, 300, 500 SP

**Talents**:
- Perks granted at Specialty levels 3 and 6
- Binary choice at each level (e.g., "+20% crafting speed" vs "-15% material cost")
- **Permanent choice**: Cannot switch later
- Forces meaningful specialization decisions

**Skill Point Gain**:

Players earn SP by leveling up, which requires **Experience (XP)** gained through:
- **Time**: Passive gain based on nutrition and housing quality
- **Crafting**: Performing actions grants XP in related skills
- **Food quality**: Nutrition bonus up to 2x multiplier based on diet variety/quality
- **Housing**: Up to 1.25x multiplier based on furniture quality/balance

**Specialization Mechanics**:

**Efficiency System**:
- Higher skill levels = greater efficiency at tasks
- Example: Level 7 Carpenter uses fewer logs per item than Level 1
- Efficiency differences create trade advantages

**Recipe Unlocking**:
- New items/recipes unlocked at specific skill levels
- Advanced technology requires high-level specialties
- Some items require multiple specialty collaborations

**Interdependence Creation**:
- No single player can master all specialties (too expensive)
- Specialization forces trade (carpenter needs iron from smith)
- Work parties allow collaborative crafting projects

**Skill UI/UX**:

**Skill Points Panel** (Hotkey: Z):
- Shows current SP and XP progress
- Nutrition balance graph (pie chart of food types)
- Housing bonus indicator
- Combined XP multiplier display

**Skills Window**:
- Tree view of all professions and specialties
- Shows cost to learn next specialty
- Tracks progress in current specialties
- Talent selection interface at level thresholds

**Strengths**:
- **Clear progression**: Exponential costs make specialization decisions meaningful
- **Natural interdependence**: Forces collaboration without artificial restrictions
- **Efficiency rewards**: Time investment pays off in material savings
- **Permanent choices**: Talent selections feel weighty and strategic
- **Passive growth**: XP gain while offline respects player time

**Weaknesses**:
- **Punishing respec**: No way to undo talent choices or reset specialties
- **Collaboration requirement**: Solo play nearly impossible for advanced content
- **Slow early game**: Initial SP gain feels slow, leading to impatience
- **Server-dependent**: Balanced for specific collaboration rates; too many/few players break progression
- **Grindy feel**: Some players perceive passive XP gain as removing agency

**Recommendations for Societies**:

1. **Hybrid Progression**: Combine time-based gain with active performance bonuses (e.g., crafting well gives XP boost).

2. **Partial Respecs**: Allow limited talent changes (e.g., once per week) to fix mistakes without removing consequence.

3. **Cross-Training**: Make early levels of other specialties cheaper so players can dabble without full commitment.

4. **Visual Progression**: Show skill mastery visually (different clothing, titles, animations) to celebrate specialization.

5. **AI Skill Assistance**: Allow AI agents to fill gaps in player skills for solo/small group play while maintaining multiplayer benefits.

**Sources**:
- https://wiki.play.eco/en/Skills
- https://wiki.play.eco/en/Skill_Points
- https://steamcommunity.com/app/382310/discussions/7/1697167355228652682/

---

## 6. Economic Systems - Economy

### Market Mechanism Analysis

**How It Works**:

Eco features an entirely player-created economy with no NPCs, preset prices, or central authority (unless players create one). The economy runs on three pillars: **Stores**, **Contracts**, and **Currency**.

**Stores**:
- Furniture placed in rooms for trading
- Owners set buy/sell prices for specific items
- Can stock items for sale and set items to purchase
- **Asynchronous**: Players trade even when owner is offline
- Currency selectable: Barter (none), player credit, or minted currency

**Contracts**:
- Written agreements for services (not just items)
- Posted on Contract Boards in common areas
- Presets for: Transport, road building, terraforming, house building, item exchange
- **Work Parties**: Special contracts for collaborative crafting (e.g., "Contribute 100 logs to this project")
- Escrow system: Payment held until completion

**Currency Types**:

**Player Credit**:
- Auto-created when player joins world
- Named after player (e.g., "Iceberg Credit")
- Generated when someone sells to your store
- Functions as IOU: "I owe you goods from my store"
- Trust-based: Player must honor credit or face reputation damage

**Minted Currency**:
- Created at a Mint using physical resources as backing
- Resource selected once (wood pulp, gold bars, land claim papers, etc.)
- Irreversible: Can't recover backing resources
- Ratio set by creator (e.g., 1 Gold Bar = 100 Currency)
- Can be government-controlled or player-created

**Price Discovery**:

**Supply and Demand**:
- Prices entirely player-set
- No market algorithms or NPC price adjustments
- Value determined by labor input, resource scarcity, skill efficiency
- Players compete on price for similar goods

**Economic Data Available**:

**Economy Viewer** (Hotkey: Y):
- All active stores and their listings
- Price comparisons across sellers
- Currency exchange rates
- Transaction histories
- Bank account management

**Web Interface Graphs**:
- Price trends over time
- Trade volume statistics
- Currency circulation data
- Resource availability metrics

**What Makes the Economy Feel Dynamic**:

1. **Emergent prices**: No preset values mean prices reflect actual player valuation
2. **Specialization arbitrage**: Efficiency differences create profit opportunities
3. **Reputation systems**: Untrustworthy players (not honoring credit) get negative rep
4. **Government intervention**: Laws can tax, restrict, or regulate economic activity
5. **Currency competition**: Multiple currencies compete for adoption
6. **Contract complexity**: Custom agreements enable unique economic relationships

**Strengths**:
- **Genuine emergence**: Economy reflects actual player needs and creativity
- **Multiple currencies**: Competition between currencies creates interesting dynamics
- **Contract flexibility**: Services tradeable, not just goods
- **Asynchronous trade**: Stores enable trade across time zones/play sessions
- **Government integration**: Economy and governance deeply connected (taxes, regulations)

**Weaknesses**:
- **New player barrier**: Understanding credit/currency systems has steep learning curve
- **Liquidity problems**: Early servers lack established currencies, making trade difficult
- **Exploitation risk**: No safeguards against scams or market manipulation
- **Inflation potential**: Unlimited credit creation can cause currency devaluation
- **Coordination required**: Market needs minimum population to function; small servers struggle

**Recommendations for Societies**:

1. **Multiple Currency Support**: Allow both fiat (government) and commodity-backed (player) currencies to coexist.

2. **Market Transparency**: Provide clear price history, volume data, and trend analysis to help players make informed decisions.

3. **Contract Templates**: Offer pre-written contract templates for common agreements (employment, construction, loans) with clear clauses.

4. **Reputation Systems**: Track and display player reliability (contract completion rate, credit honoring) to build trust.

5. **Government Economic Tools**: Allow taxation, subsidies, and regulations that affect market behavior (e.g., carbon taxes on pollution).

6. **AI Market Makers**: Use AI agents to provide baseline liquidity and price discovery, especially for small populations.

**Sources**:
- https://wiki.play.eco/en/Economy
- https://wiki.play.eco/en/Store
- https://wiki.play.eco/en/Contracts
- https://steamcommunity.com/app/382310/discussions/0/3667553846825225838/

---

## 7. Meteor Threat

### Threat Implementation Analysis

**How It Works**:

The meteor is Eco's "win condition" that drives the entire game arc. It creates time pressure forcing collaboration and technological development.

**Implementation Details**:

**Introduction**:
- Visible in sky from game start
- 30 real-time days until impact (configurable)
- Countdown visible on meteor in sky
- No way to stop it without technology

**Preparation Required**:

**Technology Tree**:
- Must advance from Stone Age → Industrial → Modern → Space Age
- Requires researching specific technologies at Research Table
- Each technology requires resource investment (resources consumed)
- Collaboration needed: Multiple specialties required

**Destruction Requirements**:
- Build 4 **Lasers** (endgame technology)
- Build 1 **Computer Lab** (to coordinate lasers)
- Generate massive amounts of **Power**
- Lasers need line of sight to meteor
- 30-second charge time before firing

**Failure Consequences**:

**Meteor Impact**:
- Initial explosion creates large crater at impact site
- Meteor shower of smaller meteors for ~8.5 minutes
- Leaves craters across world surface
- Does not kill players (can watch destruction)
- Destroys buildings, plants, animals in impact zones
- World continues but damaged; players must rebuild

**Success**:
- Lasers fire simultaneously
- Meteor destroyed in space
- World saved
- Game can continue indefinitely (or server resets)

**Player Behavior Impact**:

**Urgency**:
- Time limit forces decision-making under pressure
- Prevents infinite procrastination
- Creates natural game arc (beginning → mid → endgame)

**Cooperation**:
- Single players cannot master all required skills in time
- Must collaborate across specialties
- Shared fate (everyone wins or loses together)

**Competition**:
- Resource scarcity creates competition
- Debates over resource allocation (laser research vs infrastructure)
- Conflicts between immediate needs and long-term goals

**Stress vs Compulsion**:

**What Makes It Compelling**:
- **Visible progress**: Meteor countdown creates clear goal
- **Achievable challenge**: 30 days is enough time with cooperation
- **Shared stakes**: Everyone invested in success
- **Gradual escalation**: Technology needs ramp up naturally
- **Spectacle**: Destroying meteor is satisfying payoff

**What Makes It Stressful**:
- **Real-time pressure**: Can't pause or slow countdown
- **Irreversible**: Failure means permanent world damage
- **Coordination difficulty**: Requires organized group effort
- **New player intimidation**: Joining late in countdown feels hopeless
- **Server population dependent**: Too few players = impossible; too many = chaotic

**Recommendations for Societies**:

1. **Flexible Time Pressure**: Consider multiple threat types with different timelines (immediate crises vs long-term challenges) to vary pacing.

2. **Scalable Difficulty**: Adjust threat intensity based on player count and progression to maintain challenge without impossibility.

3. **Partial Success**: Allow partial mitigation (e.g., evacuating population, preserving knowledge) so failure isn't total loss.

4. **Multiple Victory Conditions**: Beyond just stopping threat, include cultural, economic, or ecological victory conditions.

5. **Visible Progress**: Make threat mitigation progress visible (laser construction, defense preparation) to maintain motivation.

6. **AI Collaboration**: Ensure AI agents can contribute meaningfully to threat response so small player counts remain viable.

**Sources**:
- https://wiki.play.eco/en/Meteor
- https://play.eco/news/eco-a-doubly-doomed-world
- https://www.youtube.com/watch?v=rXf1zejjoY0

---

## 8. UI/UX Patterns

### UI Pattern Catalog

**How Complex Data is Visualized**:

**1. Layered Heatmaps (Map Interface)**:
- **Pattern**: Toggleable data layers on world map
- **Implementation**: Dropdown menu selects data type (pollution, population, temperature, rainfall)
- **Visualization**: Color gradient heatmap overlaid on terrain
- **Scale**: Color key shows value ranges (e.g., low=green, high=red)
- **Use cases**: Pollution spread, species populations, biome boundaries
- **Effectiveness**: Makes invisible systems visible at glance
- **Frustration**: Switching layers requires multiple clicks; can't compare two layers simultaneously

**2. Time-Series Graphs (Web Interface)**:
- **Pattern**: Line graphs showing trends over time
- **Implementation**: Browser-based interface accessible via hotkey (G)
- **Data**: Population counts, pollution levels, economic transactions, climate data
- **Features**: Zoom, date range selection, multiple data series
- **Use cases**: Tracking ecosystem health, economic trends, pollution history
- **Effectiveness**: Historical context helps predict future trends
- **Frustration**: Requires leaving game to view; not integrated into HUD

**3. Real-Time Stat Panels (HUD)**:
- **Pattern**: Persistent display of key metrics
- **Implementation**: Skill Points panel showing nutrition, housing, XP gain
- **Data**: Graphs (nutrition pie chart), numerical values (SP count), multipliers
- **Use cases**: Personal progression tracking
- **Effectiveness**: Always visible, informs immediate decisions (what to eat)
- **Frustration**: Cluttered appearance; small text hard to read

**4. Notification System**:
- **Pattern**: Toast alerts for important events
- **Implementation**: Popup messages when laws proposed, votes active, skill levels gained
- **Features**: Dismissible, color-coded (green=positive, red=negative)
- **Use cases**: Law enactment, election results, contract completion
- **Effectiveness**: Brings attention to important changes
- **Frustration**: Can spam multiple notifications simultaneously

**5. Dropdown Construction Menus**:
- **Pattern**: Hierarchical menus for law/contract creation
- **Implementation**: Cascading dropdowns for Triggers → Conditions → Actions
- **Use cases**: Law composition, contract terms
- **Effectiveness**: Makes complex logic accessible without coding
- **Frustration**: Deep nesting can be confusing; limited flexibility

**Information Architecture**:

**Menu Hierarchy**:
```
Esc (Game Menu)
├── Interface Scale settings
├── Skills (Z)
│   ├── Professions tree
│   ├── Specialties costs
│   └── Talent selection
├── Backpack (B)
├── Economy (Y)
│   ├── Stores list
│   ├── Contracts board
│   └── Currency exchange
├── Map (M)
│   ├── World layers (heatmaps)
│   ├── Markers
│   └── Player locations
├── Laws (L) → Opens Web Interface
├── Graphs (G) → Opens Web Interface
├── Chat (C)
│   ├── General
│   ├── Trade
│   └── Government
└── Objectives (O)
```

**Navigation Patterns**:
- **Hotkey-driven**: Most systems accessible via single keystroke
- **Contextual**: Right-clicking objects opens relevant interactions
- **Web integration**: Complex systems (laws, graphs) use browser for flexibility
- **Physical world**: Some systems (stores, courts, ballot boxes) are physical objects players must locate

**Learning the Interface**:

**Onboarding**:
- Tutorial objectives guide initial interactions
- Tooltips explain icons and values
- Ecopedia (in-game wiki) provides reference information
- Community wikis and guides fill gaps

**Discoverability**:
- **Strengths**: Hotkeys shown in UI; consistent patterns (web interface for complex systems)
- **Weaknesses**: No guided tour; many features hidden behind hotkeys; web interface breaks immersion

**Usability Evaluation**:

**Effective**:
- Hotkeys enable quick access without menu diving
- Heatmaps make complex data immediately comprehensible
- Web interface allows rich data presentation impossible in-game
- Physical objects (stores, courts) create natural interaction points
- Consistent color coding (green=good, red=bad/danger)

**Frustrating**:
- Web interface requires alt-tabbing, breaking immersion
- Graphs not integrated into world (can't see pollution while looking at landscape)
- Small text and UI elements hard to read at distance
- No split-screen comparison (can't view pollution and temperature simultaneously)
- Information overload: Too many stats presented without prioritization
- Mobile-unfriendly: UI designed for desktop monitors

**Recommendations for Societies**:

**Patterns to Adopt**:

1. **Layered Visualization**: Implement toggleable map layers for different data types (pollution, wealth, crime, happiness) using color-coded heatmaps.

2. **Persistent Personal Dashboard**: Always-visible HUD showing critical personal metrics (health, wealth, skill progress) with mini-graphs for trends.

3. **In-World Data Displays**: Project graphs/charts onto buildings or dedicated info centers so players don't need to open menus.

4. **Contextual Notifications**: Smart alerts that appear based on player activity and relevance (e.g., "Hunting yield declining—check elk population graph").

5. **Node-Based Editors**: Use visual flowcharts for creating laws/policies instead of dropdowns—more intuitive and flexible.

**Patterns to Avoid**:

1. **Web Interface Dependency**: Keep all UI in-game; avoid requiring external browsers which breaks immersion.

2. **Information Overload**: Prioritize information—show critical alerts prominently, tuck detailed stats into collapsible panels.

3. **Hidden Hotkeys**: Make controls discoverable through visual cues rather than requiring memorization.

4. **Static Tutorials**: Replace one-time tutorials with contextual tooltips that appear when players encounter new systems.

**Sources**:
- https://wiki.play.eco/en/Web_Interface
- https://wiki.play.eco/en/Map
- https://wiki.play.eco/en/Getting_Started
- https://www.gamedeveloper.com/design/the-design-pillars-of-eco

---

## Cross-Cutting Insights

### Patterns to Adopt

**1. Data-Driven Decision Making** (High Priority)
- **Rationale**: Eco's requirement to use scientific data to argue for laws transforms governance from opinion-based to evidence-based
- **Implementation**: Integrate graphs/statistics directly into proposal interfaces; make ecosystem metrics easily accessible
- **Impact**: Creates more informed, less emotional decision-making; teaches systems thinking

**2. Visible Consequence Chains** (High Priority)
- **Rationale**: Heatmaps and graphs make abstract environmental systems tangible and actionable
- **Implementation**: Use layered visualizations to show pollution, population dynamics, economic flows
- **Impact**: Players understand their impact; environmental management becomes engaging puzzle rather than punitive restriction

**3. Specialization-Based Interdependence** (High Priority)
- **Rationale**: Exponential skill costs force collaboration naturally without artificial barriers
- **Implementation**: Design skill trees where mastery requires significant investment; ensure all skills have value to others
- **Impact**: Creates organic economic relationships; no player can succeed alone

**4. Player-Created Governance** (Medium Priority)
- **Rationale**: Letting players define their own laws and government structures creates emergent complexity and investment
- **Implementation**: Provide tools (not rules) for law creation; allow constitutional evolution
- **Impact**: Unique server cultures; player investment in political process

**5. Time-Limited Collaborative Challenges** (Medium Priority)
- **Rationale**: Meteor countdown provides urgency that drives cooperation and prevents infinite procrastination
- **Implementation**: Create periodic server-wide challenges with visible deadlines
- **Impact**: Creates natural game arcs; prevents stagnation; rewards coordination

### Patterns to Avoid

**1. Permanent Punitive Consequences** (High Priority)
- **Rationale**: Eco's permanent extinctions and pollution can be demoralizing, especially for new players
- **Alternative**: Implement reversible damage and restoration mechanics; teach through recovery rather than punishment
- **Benefit**: Maintains challenge without hopelessness; allows learning from mistakes

**2. Web-Based Interface for Core Systems** (High Priority)
- **Rationale**: Alt-tabbing to browser breaks immersion and creates friction
- **Alternative**: Build all UI in-game using diegetic interfaces (computers, info boards, projections)
- **Benefit**: Maintains immersion; smoother UX; works better on all platforms

**3. Tyranny of Majority Voting** (Medium Priority)
- **Rationale**: Simple majority voting allows oppression of minority play styles
- **Alternative**: Implement weighted voting, consensus requirements, or constitutional protections
- **Benefit**: Protects diverse play styles; encourages coalition building

**4. Solo Play Exclusion** (Medium Priority)
- **Rationale**: Eco nearly requires server population to function; solo play loses core tension
- **Alternative**: Scale systems dynamically; provide AI collaborators; maintain challenge across group sizes
- **Benefit**: Accessible to all player counts; doesn't punish small friend groups

### Unique Opportunities for Societies

**1. AI-Native Design**
Unlike Eco which added single-player as an afterthought, Societies can design AI agents as core systems from the start. AI can:
- Fill economic niches when player count is low
- Demonstrate sustainable practices through behavior
- Provide opposition/opportunity without requiring massive human coordination
- Serve as "training wheels" for complex systems

**2. Multi-Scale Governance**
Eco is limited to single-world governments. Societies can implement:
- Neighborhood-level governance (zoning, local services)
- City-level governance (infrastructure, taxes)
- Regional/national governance (environmental policy, trade)
- International governance (climate treaties, migration)
This nested structure mirrors real-world governance complexity.

**3. Cultural Evolution Systems**
Beyond laws, track and simulate:
- Cultural values that shift based on conditions
- Generational attitudes toward environment/economy
- Social movements that emerge organically
- Media/propaganda systems that influence opinion

**4. Reverse Tragedy of Commons**
While Eco focuses on preventing environmental destruction, Societies can also model:
- Collective action problems in urban planning
- Innovation coordination (who pays for R&D?)
- Crisis response (coordination during disasters)
- Long-term investment (pension systems, infrastructure)

---

## Synthesis: Key Lessons for Societies

### High Priority

**1. Make Systems Visible and Understandable**
Eco's greatest success is making invisible environmental systems visible through heatmaps and graphs. Societies must invest heavily in data visualization that helps players understand complex societal systems (economics, ecology, politics) at a glance. Layered map views, real-time dashboards, and clear trend indicators are essential.

**2. Force Interdependence Through Design**
The exponential skill cost system in Eco creates natural economic interdependence without artificial restrictions. Societies should design progression systems where specialization is rewarding but complete self-sufficiency is impossible, forcing trade and collaboration.

**3. Evidence-Based Governance**
Eco's requirement to use scientific data when proposing laws elevates governance above opinion. Societies should integrate data collection and visualization into the political process, making evidence-based argumentation the norm rather than the exception.

**4. Time Pressure as Design Tool**
The meteor countdown provides urgency that drives collaboration and prevents stagnation. Societies should include periodic server-wide challenges or threats that require collective response within visible timeframes.

### Medium Priority

**5. Player-Created Systems**
Eco lets players create currencies, laws, and governments rather than imposing fixed rules. Societies should provide tools for players to define their own economic and political structures, creating emergent complexity and player investment.

**6. Consequence Without Hopelessness**
While Eco's permanent damage teaches caution, it can also be demoralizing. Societies should implement reversible consequences and restoration mechanics that allow learning from mistakes without permanent punishment.

**7. Asynchronous Collaboration**
Eco's stores and contracts enable trade across time zones and play sessions. Societies should design systems that allow meaningful contribution even when players aren't simultaneously online.

**8. Flexible Government Structures**
Eco's constitution system allows various government forms (democracy, dictatorship, etc.). Societies should provide constitutional frameworks that let player communities evolve their governance structures organically.

### Low Priority / Future Reference

**9. Currency Competition**
Eco's multiple currency system creates interesting economic dynamics but may be too complex for initial implementation. Consider starting with single currency, adding complexity later.

**10. Physical World Objects for Systems**
Eco requires physical placement of stores, courts, and ballot boxes. While immersive, this can be inconvenient. Consider hybrid approach: physical placement for major structures, menu access for routine use.

**11. Web Interface Integration**
Eco uses web browsers for complex interfaces. While this enables rich functionality, it breaks immersion. Prioritize in-game UI even if it limits complexity initially.

**12. Real-Time Server Persistence**
Eco's ecosystem runs 24/7 even without players. This is resource-intensive and can create maintenance challenges. Consider alternative models (time compression when players offline, event-driven simulation).

---

## Research Quality Assessment

### Sources Used

**Primary Sources**:
- Eco Official Wiki: https://wiki.play.eco/en/ (Pollution, Laws, Meteor, Economy, Skills, Government, Map, Web Interface)
- Official Eco Website: https://play.eco/
- Steam Store Page: https://store.steampowered.com/app/382310/Eco/

**Secondary Sources**:
- Game Developer - Design Pillars of Eco: https://www.gamedeveloper.com/design/the-design-pillars-of-eco
- ModDB - Player Created Laws: https://www.moddb.com/games/eco-global-survival-game/news/how-player-created-laws-work-in-eco
- YouTube Gameplay: https://www.youtube.com/watch?v=qiUSODHtozA, https://www.youtube.com/watch?v=rXf1zejjoY0

**Community Sources**:
- Reddit r/EcoGlobalSurvival: https://www.reddit.com/r/EcoGlobalSurvival/
- Steam Community Discussions: https://steamcommunity.com/app/382310/discussions/

### Confidence Levels

**High Confidence** (Verified across multiple sources):
- Three-pillar design (Economy, Ecology, Government)
- Law system structure (triggers, conditions, actions)
- Meteor countdown mechanics (30 days, lasers to destroy)
- Skill system basics (professions, specialties, talents)
- Store and currency mechanics
- Pollution types (ground vs air) and mitigation

**Medium Confidence** (Single source or inferred):
- Specific numerical values (pollution reduction rates, skill point costs)
- UI/UX implementation details
- Web interface technical architecture
- Ecosystem simulation algorithms

**Low Confidence** (Limited documentation, may have changed):
- Criminal justice system (mentioned as planned, may not be implemented)
- Specific gameplay balance (tuned regularly in updates)
- Console/mobile UI implementations
- Server performance characteristics

### Gaps in Research

**What Couldn't Be Determined**:
1. **Exact simulation algorithms**: The precise math behind ecosystem simulation, pollution propagation, and population dynamics is proprietary/undocumented
2. **Player behavior metrics**: No data on how often laws are proposed, voting participation rates, or economic transaction volumes
3. **Update changes**: Wiki may not reflect latest game version; some mechanics may have changed
4. **Modding capabilities**: Extent to which systems can be modified unclear
5. **Performance benchmarks**: Server specs needed for various player/ecosystem sizes undocumented
6. **Multi-server interactions**: Whether multiple worlds can interact (trade, migration) unclear

**Recommended Additional Research**:
- Interview Eco developers or experienced server admins
- Play Eco directly for hands-on experience
- Analyze server logs if available
- Review academic papers analyzing Eco's design

---

## Next Steps

### For Session 3 (Core Gameplay)

**Immediate Applications**:
1. **Implement layered map visualization** for pollution, population, and economic data
2. **Design exponential skill progression** to force specialization and interdependence
3. **Create evidence-gathering tools** (graphs, statistics) for ecosystem monitoring
4. **Develop basic contract/store system** for asynchronous trade
5. **Add visible countdown system** for periodic collaborative challenges

**Design Questions to Resolve**:
- How many skill specializations should exist?
- What data should be visualized on maps vs in menus?
- How to balance AI agents vs human players in economy?
- What are appropriate time scales for challenges?

### For Session 4 (Progression & Balance)

**Systems to Develop**:
1. **Skill tree architecture** with meaningful choices and trade-offs
2. **Efficiency scaling** that rewards specialization
3. **Ecosystem simulation** with visible feedback loops
4. **Pollution propagation** with mitigation gameplay
5. **Technology progression** tied to resource investment

**Balance Considerations**:
- Skill point gain rates across different play styles
- Ecosystem fragility vs player learning curve
- Economic liquidity at different population sizes
- Challenge difficulty scaling

### For Session 5 (Governance)

**Priority Features**:
1. **Law creation interface** using visual flowcharts or node editors
2. **Voting system** with multiple modes (direct, representative, quadratic)
3. **Constitutional framework** for defining government structure
4. **Data integration** in governance (graphs embedded in proposals)
5. **District/demographic system** for targeted policies

**UX Priorities**:
- Keep all governance UI in-game (no web interface)
- Make law consequences clear before voting
- Provide templates for common law types
- Visualize government structure and current laws

---

## Appendix: Visual Examples Referenced

**1. Pollution Heatmap**
- Source: https://wiki.play.eco/en/Pollution
- Description: Minimap showing tailings spread with color gradient from clean (green) to heavily polluted (red)
- Shows: Ground pollution following hydrology patterns

**2. Law Composition Interface**
- Source: https://wiki.play.eco/en/Laws
- Description: Web interface screenshot showing dropdown menus for triggers, conditions, and actions
- Shows: "Prevent hunting unless player has specialty" law being composed

**3. Meteor Visualization**
- Source: https://wiki.play.eco/en/Meteor
- Description: Sky view showing meteor with visible countdown timer
- Shows: Impending threat visible in game world, not just UI

**4. Skill Tree Structure**
- Source: https://wiki.play.eco/en/Skills
- Description: Icons showing 10 professions with nested specialties
- Shows: Smith → Smelting/Blacksmith/Advanced Smelting hierarchy

**5. Graphs Interface**
- Source: https://wiki.play.eco/en/Web_Interface
- Description: Line graph showing ecosystem data trends over time
- Shows: Population counts or pollution levels with date axis

**6. Store Interface**
- Source: https://wiki.play.eco/en/Economy
- Description: Player store showing buy/sell listings with prices
- Shows: Asynchronous trade with credit/currency options

**7. Design Pillars Diagram**
- Source: https://www.gamedeveloper.com/design/the-design-pillars-of-eco
- Description: Venn diagram showing Economy, Ecology, Government intersection
- Shows: Core design philosophy of solving Tragedy of the Commons

---

*Research completed: January 2026*
*Researcher: Agent B (Game Design Specialist)*
*Word count: ~3,800 words*
