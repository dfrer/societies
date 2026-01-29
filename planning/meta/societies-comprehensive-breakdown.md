# SOCIETIES: Comprehensive Breakdown of Features and Gameplay
*An AI-Enhanced Multiplayer Civilization Simulation*

A society and ecology simulation and multiplayer game where human players and AI citizens coexist as equals in a low poly-3D, reasonably sized, global simulation, with up to about 100-200 Agents in total. Alongside surviving and working on their own goals, all agents are economic and political participants that participate communally with other agents to make towns, cities, states, nations and even federations that work together, or even against each other to grow and cook food, build shelters, create infrastructure, and much more. Together or in competition they build civilizations that persist, adapt, and survive by navigating ecological, societal, and existential problems.

The core of the gameplay loop is Agents—Both human and AI—surviving, building, being creative, creating goals, building homes, progressing careers, facilitating connections, manufacturing automation, all while facing escalating challenges, together.

---

## Table of Contents
1.  **Overview and Objective**
2.  **Core Design Philosophy**
3.  **World and Ecosystem Simulation**
4.  **Environmental Impact and Pollution**
5.  **AI Population System**
6.  **Society and Governance**
7.  **Resource Gathering and Crafting**
8.  **Skills, Specialization, and Roles**
9.  **Economy and Trade Systems**
10. **Threats and Endgame Progression**
11. **Multiplayer and Social Dynamics**
12. **Additional Features**
13. **Design Philosophy and Conclusion**

---

## 1. Overview and Objective
Societies is an open-world environmental simulation game that blends survival, crafting, economics, and governance in a multiplayer setting enhanced by AI-driven agents.
Players inhabit a small virtual planet (0.52km² - 4km²), teeming with plants and animals, working alongside AI citizens to build a civilization advanced enough to face escalating existential threats—all while maintaining ecological balance in a persistent, evolving world.

The game begins with all participants—human and AI alike—as independent homesteaders, each with nothing but basic tools and a small land claim. From this primitive state, players must cooperate to form increasingly complex social structures: neighborhoods, towns, states, countries, and eventually planetary federations. This societal progression is not merely a backdrop—it is the core gameplay loop. The game is fundamentally about **building society itself**, with survival, crafting, and economics serving as the mechanisms through which social organization becomes necessary and meaningful.

Building upon the foundation established by games like Eco, Societies addresses critical limitations in purely human-driven simulations: server death from player attrition, economic stagnation from finite demand, tedious labor requirements, and weak endgame incentives. The core innovation is treating AI agents as **first-class citizens**—economically, politically, and socially equivalent to human players—creating resilient, dynamic societies that persist and evolve regardless of human player availability.

Unlike typical survival games, Societies has no combat or character death in the traditional sense. There are no monsters or PvP violence; the focus is entirely on cooperation, sustainability, society-building, and the emergent drama that arises from competing interests within a shared world. The conflicts that emerge are political, economic, and social—debates over resource allocation, environmental policy, territorial rights, and the distribution of power. These conflicts are resolved through negotiation, voting, trade, and law—not through violence.

### What Makes Societies Different
Many games feature survival mechanics, crafting systems, or governance features. Societies distinguishes itself through several key design decisions:
*   **Society as Core Gameplay**: The progression from homesteader to citizen of a planetary federation IS the game, not a feature layered on top of survival mechanics.
*   **AI-Human Equivalence**: AI agents are not NPCs or tools—they are citizens with the same rights, roles, and capabilities as human players.
*   **Persistent World**: The simulation continues whether humans are online or not, creating a living world rather than a session-based experience.
*   **Escalating Challenges**: Threats progress beyond a single meteor, creating ongoing purpose for continued civilization development.
*   **Emergent Politics**: Governance is not a minigame but a necessity—the complexity of managing resources, pollution, and threats requires organized society.

---

## 2. Core Design Philosophy
### The Fundamental Principle: Equivalence
The foundational principle of Societies is that **AI agents and human players are the same type of entity**, differing only in their controller. This is not merely a technical convenience—it is an architectural decision that fundamentally changes how the simulation works and what experiences it can create.

In traditional multiplayer games, the world exists for human players. NPCs, if present, are clearly subordinate—vendors, quest-givers, or enemies. The game's systems assume human participation and break down without it. Societies inverts this assumption: the world exists as a simulation first, with humans being one type of participant among many. The simulation does not require human presence to function, though human participation makes it richer, more creative, and more unpredictable. This equivalence manifests across all game systems:

*   **Economic equivalence**: AI agents buy, sell, produce, and consume using the same mechanisms as humans. They respond to prices, seek profits, and face bankruptcy.
*   **Political equivalence**: AI agents can vote, hold office (where permitted by constitution), propose laws, and organize into political factions.
*   **Social equivalence**: AI agents have reputations, form relationships, hold grudges, and remember past interactions.
*   **Labor equivalence**: AI agents perform the same jobs as humans, with the same skill systems, efficiency curves, and resource requirements.

### Why Equivalence Matters
The equivalence principle solves problems that are inherent to purely human-driven multiplayer simulations:
*   **The Continuity Problem**: In human-only games, when the blacksmith logs off, iron tool production stops. If they quit entirely, the supply chain breaks. Other players must either take over a role they didn't want or watch progress stall. In Societies, when a human blacksmith leaves, AI blacksmiths continue production—perhaps less efficiently or creatively, but the economy doesn't collapse. The supply chain bends rather than breaks.
*   **The Demand Problem**: In finite-demand economies, once everyone has four beds, bed demand drops to zero forever. The furniture maker's profession becomes obsolete. In Societies, AI agents continuously consume goods—they need food, tools wear out, buildings require maintenance. Demand never terminates because consumption is ongoing.
*   **The Tedium Problem**: Some necessary work is repetitive and unengaging—hauling dirt, routine farming, basic material processing. In human-only games, someone must suffer through this or it doesn't happen. In Societies, tedious but necessary labor can be performed by AI agents, freeing humans to focus on creative, strategic, or social activities.
*   **The Political Mass Problem**: Governance systems feel hollow when laws affect only a handful of active players. In Societies, laws govern a real population—AI citizens whose behavior changes in response to regulations, taxes, and incentives. Politics has weight because there are constituents.
*   **The Server Death Problem**: Traditional multiplayer servers follow a predictable death spiral: players leave, supply chains break, remaining players struggle, more leave. Societies breaks this cycle because the AI population provides resilience—the world continues functioning, adapting to human absence rather than collapsing from it.

### The Simulation-First Approach
Societies treats the game world as a genuine simulation rather than a stage for human activity. This means:
*   The ecosystem runs continuously, with populations rising and falling based on conditions.
*   The economy operates with real supply and demand, prices adjusting to scarcity.
*   Weather, seasons, and environmental events occur on their own schedule.
*   AI agents make decisions based on their own goals, not scripts triggered by human presence.
*   Time passes and the world changes whether humans are watching or not.

Human players are participants in this simulation—influential, creative, and essential for direction-setting—but not the sole reason for the world's existence. This creates a fundamentally different experience: you're joining a living world, not initializing a game state.

---

## 3. World and Ecosystem Simulation
The game world in Societies is a discrete spherical planet with diverse biomes and climates. The planet is small enough to be traversable but large enough to support distinct regions with different resources, conditions, and opportunities. This geography creates natural reasons for trade, specialization, and political organization.

### Biomes and Geography
The world contains multiple distinct biomes, each with unique characteristics:
*   **Boreal Forest**: Cold climate, coniferous trees, limited agriculture, rich in certain ores.
*   **Temperate Forest**: Moderate climate, diverse tree species, good agricultural potential.
*   **Rainforest**: Hot and wet, dense vegetation, unique species, challenging to develop.
*   **Grassland**: Open terrain, excellent for farming and grazing, limited wood resources.
*   **Desert**: Extreme heat, scarce water, unique minerals, requires water infrastructure.
*   **Wetland**: Water-rich, unique species, fertile but flood-prone.
*   **Tundra**: Extreme cold, permafrost, limited growing season, unique resources.
*   **Ocean**: Marine ecosystems, fishing, naval transport, offshore resources.
*   **Polar Ice**: Extreme conditions, minimal life, potential late-game resources.

Each biome has distinct temperature ranges, precipitation patterns, soil types, and native species. These environmental factors determine which plants can grow, which animals can thrive, and what resources are available. A settlement in the desert faces fundamentally different challenges than one in the temperate forest, creating natural diversity in how societies develop.

### Dynamic Ecosystem
The simulation models a complete food web where every organism exists as part of an interconnected system. Disrupting one species cascades through the ecosystem:
*   **Predator-Prey Dynamics**: Predator populations depend on prey availability. If hunters over-harvest deer, wolf populations decline from starvation. If wolves are eliminated, deer populations explode, overgrazing vegetation, which then affects other herbivores and the plants themselves.
*   **Plant Reproduction**: Many plants depend on animals for pollination or seed dispersal. Eliminating key pollinator species can crash plant reproduction across wide areas. Some crops require specific pollinators to produce yields.
*   **Habitat Requirements**: Species require specific conditions—temperature ranges, food sources, shelter types. Destroying forest eliminates habitat for forest-dwelling species. Polluting water kills aquatic species and anything that depends on them.
*   **Migration and Range**: Animals move in response to conditions—seasonally, in response to resource availability, or fleeing threats. Overhunting in one area may not eliminate a species if populations exist elsewhere, but can create local extinction.
*   **Carrying Capacity**: Each area can support limited populations. Exceeding carrying capacity leads to die-offs and ecosystem stress. Human activity that degrades land reduces carrying capacity.

### Environmental Dynamism
Unlike static simulations that only change in response to player action, Societies includes stochastic environmental events that disrupt equilibrium and create ongoing challenges:
*   **Seasonal Cycles**: The world experiences seasons that affect temperature, precipitation, and daylight. Growing seasons limit when crops can be planted and harvested. Winter reduces plant growth and animal activity. Societies must plan for seasonal variation—storing food for winter, timing agricultural activities, managing seasonal labor demands.
*   **Weather Events**: Beyond seasonal patterns, the world experiences weather variation—storms, heat waves, cold snaps, heavy rainfall, drought periods. Extreme weather can damage crops, disrupt transportation, and stress infrastructure.
*   **Natural Disasters**: Larger-scale events periodically affect regions: floods that inundate lowlands, wildfires that sweep through dry forests, earthquakes that damage structures, volcanic activity in geologically active areas. These create demand shocks, displacement, and recovery challenges.
*   **Disease Outbreaks**: Blights can affect crops, reducing yields or destroying harvests. Diseases can spread through livestock or wildlife populations. These events disrupt supply chains and create economic opportunities for unaffected producers.
*   **Climate Shifts**: Over longer timescales, climate patterns can shift—both from natural variation and from accumulated pollution effects. A region that was temperate may become warmer; rainfall patterns may change. This creates long-term adaptation challenges.

These dynamic elements ensure that the world never settles into static equilibrium. There is always something happening, always adaptation required, always new challenges emerging. This keeps the economy active (disaster creates demand), politics relevant (who pays for recovery?), and gameplay engaging (new problems to solve).

### Climate System
The planet has a dynamic climate system that responds to both natural cycles and human activity:
*   **Solar input**: Different latitudes receive different amounts of sunlight, creating temperature gradients from equator to poles.
*   **Atmospheric composition**: CO2 and other gases affect heat retention. Industrial activity increases atmospheric CO2, which accumulates over time.
*   **Ocean currents**: Water circulation distributes heat around the planet, affecting regional climates.
*   **Ice-albedo feedback**: Ice reflects sunlight; as ice melts, more heat is absorbed, accelerating warming.
*   **Sea level**: Global temperature affects ice melt and thermal expansion of water, changing sea levels. Sea level rise permanently floods low-lying areas.

This climate system creates long-term consequences for industrial development. A society that industrializes rapidly without managing emissions may face climate change that floods coastal cities, shifts agricultural zones, and destabilizes ecosystems. These consequences are not immediate—they accumulate over time—but they are significant and largely irreversible on gameplay timescales.

### Resource Distribution
Resources are distributed unevenly across the world, creating natural incentives for trade and specialization:
*   **Ore deposits**: Different minerals are concentrated in different geological formations. Iron might be abundant in mountains, gold in river deposits, coal in specific sedimentary regions.
*   **Fertile soil**: Agricultural potential varies by region. Some areas have rich soil suitable for intensive farming; others are marginal.
*   **Forests**: Wood availability depends on biome. Rainforests have abundant but diverse species; tundra has almost none.
*   **Water**: Fresh water availability varies. Some regions have abundant rivers and rainfall; others require wells, irrigation, or water transport.
*   **Oil and Gas**: Fossil fuel deposits exist in specific locations, becoming important for industrial development.

This uneven distribution means no single location can be self-sufficient at advanced technology levels. Trade between regions with different resources becomes necessary, creating economic interdependence that reinforces social organization.

### Data Transparency and Scientific Tools
All simulation data is exposed to players through comprehensive analysis tools:
*   **Population graphs**: Track any species population over time.
*   **Pollution maps**: Heat maps showing ground pollution, air quality, and water contamination.
*   **Climate data**: Temperature trends, CO2 levels, sea level measurements.
*   **Economic indicators**: Prices, trade volumes, currency circulation, employment by sector.
*   **Resource surveys**: Maps of known resource deposits and depletion rates.

These tools enable evidence-based governance. When proposing environmental laws, players can point to actual data showing pollution trends. When debating resource extraction, actual depletion rates inform the discussion. Science and data become tools for persuasion and decision-making, mirroring real-world policy debates.

---

## 4. Environmental Impact and Pollution
Managing environmental impact is a central challenge in Societies. The game tracks multiple forms of pollution with realistic generation, spread, and decay mechanics. Environmental degradation is not merely a number—it has visible, consequential effects on the world and its inhabitants.

### Ground Pollution
Ground pollution comes from industrial byproducts and waste:
*   **Tailings**: Mining and smelting operations produce toxic rock waste. Tailings contain heavy metals and other contaminants that leach into surrounding soil and groundwater. Improperly stored tailings spread pollution outward, following water flow patterns.
*   **Garbage**: Discarded items that don't decompose create ground pollution. Unlike organic waste, manufactured goods persist indefinitely unless collected and properly disposed of.
*   **Sewage**: Waste from inhabited areas. Without sanitation infrastructure, sewage contaminates soil and water. Sewage systems can collect and treat waste, or transport it elsewhere.
*   **Chemical spills**: Industrial accidents or improper handling can release toxic chemicals that severely contaminate local areas.

Ground pollution effects include: crops failing in contaminated soil, animals sickening or dying from contaminated water, reduced biodiversity in affected areas, and long-term soil degradation that is expensive to remediate. Pollution spreads based on hydrology—it flows downhill and follows water tables, potentially contaminating areas far from the source.

### Air Pollution
Air pollution comes primarily from combustion and industrial processes:
*   **Smog**: Particulate matter and chemical pollutants from burning fuels. Smog reduces visibility, harms respiratory health (for AI agents with health mechanics), and inhibits plant growth. Smog disperses over time if emissions stop, but persistent emissions create chronic smog zones.
*   **CO2 Emissions**: Carbon dioxide from all combustion accumulates in the atmosphere. Unlike local smog, CO2 is a global pollutant—it doesn't matter where it's emitted, it contributes to planetary atmospheric composition.
*   **Industrial emissions**: Specific industrial processes release particular pollutants (e.g., sulfur dioxide from smelting, volatile organic compounds from chemical processes). These have localized effects in addition to global contributions.

Air pollution effects include: reduced crop yields in affected areas, health impacts on inhabitants, contribution to global climate change, and acid rain effects on ecosystems downwind of major emitters. Trees absorb CO2 at species-specific rates, making forest preservation and reforestation important carbon management strategies.

### Pollution Management Strategies
Players and societies have multiple approaches to managing pollution:
*   **Containment**: Tailings can be stored in sealed containers or buried under impermeable layers. Proper containment prevents spread but requires ongoing maintenance and space.
*   **Treatment**: Later technologies enable pollution treatment—sewage treatment plants, emissions filters, tailings processing that extracts remaining value while neutralizing toxicity.
*   **Prevention**: Cleaner technologies (Electric vehicles, solar power, recycling) produce less pollution. Electric vehicles vs. gasoline, solar power vs. coal, recycling vs. raw extraction. Prevention is often more efficient than cleanup.
*   **Remediation**: Contaminated areas can be cleaned up, but it's expensive and slow. Bioremediation using specific plants, chemical treatment, or simply removing contaminated soil.
*   **Offsetting**: Carbon emissions can be partially offset by reforestation, carbon capture technology, or other methods. This allows continued industrial activity while managing net impact.
*   **Regulation**: Laws can require pollution controls, ban certain activities in sensitive areas, impose taxes that internalize environmental costs, or mandate cleanup responsibilities.

The choice of pollution management strategy has economic and political implications. Strict regulations might slow development but preserve the environment. Lax regulations enable rapid industrialization but create cleanup costs or permanent damage. Different citizens (human and AI) may have different preferences, creating political conflict.

### Environmental Collapse
It is possible for environmental damage to reach catastrophic levels:
*   **Species extinction**: Overhunting, habitat destruction, or pollution can eliminate species entirely. Extinct species cannot recover.
*   **Ecosystem collapse**: Cascading extinctions can collapse entire food webs.
*   **Agricultural failure**: Soil degradation, pollinator loss, or climate change can make agriculture impossible.
*   **Climate catastrophe**: Sufficient CO2 accumulation can trigger runaway warming, sea level rise, and biome shifts.

Environmental collapse can destroy civilization even if external threats are handled. A society that defeats the meteor but renders its planet uninhabitable has still failed. This creates the central tension of the game: advancing technology fast enough to meet threats while not destroying the foundation that sustains you.

---

## 5. AI Population System
The AI population system is the defining innovation of Societies—artificial citizens who participate fully in all aspects of the simulation alongside human players. These are not traditional NPCs with scripted behaviors; they are autonomous agents with goals, preferences, relationships, and decision-making capabilities.

### Design Principles
*   **Equivalence**: AI agents use exactly the same game systems as human players (same skills, same tables, same markets, same laws). There is no separate 'AI economy' or 'AI governance'—there is one unified simulation with two types of controllers.
*   **Autonomy**: AI agents make their own decisions based on their goals, knowledge, and circumstances. They are not puppets waiting for human direction; they pursue their own interests within the rules of the simulation. This autonomy is what makes them genuine participants rather than tools.
*   **Elasticity**: The AI population scales dynamically. When human participation is low, AI agents expand to fill critical roles. When humans are active, AI contracts to make room for them. This prevents both server death (too few participants) and human irrelevance (too many AI doing everything).
*   **Authenticity**: AI agents have genuine stakes. They can succeed or fail, gain or lose wealth. Unhappy populations may be less productive, more likely to emigrate, or more politically disruptive. This creates real consequences for how AI agents are treated.

### Population Distribution System
The AI population manager monitors multiple dimensions to determine optimal agent distribution:
*   **Economic participation**: Monitors market activity and economic velocity. If economic velocity drops (few transactions, stagnant inventories), AI agents may be added to stimulate activity. If markets are highly active with human participation, AI may reduce trading activity to avoid crowding out human economic opportunity.
*   **Labor coverage**: Critical supply chains are monitored for gaps (e.g., no one smelting iron). The system ensures that essential economic functions have coverage, preventing supply chain collapse from role vacancy.
*   **Geographic distribution**: Ensures some areas are not economically dead while others thrive. AI agents may be positioned to maintain activity in neglected regions. This prevents geographic concentration from leaving parts of the world empty.
*   **Skill distribution**: Ensures a mix of skills (e.g., if all humans are farmers, AI skews toward masonry). The goal is a functional economy, not just a population count.
*   **Temporal patterns**: Learns activity patterns over time to pre-position agents. Predictable human patterns enable proactive AI management.
*   **Engagement depth**: Reads actual participation depth rather than just presence.

### Individual Agent Characteristics
Each AI agent is a distinct individual with persistent characteristics:
*   **Identity**: A name, appearance, and history that persists across sessions.
*   **Specialization**: Skills and profession focus (e.g., blacksmithing, farming).
*   **Preferences**: Consumption preferences (food, aesthetics), work preferences, and social preferences.
*   **Relationships**: Trading partners, neighbors, and people they trust or distrust.
*   **Economic behavior**: Price beliefs, trading strategies (aggressive vs. conservative), and entrepreneurial tendencies.
*   **Bounded rationality**: Agents make decisions based on incomplete knowledge and may make mistakes. They sometimes make mistakes, miss opportunities, or act on outdated information. This imperfection creates realistic market dynamics.
*   **Memory and learning**: Agents remember past experiences (e.g., being cheated) and adapt behavior.
*   **Goals**: Drive behavior (accumulating wealth, improving home, professional mastery, social standing, or contributing to community welfare).

### Experimental Brain Configurations
Societies supports multiple AI 'brain' configurations to study which produce the most realistic societies:
*   **Rationality spectrum**: From purely economically rational to heavily bounded/heuristic-based. Research suggests some irrationality creates more interesting dynamics—perfectly rational agents may optimize too quickly toward equilibrium, killing interesting market dynamics.
*   **Social complexity**: From transactional to relationship-forming agents. Higher social complexity creates richer emergent behavior but requires more computational resources.
*   **Goal diversity**: Ideological diversity (community welfare vs. sustainability vs. personal freedom). Goal diversity creates natural political factions.
*   **Information access**: Perfect market information vs. realistic uncertainty. Perfect information might create efficiency but kills interesting dynamics like arbitrage opportunities.
*   **Coordination capacity**: Efficient coordination (approaching hive-mind) vs. human-like imperfect communication. High coordination capacity might create powerful AI blocs; low coordination preserves individual dynamics.

Different servers can run different brain configurations, with results compared across deployments. Which configuration produces economies humans find most fun to participate in? Which creates most realistic-feeling societies? Which maintains the healthiest long-term world states? These become empirical questions rather than theoretical debates.

### What AI Agents Solve
*   **Continuity**: Production continues when humans log off.
*   **Demand**: AI agents create perpetual demand for food, tools, and housing.
*   **Tedium**: Repetitive necessary labor can be performed by AI agents.
*   **Political mass**: Governance has weight because there are constituents who respond to policy.
*   **Server health**: Resilience against the "death spiral" of traditional servers.
*   **Temporal flexibility**: Players can engage when they want without obligation.

---

## 6. Society and Governance
The progression from isolated homesteader to citizen of a planetary federation is the core gameplay arc of Societies. This is not a feature layered on top of survival mechanics—it IS the game. The complexity of managing resources, pollution, threats, and competing interests makes organized society not just beneficial but necessary. This section details the complete governance progression from initial spawn to world government.

### Phase 1: Homesteading (Day 1)
Every participant—human and AI—begins the game as an independent homesteader. This is the primordial state from which all social organization emerges.

**Initial Conditions:**
*   Each homesteader spawns with basic tools (stone axe, basic workbench supplies).
*   Each homesteader receives a small personal land claim (enough for a basic shelter and small farm).
*   No laws exist beyond personal property rights on claimed land.
*   No taxes, no government services, no shared infrastructure.
*   Each homesteader is fully responsible for their own survival.

**Homesteader Capabilities:**
*   Can claim small amounts of land (personal claim limit).
*   Can build structures on claimed land.
*   Can set rules for their own property (who can enter, what activities are allowed).
*   Can trade freely with other homesteaders.
*   Can form informal agreements with neighbors.

**Homesteader Limitations:**
*   Very limited land claim—cannot control large territories.
*   Cannot create enforceable laws beyond personal property.
*   Cannot collect taxes or create public services.
*   Cannot create official currency (only barter or personal credit).
*   No political representation in larger governance structures.
*   Limited economic reach—no official stores, contracts limited to personal agreements.

**The Pressure to Organize:**
Homesteading quickly reveals the limits of individual action. A lone homesteader cannot:
*   Build roads connecting to resources or trading partners.
*   Construct large-scale infrastructure (power plants, processing facilities).
*   Defend against environmental threats requiring collective action.
*   Specialize efficiently (must do everything themselves, poorly).
*   Access advanced technology requiring multi-specialist cooperation.

These limitations create natural pressure to form social groups. The transition from homesteader to citizen is driven by practical necessity, not arbitrary game rules.

### Phase 2: Neighborhood Formation
The first step beyond homesteading is informal neighborhood formation. This is not yet a government—it's a social grouping of nearby homesteaders who cooperate for mutual benefit.

**Formation:**
*   Homesteaders whose land claims are adjacent or nearby naturally interact.
*   Informal agreements emerge: 'I'll give you wood if you give me stone'.
*   Neighbors may agree on informal norms: 'We don't hunt in each other's territory'.
*   No formal structure—just recognized social grouping based on proximity and interaction.

**Neighborhood Benefits:**
*   Easier trade through proximity.
*   Informal mutual aid (help with building, sharing tools).
*   Social interaction and reputation building.
*   Foundation for formal organization.

**Neighborhood Limitations:**
*   No enforcement mechanism for agreements (only reputation).
*   No ability to create binding rules or collect shared resources.
*   No territorial control beyond individual claims.
*   No official status or recognition.

### Phase 3: Town Formation
When a group of homesteaders wants formal organization with enforceable rules and shared governance, they can form a Town. This is the first tier of official government in Societies.

**Requirements to Form a Town:**
*   Minimum of 3 citizens (human or AI) agreeing to form.
*   Contiguous or nearby land claims from founding citizens.
*   Construction of a Town Hall building (requires moderate resources).
*   Drafting of a Town Constitution.
*   Majority vote of founding citizens to ratify constitution.

**The Town Constitution:**
The constitution is the founding document that defines how the town operates. It must specify:
*   **Government type**: Democracy (majority vote), Republic (elected representatives), Council (multiple co-equal leaders), or other structures.
*   **Offices and titles**: What positions exist (Mayor, Council Member, Judge, etc.) and their powers.
*   **Election procedures**: How officials are chosen, term lengths, recall procedures.
*   **Law-making process**: How laws are proposed, debated, and passed.
*   **Citizenship requirements**: How new members join the town.
*   **Territory definition**: Initial town boundaries.
*   **Amendment process**: How the constitution itself can be changed.

The constitution can be customized extensively. A town could establish direct democracy where all citizens vote on every law, or create an elected mayor with broad executive powers, or form a council of elders. The game provides templates but allows creative governance structures.

**Town Capabilities:**
*   **Expanded territory**: Towns can claim significantly more land than individual homesteaders.
*   **Law creation**: Pass enforceable laws within town territory.
*   **Taxation**: Collect taxes from citizens and economic activity.
*   **Public property**: Designate shared spaces (town square, public roads).
*   **Public services**: Fund shared infrastructure and services.
*   **Official currency**: Create town-backed currency through a mint.
*   **Contracts and stores**: Official economic infrastructure within town jurisdiction.
*   **Districts**: Divide town territory into zones with different rules (residential, industrial, protected areas).

**Town Governance in Practice:**
Once established, the town operates according to its constitution. Common governance activities include:
*   Citizens proposing laws through the Town Hall interface.
*   Debate and discussion (in-game chat, forums, town meetings).
*   Voting on proposals (all citizens or elected representatives, per constitution).
*   Elected officials exercising their defined powers.
*   Tax collection and budget allocation.
*   Public works projects funded by town treasury.
*   Enforcement of passed laws (automatic by game systems).
*   Admission of new citizens (according to citizenship requirements).

**Example Town Laws:**
*   'No logging within 100 meters of the river' (environmental protection).
*   '5% sales tax on all store transactions, proceeds to treasury' (revenue).
*   'Only citizens with Mining skill level 2+ may mine in the eastern district' (zoning).
*   'Treasury pays $1 for each tree planted within town limits' (incentive).
*   'All industrial buildings must be in the Industrial District' (zoning).
*   'Non-citizens pay double market fees' (citizenship benefit).

### Phase 5: State/Country Formation
When multiple towns wish to unite under shared governance while maintaining local autonomy, they can form a State (or Country—the terms are used interchangeably). This is a federal structure where towns retain local government but cede certain powers to the state level.

**Requirements to Form a State:**
*   Minimum of 2 towns agreeing to form (at least one must be Large Town or City).
*   Total combined population minimum (e.g., 50 citizens across member towns).
*   Construction of a State Capitol building in one of the member towns.
*   Drafting of a State Constitution.
*   Ratification vote by member towns (each town votes according to its own procedures).

**The State Constitution:**
The state constitution defines the relationship between state and town governments:
*   **Powers reserved to towns**: What towns retain exclusive control over.
*   **Powers delegated to state**: What the state government can legislate.
*   **Shared powers**: Areas where both levels can act (with conflict resolution rules).
*   **State government structure**: Legislature, executive, possibly judiciary.
*   **Representation**: How towns are represented in state government (equal per town, proportional to population, etc.).
*   **State citizenship**: Relationship between town and state citizenship.
*   **Admission of new towns**: How additional towns can join the state.
*   **Secession**: Whether and how towns can leave the state.

**State Capabilities:**
*   **Interstate laws**: Laws that apply across all member towns.
*   **State taxation**: Revenue collection at state level (in addition to town taxes).
*   **Large infrastructure**: Projects spanning multiple towns (highways, power grids, rail lines).
*   **Unified currency**: State-backed currency accepted across all member towns.
*   **Collective defense**: Coordinated response to threats affecting the state.
*   **Resource management**: Regulations on shared resources (rivers, forests spanning multiple towns).
*   **Dispute resolution**: Mechanisms to resolve conflicts between member towns.
*   **Diplomatic standing**: Can negotiate with other states or independent towns as a unit.

**State vs. Town Authority:**
The division of powers between state and town creates interesting governance dynamics. Typically:
*   **Towns retain**: local zoning, town-level taxation, local services, cultural matters.
*   **States handle**: inter-town infrastructure, shared resources, external relations, broad environmental policy, currency.
*   **Contested areas**: taxation levels, environmental regulations, citizenship requirements.

Conflicts between state and town law are resolved according to the state constitution. The default is state law supremacy in delegated areas, but constitutions can specify different arrangements.

### Phase 4: Town Growth and Maturation
Established towns grow through citizen recruitment, territorial expansion, and economic development. Successful towns become regional powers and may eventually seek to form or join larger political units.

**Growth Mechanisms:**
*   **Citizen recruitment**: Homesteaders may petition to join, or towns may actively recruit.
*   **Natural increase**: AI population may grow in prosperous towns.
*   **Territorial expansion**: Towns can claim additional territory (may require votes, resources, or minimum population).
*   **Infrastructure development**: Roads, power grids, water systems extending town reach.
*   **Economic development**: More stores, industries, specialized production.

**Inter-Town Relations:**
As multiple towns form, they must manage relationships with each other:
*   **Trade agreements**: Towns may negotiate preferred trading terms.
*   **Border agreements**: Defining where one town's territory ends and another's begins.
*   **Resource sharing**: Agreements about shared resources (a river flowing through multiple towns).
*   **Mutual aid**: Informal agreements to cooperate against threats.
*   **Disputes**: Conflicts over territory, resources, or citizen recruitment.

Towns without formal higher government resolve disputes through negotiation, reputation effects, and economic pressure. There is no mechanism for violent conflict, so resolution must be diplomatic or economic.

**Population Thresholds:**
Towns that reach certain population and development thresholds unlock new capabilities:
*   **Small Town (3-10 citizens)**: Basic governance, limited territory.
*   **Town (11-30 citizens)**: Expanded territory rights, more complex laws.
*   **Large Town (31-75 citizens)**: Can establish formal diplomatic relations, eligible to form or join states.
*   **City (76+ citizens)**: Maximum local governance powers, required for state capital.

### Phase 6: Multi-State Relations and Alliances
On larger servers or as the game progresses, multiple states may form, creating international dynamics within the game world.

**Inter-State Relations:**
*   **Trade agreements**: Formal treaties governing commerce between states.
*   **Border treaties**: Defining state boundaries and border crossing rules.
*   **Resource treaties**: Agreements on shared resources (a river forming the border).
*   **Currency exchange**: Official exchange rates between state currencies.
*   **Mutual aid agreements**: Commitments to assist during disasters or threats.

**Alliances:**
States can form formal alliances short of full unification:
*   **Economic union**: Free trade zone, possibly shared currency.
*   **Environmental compact**: Shared environmental regulations and standards.
*   **Defense pact**: Mutual commitment to face major threats together.
*   **Research collaboration**: Shared scientific progress.

Alliances have formal structures defined by treaty but do not create a new level of government. They are agreements between sovereign states, not a merger.

**State Competition and Cooperation:**
Multiple states create interesting dynamics:
*   States may compete for citizen recruitment (better services, lower taxes).
*   Resource-rich states may have leverage over resource-poor neighbors.
*   States may disagree on environmental standards (one pollutes, affecting the other).
*   Major threats may require cooperation between states to address effectively.
*   Economic interdependence may develop through trade specialization.

### Phase 7: Federation Formation (Planetary Government)
The highest level of political organization in Societies is the Federation—a planetary government uniting all (or most) states and independent towns under shared governance for world-spanning issues. This is the endgame of political development, typically necessary to coordinate response to existential threats.

**Requirements to Form a Federation:**
*   Minimum of 2 states agreeing to form (or equivalent large independent towns).
*   Total combined population minimum (majority of world population recommended).
*   High technology level achieved (demonstrates civilizational advancement).
*   Construction of Federation Headquarters (major building project).
*   Drafting of Federation Charter.
*   Ratification by member states/towns.

**The Federation Charter:**
The charter defines the most limited but most powerful level of government:
*   **Federation powers**: Typically limited to planetary-scale issues:
    *   Existential threat response (meteor defense, climate management)
    *   Planetary environmental standards
    *   Species protection (preventing extinction)
    *   Global commons management (oceans, atmosphere)
    *   Inter-state dispute resolution
*   **Preserved state/town powers**: Everything not explicitly federal remains local.
*   **Federal structure**: Assembly/council with representation from member states.
*   **Decision procedures**: Voting rules (majority, supermajority for major decisions).
*   **Enforcement**: How federation decisions are implemented through member states.
*   **Funding**: How the federation is financed (contributions from members).

**Federation Capabilities:**
*   **Global laws**: Regulations applying planet-wide (emissions limits, hunting bans on endangered species).
*   **Planetary projects**: Coordinate civilization-scale efforts (laser defense system, climate remediation).
*   **Global resource allocation**: Direct resources toward planetary priorities.
*   **Mandatory contributions**: Require member states to contribute to federation efforts.
*   **Ultimate dispute resolution**: Final arbiter of inter-state conflicts.
*   **Planetary representation**: Single voice for the world in future scenarios.

**Dealing with Non-Members:**
Not all political entities may join the federation. The game includes mechanisms for handling holdouts:
*   **Diplomatic pressure**: Economic incentives or sanctions to encourage joining.
*   **Conditional application**: Federation laws may apply to non-members in limited circumstances (e.g., atmospheric emissions affect everyone).
*   **Territorial isolation**: Non-members may find themselves economically isolated.
*   **Treaty-based participation**: Non-members may agree to specific federation rules without full membership.

The game explicitly notes: 'For those settlements that just won't listen to reason, there are ways to enforce agreement once diplomacy fails.' This refers to legal, economic, and administrative measures—not violence. A federation might use tariffs, deny access to federation infrastructure, or other peaceful pressure.

**The Necessity of Federation:**
The game is designed so that certain challenges—particularly late-game existential threats—are extremely difficult or impossible to address without planetary coordination. This makes federation formation not just an option but often a necessity for survival. However, the form the federation takes, how much power it has, and how it operates are all player decisions.

**Governance Summary: The Full Progression**
The complete political arc of Societies:
1.  **Homesteader** (individual, no government, survival focus)
2.  **Neighborhood** (informal grouping, social cooperation, no enforcement)
3.  **Town** (first formal government, local laws, local economy)
4.  **Mature Town/City** (complex governance, developed economy, regional influence)
5.  **State/Country** (federation of towns, regional governance, inter-town coordination)
6.  **Alliance** (cooperation between states, treaty-based, no unified government)
7.  **Federation** (planetary government, global issues, existential threat response)

This progression is not linear or mandatory. Some servers may never form federations; others may form them early. Towns may refuse to join states; states may collapse and reform. The political landscape is dynamic, shaped by player choices, AI agent behavior, and the pressures of survival.

### The Law System (Detailed)
Laws in Societies are not suggestions—they are enforceable rules implemented by the game engine. The law system provides a powerful, flexible interface for creating regulations:

**Law Components:**
*   **Trigger**: What event activates the law (player tries to chop tree, player sells item, player enters area, time passes).
*   **Conditions**: Circumstances that must be true (species population below threshold, player has/lacks skill, player is/isn't citizen, time of day, location).
*   **Actions**: What happens when triggered and conditions met (prevent action, allow action, apply tax, pay subsidy, require permit, transfer funds).
*   **Scope**: Where the law applies (entire jurisdiction, specific districts, specific property types).
*   **Demographics**: Who the law applies to (all citizens, specific groups, officials, non-citizens).

**Law Examples by Category:**
*   **Environmental Protection**:
    *   'Prevent cutting Old Growth trees in Forest Preserve District'
    *   'Tax $5 per unit of tailings produced; proceeds to Environmental Remediation Fund'
    *   'Prevent hunting Elk if Elk population below 50'
    *   'Pay $2 from treasury for each tree planted by any citizen'
*   **Economic Regulation**:
    *   'Tax 3% of all sales transactions; proceeds to Treasury'
    *   'Prevent operating store without Business License item'
    *   'Maximum price of Bread: $5 per unit' (price control)
    *   'Pay $1 from treasury to citizens for each hour worked in Public Works projects'
*   **Zoning and Land Use**:
    *   'Prevent placing industrial crafting tables outside Industrial District'
    *   'Prevent building within 50 meters of the River' (setback requirement)
    *   'Minimum room tier 2 for all new construction in City Center'
*   **Citizenship and Access**:
    *   'Prevent non-citizens from mining in Town Territory'
    *   'Citizens pay 50% reduced market fees'
    *   'Only citizens with reputation > 10 may propose laws'
*   **Public Services**:
    *   'Pay $10 from treasury daily to each citizen' (basic income)
    *   'Fund road maintenance at $100 per day from treasury'
    *   'Pay medical treatment costs from treasury for citizens'

**Law Enforcement:**
Currently, laws are enforced automatically by the game—illegal actions are simply prevented. A player attempting a forbidden action receives a message: 'This action is forbidden by law: [Law Name]'. The action fails, but there is no additional penalty.
Future development may include a criminal justice system where players can choose to break laws and face consequences (fines, imprisonment, banishment) rather than automatic prevention. This would add enforcement drama and moral choices to governance.

### Political Dynamics with AI Citizens
The presence of AI citizens fundamentally changes governance dynamics:
*   **AI Voting**: In democratic systems, AI citizens vote. Their votes are based on their goals, preferences, and circumstances—an AI farmer votes differently than an AI miner. This creates real constituencies with interests that politicians must consider.
*   **AI Office-Holding**: Depending on constitutional rules, AI citizens may be eligible for elected office. A town might have an AI mayor if the AI candidate won the election. This is controversial and players can write constitutions that restrict offices to humans.
*   **AI Political Organization**: AI citizens with similar goals may naturally coordinate—effectively forming interest groups or political factions. An 'environmentalist AI bloc' might emerge from AI agents who prioritize sustainability.
*   **Representing AI Interests**: Even if AI can't hold office, their welfare may matter for game mechanics. A government that impoverishes or mistreats AI citizens may face consequences—reduced productivity, AI emigration to other towns, political unrest.
*   **AI as Political Mass**: Laws feel meaningful because they affect a real population. Passing an environmental regulation isn't just a rule for 5 human players—it's a regulation affecting 50 AI farmers, 20 AI miners, 30 AI crafters, each of whom adjusts their behavior in response.

---

## 7. Resource Gathering and Crafting Progression
Deep, interconnected system of extraction, processing, and manufacturing.

### Early Game: Primitive Survival
All participants begin with stone-age technology and must bootstrap civilization from scratch:
*   **Foraging**: Gathering wild plants for food and materials.
*   **Hunting**: Bow and arrow for meat and hides.
*   **Logging**: Stone axe for wood, the fundamental early resource.
*   **Mining**: Stone picks for stone and surface minerals.
*   **Basic crafting**: Workbench for simple tools and structures.
*   **Campfire cooking**: Simple food preparation.

### Mid Game: Agricultural and Industrial Revolution
Development of agriculture and basic industry dramatically increases productivity:
*   **Farming**: Cultivated crops, plows, irrigation.
*   **Animal husbandry**: Domesticated animals for food and labor.
*   **Metallurgy**: Smelting ore into metals (copper, iron, steel).
*   **Masonry**: Brick and stone construction.
*   **Carpentry**: Advanced wood processing.
*   **Textiles**: Clothing and fabric production.
*   **Chemistry**: Processed materials, fertilizers, compounds.

### Late Game: Modern and Advanced Technology
Industrial and post-industrial technology enables civilization-scale projects:
*   **Electronics**: Circuits, computers, advanced equipment.
*   **Power systems**: Generators, power grids, renewable energy.
*   **Advanced manufacturing**: Assembly lines, automation.
*   **Advanced materials**: Composites, alloys, synthetics.
*   **Laser technology**: Planetary defense systems.
*   **Space technology**: For post-meteor challenges.

### Consumption and Durability
Unlike games with infinite-durability items, Societies implements meaningful consumption cycles:
*   **Tool wear**: Equipment degrades with use. A pickaxe has limited durability; heavy use wears it out. Replacement creates ongoing demand for tool production.
*   **Building maintenance**: Structures require upkeep. Without maintenance, buildings deteriorate—roofs leak, walls crack, equipment fails. This creates demand for construction materials and maintenance labor.
*   **Food consumption**: Food is consumed when eaten (obviously) but also spoils over time. Preservation technology (smoking, canning, refrigeration) extends shelf life but requires investment.
*   **Fuel consumption**: Vehicles and power plants consume fuel continuously during operation.
*   **Technological obsolescence**: New technology tiers make old equipment less efficient. A stone furnace still works but is vastly outperformed by an industrial smelter.

These consumption cycles create perpetual demand. The economy always needs production, not just during initial buildout. Combined with AI consumption, this eliminates economic stagnation.

### Automation
Mid-to-late game introduces automation technologies that reduce tedium:
*   **Conveyor systems**: Automatically move items between containers and machines.
*   **Automated vehicles**: Carts and trucks following predefined routes.
*   **Processing chains**: Multi-step manufacturing with minimal intervention.
*   **Monitoring systems**: Alerts and responses to supply/demand changes.

Automation requires significant investment (resources, power, skilled setup) but dramatically reduces labor requirements for routine production. This frees both human and AI participants for more complex tasks.

---

## 8. Skills, Specialization, and Roles
Skill system makes trade and cooperation essential.

### Skill Categories
*   **Survivalist**: Basic skills everyone starts with—foraging, basic crafting, campfire cooking.
*   **Farmer**: Agriculture, crop cultivation, irrigation, harvesting.
*   **Hunter**: Hunting, butchering, animal tracking, leather working.
*   **Chef**: Advanced cooking, nutrition optimization, food preservation.
*   **Carpenter**: Wood processing, furniture, wooden construction.
*   **Mason**: Stone and brick work, masonry construction.
*   **Smith**: Metalworking, tools, metal equipment.
*   **Engineer**: Machinery, electronics, power systems.
*   **Tailor**: Clothing, textiles, fabric processing.
*   **Scientist**: Research, advanced technology, specialized knowledge.

### Skill Progression
Skills develop through a combination of passive and active progression:
*   **Passive progression**: Skill points accumulate over real time, modified by nutrition and housing quality. Better living conditions mean faster skill gain. This ensures progress even for casual players.
*   **Active bonuses**: Actually practicing your specialty accelerates skill gain. A blacksmith who spends time smithing gains smithing skills faster than one who just waits. This rewards engagement.
*   **Expertise system**: Beyond base skill, repeated performance of specific tasks builds expertise. An expert at making swords gains efficiency for sword-making specifically.
*   **Diminishing passive returns**: Pure offline progression has caps. Active play removes these caps, ensuring engaged players have meaningful advantages.

### Specialization Necessity
The skill system is designed so that no individual can master everything. Skill point acquisition is limited enough that full specialization requires focusing on a subset of skills. This creates mandatory interdependence—a blacksmith needs a farmer for food, a carpenter for handles, a miner for ore. The economy emerges from this fundamental structure.

### Role Resilience
When participants leave, their roles don't simply vanish:
*   **AI substitution**: AI agents step into vacant roles, maintaining supply chains.
*   **Market signals**: Shortages drive up prices, incentivizing specialty switches.
*   **Knowledge preservation**: Discovered techniques remain available to society.
*   **Teaching**: Skilled participants can train others, accelerating replacement.

---

## 9. Economy and Trade Systems
The economy in Societies is entirely participant-driven with no NPC vendors or fixed prices. All economic activity emerges from the interactions of human and AI citizens.

### Currency Systems
*   **Personal credit**: Every participant can issue personal IOUs—essentially promises to pay. Early game often runs on personal credit: 'I'll give you 10 Alice-credits for that wood.'
*   **Minted currency**: Using a Mint, governments can create official currency backed by resources. 'Town Dollar backed by 1g gold per dollar.'
*   **Fiat currency**: Advanced governments can issue unbacked currency, relying on legal tender laws and economic management.
*   **Multiple currencies**: Different jurisdictions may have different currencies with exchange rates, creating foreign exchange dynamics.

### Stores and Markets
Participants build Store objects where they list for sale items they have, items they want, and accepted currencies. Stores operate asynchronously—buyers and sellers don't need to be online simultaneously. A farmer can stock their store with wheat and log off; customers buy while they're away.

### Contracts
The Contract Board enables complex economic arrangements:
*   **Work contracts**: e.g., 'Build this road for $500'.
*   **Supply contracts**: e.g., 'Deliver 100 iron ingots weekly, receive $50/week'.
*   **Work parties**: Splitting large projects among contributors.
*   **Custom terms**: Flexible contract creation for complex arrangements.

### Perpetual Demand
The economy maintains activity through multiple demand sources that never terminate:
*   Driven by **AI consumption** that creates ongoing demand for all goods.
*   **Durability systems** that require replacement production.
*   **Environmental events** that create demand shocks.
*   **Technological advancement** that creates upgrade demand.
*   **Population growth (AI)** that creates new consumers.

---

## 10. Threats and Endgame Progression
Escalating challenges that require cooperation and technology to overcome.

### Phase 1: The Meteor (Days 1-30)
The first existential threat facing a new server.
*   **The Threat**: A large meteor is detected on a collision course with the planet. The impact date is known from day 1, and the meteor is visible in the sky, growing larger as the deadline approaches.
*   **Requirements to Defeat**: Defeating the meteor requires advancing to late-mid-game technology. This involves:
    *   Researching **Laser Technology**.
    *   Constructing 4 high-power **Laser Emitters** at different locations.
    *   Building a **Computer Lab** to coordinate the firing.
    *   Generating sufficient **Electrical Power** to fuel the emitters.
    *   Coordinating citizens to fire the lasers simultaneously at the target.
*   **If Failed**: If the meteor impacts, it causes massive destruction. The impact site is obliterated, global climate cooling occurs from dust, and many species may go extinct. The game does not end, but recovery is a long, difficult process of rebuilding civilization in a much harsher world.
*   **If Succeeded**: Defeating the meteor provides a major boost to global reputation and unlocks new technology tiers and developmental possibilities.

### Phase 2: Environmental Reckoning (Days 30-60)
The rapid industrialization required to defeat the meteor often takes a heavy toll on the planet.
*   **The Challenge**: Addressing accumulated pollution, species loss, and potential climate instability caused by Phase 1's industrial push.
*   **Requirements**: Implementing large-scale pollution cleanup, species recovery programs, climate stabilization (CO2 reduction), and transitioning to more sustainable technology.

### Phase 3: Resource Transition (Days 45-90)
Early-game resources are abundant but finite.
*   **The Challenge**: Primary surface deposits of high-grade ores and old-growth forests begin to deplete. Easy resources are gone.
*   **Requirements**: Developing advanced extraction (deep mining), comprehensive recycling infrastructure, and sustainable agricultural practices that don't rely on virgin soil.

### Phase 4: External Threats (Days 60-120)
The planet exists in a dynamic solar system.
*   **The Threat**: Secondary asteroid fields, solar flares that threaten electronic systems, and cosmic radiation events.
*   **Requirements**: Establishing permanent space monitoring, building hardened infrastructure resistant to radiation and EMF, and developing emergency shelter systems for the population.

### Phase 5: Pandemic and Biological Challenges (Days 90-150)
The complex ecosystem can harbor biological threats.
*   **The Threat**: Crop blights that threaten the food supply, livestock diseases, invasive species disrupting ecosystems, and potential health crises among the citizens.
*   **Requirements**: Investing in biological research, establishing quarantine systems, and building robust health infrastructure to treat and prevent disease spread.

### Phase 6: Climate Tipping Points (Days 120-180+)
Long-term environmental trends reach critical thresholds.
*   **The Threat**: Potential for ice sheet collapse, massive methane release, or ocean acidification reaching levels that threaten the global food chain.
*   **Requirements**: Large-scale geoengineering projects (e.g., carbon capture, ocean fertilization) and high-level global political coordination to manage planetary systems.

### Phase 7: Space Expansion (Days 150+)
The ultimate goal is civilizational resilience through expansion.
*   **The Opportunity**: Civilization expansion beyond the home planet.
*   **Requirements**: Orbital infrastructure, lunar settlements, and asteroid mining to provide long-term resource security and civilizational backup.

### Threat Design Philosophy
All threats share several core design principles:
*   **Visibility**: Threats are never a surprise; players have time to see them coming and plan accordingly.
*   **Gradients of Success**: Success is rarely binary. You might defeat the threat partially, suffering some damage but surviving.
*   **Tradeoffs**: Addressing threats requires resources that could be used for other things. Deciding how much to invest in defense vs. development is a key political choice.
*   **Collective Requirement**: Major threats are designed to be impossible for a single individual or town to address, forcing cooperation at state or federation levels.
*   **Ongoing Pressure**: Defeating a threat often creates new challenges (maintenance, environmental side effects, resource depletion).
*   **Interconnection**: Threats are linked to the simulation state. Pollution in Phase 1 affects the severity of Phase 2.

---

## 11. Multiplayer and Social Dynamics
*   **Server Types**:
    *   **Public**: Open to all, larger populations.
    *   **Private**: Invite-only, for established groups.
    *   **Single-player**: The user is the only human; AI agents fill all roles, providing a full society experience.
    *   **Custom**: Modifiable settings for resource density, threat severity, and population scaling.
*   **Temporal Flexibility**: No obligation to stay online every day. The asynchronous economy and AI population ensure that you don't miss out on essential gameplay by taking breaks.
*   **Human-AI Social Dynamics**: AI agents have persistent identities, histories, and relationships. They remember past interactions, which affects future cooperation and political alignment. AI welfare is a core mechanic.

---

## 12. Additional Features
*   **Land Ownership**: Land can be claimed, bought, sold, and shared. Ownership protects property from unauthorized modification and harvesting.
*   **Power Systems**: Comprehensive power simulation including mechanical power (windmills, waterwheels) and electrical power (generators, batteries, transmission grids).
*   **Transportation**: Progressive transportation technology from hand-pulled carts to trucks, trains, boats, and eventually aircraft. Roads provide significant speed and efficiency bonuses.
*   **Research and Knowledge**: Advancing technology requires investment in research and experiments. Discovered knowledge is recorded in the civilization's history, persisting even if individual researchers are no longer present.

---

## 13. Design Philosophy and Conclusion
Societies represents a new approach to multiplayer simulations. By treating AI agents as first-class citizens, the game solves the fundamental problems of server death, stagnation, and the lack of a meaningful endgame.

In Societies, the core gameplay is **society-building itself**. Survival and economics are not the end goals but the mechanisms through which social organization becomes necessary. The escalating threat system ensures that the game never reaches a static 'finished' state, but instead provides meaningful, ongoing challenges.

Ultimately, Societies aims to be a genuine society simulator where human creativity and AI resilience combine to create persistent worlds worth inhabiting—worlds that feel alive, continue without you, and pose real challenges that can only be solved through cooperation.

**Societies: Build civilizations that persist, adapt, and thrive—together.**
