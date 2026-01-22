# Societies Simulation — Development Roadmap

> **Version Legend**: V2 (Complete) | V3 (Agent Brain Overhaul) | V4+ (Future)
> **Priority Legend**: P0 (Critical/Blocking) | P1 (Core Features) | P2 (Important) | P3 (Nice to Have) | P4 (Low Priority/Maintenance)

---

# ═══════════════════════════════════════════════════════════════════
# VERSION 3 — AGENT BRAIN ARCHITECTURE OVERHAUL
# ═══════════════════════════════════════════════════════════════════
#
# GOAL: Transform agents from PASSIVE participants (reacting to 
# system-generated activities) into AUTONOMOUS actors (generating 
# their own goals and using markets/contracts as tools).
#
# CORE INSIGHT: Currently, JobBoardSystem posts activities FOR agents,
# generate_daily_contracts() randomly creates contracts, and agents
# just react. Agents should WANT things and pursue them.
# ═══════════════════════════════════════════════════════════════════

## P0 — Agent State Expansion (Foundation)

### P0a — Personal Identity & Home State
> Agents need state to track their personal territory, owned structures,
> and sense of "home" before we can implement homestead behavior.

- [ ] **Add `home_pos: Vector2i` to Agent class** — Stores the agent's claimed home tile position. Initialize to `Vector2i(-1, -1)` for homeless agents. This becomes the anchor point for all homestead goals.
  - File: `sim/agent.gd`
  - Add after line ~45 (near claim state)
  - Include in `to_dict()` and `from_dict()` serialization

- [ ] **Add `owned_structures: Array[int]` to Agent class** — Array of structure IDs that this agent personally owns (stockpiles, shelters, workshops). Distinct from faction-owned structures.
  - File: `sim/agent.gd`
  - Add helper methods: `has_personal_stockpile() -> bool`, `has_personal_shelter() -> bool`

- [ ] **Add `personal_stockpile_id: int` and `shelter_id: int` to Agent class** — Quick references to primary personal structures (first stockpile, first shelter). Set to -1 if none.
  - File: `sim/agent.gd`
  - These are convenience shortcuts; `owned_structures` is the canonical list

- [ ] **Add helper method `has_home() -> bool` to Agent class** — Returns `home_pos != Vector2i(-1, -1)`. Used by HomesteadPlanner to determine if agent needs to establish home.
  - File: `sim/agent.gd`

---

### P0b — Career & Specialization State
> Agents need state to track their career path, specialization goals,
> and skill-based identity for the CareerPlanner.

- [ ] **Add `career_type: String` to Agent class** — Current career identity: "none", "gatherer", "logger", "miner", "craftsman", "smith", "trader", "farmer". Initialize to "none" for new agents; replaces static `role` over time.
  - File: `sim/agent.gd`
  - Initially keep `role` for backwards compatibility; deprecate in V4

- [ ] **Add `career_goals: Array[Dictionary]` to Agent class** — Career-specific progression goals. Example: `[{type: "ACQUIRE_TOOL", item: "Axe"}, {type: "SECURE_RESOURCE_ACCESS", resource_type: "tree"}]`
  - File: `sim/agent.gd`
  - Serialization: include in `to_dict()` / `from_dict()`

- [ ] **Add `preferred_resource: String` to Agent class** — The resource type this agent prefers to gather based on location/skills. Used by CareerPlanner to suggest specialization.
  - File: `sim/agent.gd`
  - Set dynamically based on nearest abundant resource at spawn or career change

---

### P0c — Long-Term Planning State
> Agents need persistent state for multi-day goals that survive
> across ticks and even serialization/deserialization.

- [ ] **Add `long_term_goals: Array[Dictionary]` to Agent class** — Persistent goals that span multiple days. Schema: `{type: String, target: Variant, started_tick: int, deadline_tick: int, progress: float, sub_goals: Array}`
  - File: `sim/agent.gd`
  - Example: `{type: "SAVE_FOR_AXE", target: 150, started_tick: 100, progress: 0.6}`
  - Include full serialization support

- [ ] **Add `market_price_memory: Dictionary` to Agent class** — Remembered prices for items: `{item_name: {last_price: int, last_tick: int}}`. Used for market intelligence.
  - File: `sim/agent.gd`
  - Capacity limit: max 20 items remembered

---

### P0d — Market Intention State
> Agents need to track WHY they're going to the market so behavior
> is purposeful, not arbitrary.

- [ ] **Add `market_intentions: Array[Dictionary]` to Agent class** — Why the agent is going to/at the market. Schema: `{type: "BUY_SPECIFIC"|"SELL_SURPLUS"|"FIND_CONTRACT"|"CHECK_PRICES", item: String, qty: int, max_price: int}`
  - File: `sim/agent.gd`
  - Cleared when agent leaves market area or intentions fulfilled

- [ ] **Add `last_market_visit_tick: int` to Agent class** — Track when agent last visited market for cooldown/frequency logic.
  - File: `sim/agent.gd`

---

## P0 — Planner Infrastructure (Foundation)

### P0e — Planner Interface & Registration
> Create the infrastructure for the new planner system before
> implementing individual planners.

- [ ] **Create `IAgentPlanner` interface class** — Base class for all planners. Defines: `func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool` and `func get_priority() -> int`
  - File: `sim/brains/planners/i_agent_planner.gd` (NEW)
  - All planners extend this interface

- [ ] **Create `PlannerContext` container class** — Bundles all context needed by planners: world, market, contracts_system, tuning, recipes, state. Avoids long parameter lists.
  - File: `sim/brains/planner_context.gd` (NEW)
  - Immutable struct passed to all planners

- [ ] **Refactor DefaultBrain to use planner registry** — Replace hard-coded planner calls with a registered list of planners sorted by priority.
  - File: `sim/brains/default_brain.gd`
  - Method: `_init()` registers planners; `_generate_high_level_goals()` iterates by priority

---

## P1 — NeedsPlanner (Replaces SurvivalPlanner)

### P1a — Proactive Food Buffer Maintenance
> Agents should gather food BEFORE they're hungry, not just react
> when hunger drops below threshold.

- [ ] **Create `needs_planner.gd`** — New planner replacing `survival_planner.gd`. Handles all agent needs with both reactive and proactive modes.
  - File: `sim/brains/planners/needs_planner.gd` (NEW)
  - Keep `survival_planner.gd` temporarily for reference; delete after migration

- [ ] **Implement `get_interrupt_action()` for critical survival** — Same as current: if hunger < 15 and has food, eat immediately. If stamina <= 0, sleep. Returns action directly (not goal).
  - File: `sim/brains/planners/needs_planner.gd`
  - This is the PANIC layer that bypasses goal system

- [ ] **Implement `maybe_add_critical_goal()` for reactive needs** — If hunger < 50, push EAT_FOOD or OBTAIN_ITEM goal. If stamina < 20, push REST goal. This is the current behavior.
  - File: `sim/brains/planners/needs_planner.gd`

- [ ] **Implement `maybe_add_proactive_goal()` for buffer maintenance** — If hunger > 50 BUT food_inventory < `proactive_food_buffer` (default 5), push MAINTAIN_FOOD_BUFFER goal.
  - File: `sim/brains/planners/needs_planner.gd`
  - New tuning param: `proactive_food_buffer: int = 5`
  - Goal: `{type: "MAINTAIN_FOOD_BUFFER", target_qty: 5, is_goal: true}`

- [ ] **Add `MAINTAIN_FOOD_BUFFER` goal type to DefaultBrain** — Process goal by pushing OBTAIN_ITEM sub-goal for Berries or checking personal stockpile.
  - File: `sim/brains/default_brain.gd`
  - Add to `_is_goal_complete()` and `_process_goal()`

- [ ] **Add helper `get_food_inventory(agent) -> int`** — Returns total edible items: Berries + CookedMeal. Used by proactive goal check.
  - File: `sim/agent.gd` or `sim/brains/planners/needs_planner.gd`

---

### P1b — Stamina & Rest Optimization
> Agents should seek shelter for efficient rest, not just rest anywhere.

- [ ] **Implement shelter detection in NeedsPlanner** — If agent has personal shelter OR is near a public shelter, prefer EFFICIENT_REST goal over basic REST.
  - File: `sim/brains/planners/needs_planner.gd`
  - New goal: `{type: "EFFICIENT_REST", shelter_id: int, is_goal: true}`

- [ ] **Add shelter rest bonus to `_execute_sleep()`** — If agent is at a shelter structure, apply `shelter_rest_bonus` multiplier (default 1.5x) to stamina recovery.
  - File: `sim/actions.gd`
  - Check: `state.structures.get_structure_at(agent.pos_x, agent.pos_y)` for shelter

- [ ] **Add tuning param `shelter_rest_bonus: float = 1.5`** — Multiplier for stamina recovery when sleeping in shelter.
  - File: `sim/tuning.gd`

---

### P1c — Comfort & Social Needs
> Activate the unused comfort and social needs to drive non-survival behavior.

- [ ] **Implement comfort decay per tick** — Comfort decreases slowly over time. Being in owned shelter prevents decay. Being homeless accelerates decay.
  - File: `sim/systems/agents_system.gd`
  - New tuning: `comfort_decay_per_tick: float = 0.1`, `homelessness_comfort_penalty: float = 0.3`

- [ ] **Add comfort-based goals to NeedsPlanner** — If comfort < 30 and no shelter, push BUILD_PERSONAL_SHELTER goal.
  - File: `sim/brains/planners/needs_planner.gd`

- [ ] **Implement social need decay and faction affinity** — Social need decreases over time. Being in faction slows decay. Successful trades boost social satisfaction.
  - File: `sim/systems/agents_system.gd`
  - New tuning: `social_decay_per_tick: float = 0.05`

- [ ] **Add social-based goals to NeedsPlanner** — If social < 30 and no faction, push FIND_COMMUNITY goal (leads to faction joining or trade partner seeking).
  - File: `sim/brains/planners/needs_planner.gd`

---

## P1 — HomesteadPlanner (Personal Territory)

### P1d — Home Establishment
> Agents should claim personal territory and establish a base of operations.

- [ ] **Create `homestead_planner.gd`** — New planner handling personal territory goals.
  - File: `sim/brains/planners/homestead_planner.gd` (NEW)

- [ ] **Implement `ESTABLISH_HOMESTEAD` goal generation** — If agent.has_home() == false AND has_claim_tokens(), push goal to find and claim a 3x3 plot.
  - File: `sim/brains/planners/homestead_planner.gd`
  - Trigger: homeless + has resources to claim
  - New tuning: `homestead_claim_radius: int = 3`

- [ ] **Implement homestead site selection logic** — Score tiles based on: nearby resources, distance from other agents, unclaimed status, low pollution.
  - File: `sim/brains/planners/homestead_planner.gd`
  - Method: `_find_best_homestead_site(agent, world, tuning) -> Vector2i`

- [ ] **Process `ESTABLISH_HOMESTEAD` goal in DefaultBrain** — Break into sub-goals: GO_TO site, CLAIM_TILE for home, then CLAIM_TILE for buffer tiles up to radius.
  - File: `sim/brains/default_brain.gd`
  - On completion: set `agent.home_pos` to claimed tile

---

### P1e — Personal Structure Building
> Agents should build their own stockpiles and shelters.

- [ ] **Implement `BUILD_PERSONAL_STOCKPILE` goal generation** — If agent has home but no personal stockpile, and has materials (Planks x12, Stone x6), push build goal.
  - File: `sim/brains/planners/homestead_planner.gd`
  - New tuning: `personal_stockpile_planks: int = 12`, `personal_stockpile_stone: int = 6`

- [ ] **Implement `BUILD_PERSONAL_SHELTER` goal generation** — If agent has stockpile but no shelter, and has materials (Planks x20, Stone x10), push build goal.
  - File: `sim/brains/planners/homestead_planner.gd`
  - New tuning: `personal_shelter_planks: int = 20`, `personal_shelter_stone: int = 10`

- [ ] **Add personal structure building actions** — Similar to faction projects but with agent.id as owner instead of faction_id.
  - File: `sim/actions.gd`
  - New action types: `TYPE_BUILD_PERSONAL_STOCKPILE`, `TYPE_BUILD_PERSONAL_SHELTER`

- [ ] **Add personal structures to Structures system** — Extend `structures.gd` with methods: `get_agent_structures(agent_id) -> Array`, `add_personal_stockpile()`, `add_personal_shelter()`
  - File: `sim/structures.gd`

---

### P1f — Deposit Surplus to Personal Storage
> Agents should use their personal stockpile for storage.

- [ ] **Implement `DEPOSIT_SURPLUS` goal generation** — If agent has personal stockpile AND inventory exceeds threshold, push goal to deposit items.
  - File: `sim/brains/planners/homestead_planner.gd`
  - Threshold: total inventory items > 20

- [ ] **Add `TYPE_DEPOSIT_TO_PERSONAL_STOCKPILE` action** — Agent goes to their stockpile and deposits surplus items.
  - File: `sim/actions.gd`
  - Reuse stockpile deposit logic with agent's `personal_stockpile_id`

---

## P1 — EconomyPlanner Overhaul (Intent-Driven Contracts)

### P1g — Agent-Driven Contract Posting
> Agents should POST contracts when they NEED something they can't efficiently
> produce themselves, not randomly via generate_daily_contracts().

- [ ] **Add `_should_post_contract_for_item()` to EconomyPlanner** — Evaluate if agent should post contract: can't produce efficiently + not available on market + can afford payout.
  - File: `sim/brains/planners/economy_planner.gd`
  - Input: item needed, quantity, market state
  - Output: `{should_post: bool, payout: int, reason: String}`

- [ ] **Implement `POST_CONTRACT_FOR_NEED` goal type** — When agent needs item and _should_post_contract_for_item() returns true, create goal to post contract.
  - File: `sim/brains/planners/economy_planner.gd`
  - Goal data: `{type: "POST_CONTRACT_FOR_NEED", item: String, qty: int, payout: int}`

- [ ] **Add `TYPE_POST_CONTRACT` action** — Agent posts a contract to the contracts_system.
  - File: `sim/actions.gd`
  - Calls `contracts_system.post_contract()` with agent-determined payout

- [ ] **Deprecate random contract generation in `generate_daily_contracts()`** — Comment out or remove the random dice roll per agent. Replace with stub that allows faction/org contracts only.
  - File: `sim/contracts_system.gd`
  - Keep method signature for backwards compat; gut the random agent posting logic

---

### P1h — Contract Acceptance Evaluation
> Agents should evaluate contracts based on their actual capabilities,
> not just profit margin.

- [ ] **Refactor `find_best_contract()` to consider agent capabilities** — Score contracts based on: can agent actually produce the item? How long will it take? Does agent have tools?
  - File: `sim/contracts_system.gd`
  - Add: `_estimate_fulfillment_time(agent, contract, world) -> int`
  - Add: `_can_agent_fulfill(agent, contract, world, recipes) -> bool`

- [ ] **Add contract rejection reasons to agent memory** — Track why agent rejected a contract: "no_tools", "too_far", "low_profit", "no_resources". Use for career guidance.
  - File: `sim/agent.gd`
  - New: `contract_rejection_history: Array[Dictionary]` (capped at 10)

---

## P1 — MarketBehaviorPlanner (Intentional Market Visits)

### P1i — Market Intention System
> Agents should know WHY they're going to the market before going.

- [ ] **Create `market_behavior_planner.gd`** — New planner that generates market visit goals with explicit intentions.
  - File: `sim/brains/planners/market_behavior_planner.gd` (NEW)

- [ ] **Implement market intention gathering** — Before generating market goal, collect all reasons to visit: buy needs, sell surplus, find work, check prices.
  - File: `sim/brains/planners/market_behavior_planner.gd`
  - Method: `_gather_market_intentions(agent, market, tuning) -> Array[Dictionary]`

- [ ] **Implement `GO_TO_MARKET_WITH_INTENT` goal type** — Carries the list of intentions; agent behavior at market depends on intention list.
  - File: `sim/brains/default_brain.gd`
  - Goal data: `{type: "GO_TO_MARKET_WITH_INTENT", intentions: Array}`

- [ ] **Implement intention fulfillment at market** — When agent arrives at market, process intentions in order: place buy orders, place sell orders, browse contracts.
  - File: `sim/brains/default_brain.gd`
  - Clear intentions as fulfilled; leave market when done

---

### P1j — Price Intelligence
> Agents should remember prices and make informed decisions.

- [ ] **Implement price memory updates on market visit** — When agent visits market, update `market_price_memory` with current ref prices for relevant items.
  - File: `sim/brains/planners/market_behavior_planner.gd`
  - Trigger: on entering market tile or after trade

- [ ] **Use price memory in buy/sell decisions** — Compare current price to remembered price; avoid buying if price is unusually high.
  - File: `sim/brains/planners/economy_planner.gd`
  - Add: `_is_price_favorable(agent, item, current_price, intent) -> bool`

---

## P1 — CareerPlanner (Emergent Specialization)

### P1k — Career Assessment
> Agents should discover what they're good at and specialize.

- [ ] **Create `career_planner.gd`** — New planner handling skill development and specialization goals.
  - File: `sim/brains/planners/career_planner.gd` (NEW)

- [ ] **Implement `_assess_career()` method** — Evaluate agent's current career potential based on: skills, nearby resources, tools owned, workshop access.
  - File: `sim/brains/planners/career_planner.gd`
  - Returns: `{current: String, suggested: String, reasoning: String}`

- [ ] **Implement starter career suggestion for new agents** — If career_type == "none", suggest based on: nearest abundant resource + any starting tools.
  - File: `sim/brains/planners/career_planner.gd`

---

### P1l — Tool Acquisition Goals
> Career progression often requires specific tools.

- [ ] **Implement `ACQUIRE_TOOL` goal generation** — If suggested career requires tool agent doesn't have, push goal to acquire it.
  - File: `sim/brains/planners/career_planner.gd`
  - Example: Logger career → need Axe; Miner career → need Pickaxe

- [ ] **Add tool requirement mapping to CareerPlanner** — Dict mapping career → required tools: `{"logger": ["Axe"], "miner": ["Pickaxe"], "smith": ["Mallet"]}`
  - File: `sim/brains/planners/career_planner.gd`

---

### P1m — Workshop Access Goals
> Crafters need workshop access to be productive.

- [ ] **Implement `SECURE_WORKSHOP_ACCESS` goal generation** — If career requires workshop type agent lacks access to, push goal to build or find one.
  - File: `sim/brains/planners/career_planner.gd`
  - Check: nearby public workshops + personal workshops + faction workshops

- [ ] **Implement resource access goals** — Career-specific: loggers should claim/access tree areas; miners should claim/access ore deposits.
  - File: `sim/brains/planners/career_planner.gd`
  - Goal: `{type: "SECURE_RESOURCE_ACCESS", resource_type: "tree"}`

---

## P2 — CivicPlanner (Governance Participation)

### P2a — Faction Joining Logic
> Agents should join factions based on rational evaluation.

- [ ] **Create `civic_planner.gd`** — New planner handling faction membership and governance.
  - File: `sim/brains/planners/civic_planner.gd` (NEW)

- [ ] **Implement faction evaluation for joining** — Score factions by: treasury, member count, claimed resources, distance, trade policy toward factionless.
  - File: `sim/brains/planners/civic_planner.gd`
  - Method: `_score_faction_for_joining(agent, faction, world) -> float`

- [ ] **Implement `JOIN_FACTION` goal generation** — If agent is factionless, grievance > threshold OR social need low, and good faction nearby, push join goal.
  - File: `sim/brains/planners/civic_planner.gd`

---

### P2b — Voting Behavior
> Agents should vote on proposals based on their values.

- [ ] **Implement value-based vote decision** — When active proposal exists, decide vote based on: grievance (lower fines), eco_concern (stricter pollution), greed (lower taxes).
  - File: `sim/brains/planners/civic_planner.gd`
  - Method: `_decide_vote(agent, proposal) -> "for" | "against" | "abstain"`

- [ ] **Implement `VOTE_ON_PROPOSAL` goal generation** — If active proposal in agent's faction and hasn't voted, push voting goal.
  - File: `sim/brains/planners/civic_planner.gd`

---

### P2c — Law Proposal Generation
> Aggrieved agents should propose law changes.

- [ ] **Implement proposal generation logic** — If grievance > 0.5 about specific law (fines, taxes, permits), and is faction member, push proposal goal.
  - File: `sim/brains/planners/civic_planner.gd`
  - Method: `_get_grievance_source(agent, state) -> {law: String, value: int, desired: int}`

- [ ] **Add `PROPOSE_LAW_CHANGE` goal type** — Agent goes to faction meeting area (home_pos of faction?) and submits proposal.
  - File: `sim/brains/planners/civic_planner.gd`

---

## P2 — SocialPlanner (Relationships)

### P2d — Trust-Based Trading
> Agents should prefer trading with agents they trust.

- [ ] **Create `social_planner.gd`** — New planner handling relationships and social capital.
  - File: `sim/brains/planners/social_planner.gd` (NEW)

- [ ] **Implement trade partner preference** — When evaluating market orders, prefer orders from high-trust agents. Add trust bonus to contract evaluation.
  - File: `sim/brains/planners/economy_planner.gd`
  - Call: `agent.get_trust(other_id)` in scoring

- [ ] **Implement `FIND_TRADING_PARTNER` goal** — If no high-trust trade partners for needed resource, push goal to find one.
  - File: `sim/brains/planners/social_planner.gd`

---

### P2e — Trust Building
> Successful trades should build trust over time.

- [ ] **Enhance trust update on successful trade** — Update social memory with trade details, increment trade_count, boost trust.
  - File: `sim/market.gd` (in trade execution)
  - Call: `buyer.update_social_memory(seller.id, "trade", state.tick)`

- [ ] **Add trust decay for inactive relationships** — Slowly decay trust for agents not traded with recently.
  - File: `sim/systems/agents_system.gd` (daily tick)

---

## P2 — LongTermPlanner (Multi-Day Goals)

### P2f — Persistent Goal System
> Agents should maintain goals that span multiple days.

- [ ] **Create `long_term_planner.gd`** — New planner handling multi-day persistent goals.
  - File: `sim/brains/planners/long_term_planner.gd` (NEW)

- [ ] **Implement long-term goal persistence** — Goals survive serialization; tracked in `agent.long_term_goals`. Progress updated each tick.
  - File: `sim/brains/planners/long_term_planner.gd`

- [ ] **Implement `SAVE_FOR_ITEM` long-term goal** — Track savings progress toward expensive item (Axe = 150 coins). Modifies spending behavior.
  - File: `sim/brains/planners/long_term_planner.gd`
  - Behavior: reject low-priority purchases while saving

---

### P2g — Goal Progress & Completion
> Track progress on long-term goals and celebrate completion.

- [ ] **Implement progress tracking** — Update `progress` field each tick based on goal type. SAVE_FOR_ITEM: progress = money / target.
  - File: `sim/brains/planners/long_term_planner.gd`

- [ ] **Implement goal completion** — When goal complete, remove from list, add experience to agent, log event.
  - File: `sim/brains/planners/long_term_planner.gd`
  - Event: `{type: "long_term_goal_completed", agent_id, goal_type, duration_ticks}`

---

## P3 — DefaultBrain Integration

### P3a — Planner Priority System
> Integrate all planners with proper priority ordering.

- [ ] **Define planner priority constants** — PRIORITY_SURVIVAL = 100, PRIORITY_CRITICAL_NEEDS = 90, PRIORITY_COMMITMENTS = 80, etc.
  - File: `sim/brains/default_brain.gd`

- [ ] **Refactor `_generate_high_level_goals()` to use priority order** — Iterate planners by priority; first one that returns true wins.
  - File: `sim/brains/default_brain.gd`
  - Order: Needs (critical) → Commitments → LongTerm → Needs (proactive) → Career → Homestead → Economy → Civic → Social → Fallback

---

### P3b — Goal Processing for New Goal Types
> Ensure all new goal types are handled in _process_goal().

- [ ] **Add processing for all new goal types** — MAINTAIN_FOOD_BUFFER, ESTABLISH_HOMESTEAD, BUILD_PERSONAL_*, DEPOSIT_SURPLUS, ACQUIRE_TOOL, SECURE_WORKSHOP_ACCESS, JOIN_FACTION, VOTE_ON_PROPOSAL, etc.
  - File: `sim/brains/default_brain.gd`
  - Method: `_process_goal()` switch statement

- [ ] **Add completion checks for all new goal types** — Implement `_is_goal_complete()` for each new goal type.
  - File: `sim/brains/default_brain.gd`

---

## P3 — Tuning Parameters

### P3c — New Tuning Schema Entries
> Add all new tuning parameters to the schema.

- [ ] **Add NeedsPlanner tuning parameters** — `proactive_food_buffer`, `comfort_decay_per_tick`, `social_decay_per_tick`, `homelessness_comfort_penalty`
  - File: `sim/tuning.gd`

- [ ] **Add HomesteadPlanner tuning parameters** — `homestead_claim_radius`, `personal_stockpile_planks`, `personal_stockpile_stone`, `personal_shelter_planks`, `personal_shelter_stone`, `shelter_rest_bonus`
  - File: `sim/tuning.gd`

- [ ] **Add CareerPlanner tuning parameters** — `career_discovery_ticks`, `specialization_skill_threshold`
  - File: `sim/tuning.gd`

- [ ] **Add EconomyPlanner tuning parameters** — `contract_posting_cooldown_ticks`, `market_intention_cooldown_ticks`
  - File: `sim/tuning.gd`

---

# ═══════════════════════════════════════════════════════════════════
# VERSION 4+ — FUTURE EXPANSIONS
# ═══════════════════════════════════════════════════════════════════

## P1 — Advanced Social Systems (V4)
- [ ] Implement family/household units with shared resources
- [ ] Add inheritance system when agents die
- [ ] Implement reputation system beyond trust (faction-wide reputation)
- [ ] Add social events: weddings, funerals, festivals

## P1 — Conflict Systems (V4)
- [ ] Implement territorial disputes between agents
- [ ] Add faction warfare / resource conflict
- [ ] Implement theft and crime with enforcement response
- [ ] Add defensive structures (walls, guards)

## P2 — Advanced Economy (V5)
- [ ] Implement currency minting by factions
- [ ] Add banking and loans
- [ ] Implement trade routes between settlements
- [ ] Add supply chain optimization

## P3 — Environmental Dynamics (V5)
- [ ] Implement seasons with varying resource availability
- [ ] Add weather affecting agent behavior
- [ ] Implement natural disasters
- [ ] Add wildlife / hunting

---

# ═══════════════════════════════════════════════════════════════════
# VERSION 2 — COMPLETED
# ═══════════════════════════════════════════════════════════════════

<details>
<summary>V2 Settlement Simulation (Complete)</summary>

## P0 — Planning + Task System Foundations ✓
- [x] Add a Task/Project system phase before Agents tick without reordering existing pipeline. (docs/sim_update_order.md)
- [x] Expand JobBoard activity types (haul, deliver-to-project, build site, craft at station, farm tasks) and serialization coverage. (sim/job_board.gd)
- [x] Extend DefaultBrain to claim new task types and stack lightweight intents for project work. (sim/brains/default_brain.gd)
- [x] Add task generation hooks for build sites, hauling, and farming while preserving deterministic ordering. (sim/job_board.gd, sim/sim.gd)
- [x] Post deliver-to-project activities for communal project resource needs. (sim/systems/job_board_system.gd, sim/job_board.gd)
- [x] Post build-site activities for communal project build phases. (sim/systems/job_board_system.gd, sim/job_board.gd)

## P0 — Communal Projects as Build Sites ✓
- [x] Add build site state (required inputs, delivered inputs, build progress, assigned workers). (sim/communal_projects_system.gd)
- [x] Add phases: COLLECTING → BUILDING → COMPLETED with tick-based progress. (sim/communal_projects_system.gd)
- [x] Create BUILD_SITE tasks from active build sites and finalize on progress completion. (sim/communal_projects_system.gd, sim/job_board.gd)
- [x] Preserve project type API and resource requirement definition. (sim/communal_projects_system.gd)

## P0 — Shared Storage + Logistics ✓
- [x] Add Stockpile structure state with capacity, ownership, and reserved items. (sim/structures.gd, sim/structure_state.gd)
- [x] Add deposit/withdraw/haul actions and task types for stockpile logistics. (sim/actions.gd, sim/job_board.gd)
- [x] Add reservation/escrow to prevent double-spending across projects. (sim/communal_projects_system.gd)

## P1 — Organization Planner ✓
- [x] Create Organization entity with members, stockpile access, and treasury. (sim/organizations.gd)
- [x] Add daily planner that spawns stockpile/workshop/shelter projects based on thresholds. (sim/organizations.gd, sim/sim.gd)
- [x] Implement contiguous claim expansion from a town center and zoning tags. (sim/claims_system.gd)

## P1 — Workshops + Production Chains ✓
- [x] Add station types and require them for recipes (carpenter, kiln, smithy). (sim/recipes.gd, sim/workshop_system.gd)
- [x] Add craft-at-station tasks and station build projects. (sim/job_board.gd, sim/communal_projects_system.gd)
- [x] Planner spawns stations in sequence based on needs. (sim/organizations.gd)

## P1 — Roads + Logistics Optimization ✓
- [x] Spawn road projects connecting resource clusters to stockpiles and town center. (sim/communal_projects_system.gd, sim/organizations.gd)
- [x] Prefer roads for hauling tasks and pathing when available. (sim/agent_navigation.gd)

## P2 — Farming Pipeline ✓
- [x] Add farm plot tile state: tilled, seeded, growth, harvest-ready. (sim/world_tile.gd)
- [x] Integrate daily growth into EnvironmentSystem tick. (sim/environment_system.gd)
- [x] Add TILL/PLANT/HARVEST/DELIVER tasks and actions. (sim/job_board.gd, sim/actions.gd)
- [x] Transition from foraging to farming via planner thresholds. (sim/organizations.gd)

## P2 — Economy + Contracts ✓
- [x] Post procurement contracts when stockpiles fall below thresholds. (sim/organizations.gd, sim/contract_system.gd)
- [x] Auto-post surplus to market. (sim/market_system.gd)
- [x] Allow agents to choose between org tasks and paid contracts. (sim/brains/default_brain.gd)

## P3 — Metrics + Debugging (Partial)
- [ ] Add build-site progress telemetry and stockpile throughput metrics. (sim/metrics_system.gd)
- [ ] Add debug overlays for projects, tasks, and stockpile reservations. (viz/)

</details>

---

# ═══════════════════════════════════════════════════════════════════
# P4 — TESTING PIPELINE (Low Priority / Maintenance)
# ═══════════════════════════════════════════════════════════════════

<details>
<summary>Testing Infrastructure Improvements</summary>

## P4a — Test Discovery & Reliability
- [ ] Expand test discovery to include `tests/` root and nested subdirectories (recursive scan).【F:tests/test_runner.gd†L8-L148】
- [ ] Move test output file from `res://` to `user://` (or allow CLI override) to avoid read-only failures in headless/CI.【F:tests/test_runner.gd†L22-L27】
- [ ] Add `--path` guidance to docs to ensure `res://` resolves correctly in CLI runs.【F:README.md†L60-L69】
- [ ] Add a preflight check for required config files (`res://config/*.json`) and fail fast with a clear error message.【F:tests/test_fixtures.gd†L11-L30】
- [ ] Ensure runner returns a distinct error when zero tests are found (currently fails ambiguously).【F:tests/test_runner.gd†L124-L129】

## P4b — Timeout & Error Handling
- [x] Add per-test timeout handling (watchdog or cooperative yield/timeout pattern) to prevent indefinite hangs.【F:tests/test_runner.gd†L90-L317】
- [ ] Emit a timeout error that includes the test file and subtest label when applicable.【F:tests/test_runner.gd†L126-L145】

## P4c — Output & Filtering
- [x] Emit machine-readable test results (JSON) alongside human log output for CI parsing.【F:tests/test_runner.gd†L136-L224】
- [x] Summarize failures with test file + failure count at end of run for quick triage.【F:tests/test_runner.gd†L136-L145】
- [x] Add optional `--filter` or `--only` flag to run a subset of tests quickly.【F:tests/test_runner.gd†L168-L191】

## P4d — Fixture Consistency
- [ ] Consolidate test fixtures (`SimFixture` vs `TestFixtures`) to a single, documented API to reduce drift.【F:tests/sim_fixture.gd†L1-L52】【F:tests/test_fixtures.gd†L1-L139】
- [ ] Document the expected minimal tuning defaults used by tests to prevent slowdowns when tuning changes.【F:sim/sim.gd†L25-L120】

## P4e — Slow/Flaky Tests
- [ ] Add a "fast/slow" split (e.g., `tests/slow/`) with an opt-in flag to run long-running integration tests.【F:tests/integration/test_save_load.gd†L24-L126】
- [ ] Reduce simulation workload in tests by using a minimal tuning config (smaller world, fewer NPCs) to improve runtime stability.【F:config/tuning.json†L1-L60】

## P4f — Environment-Specific Cleanup
- [ ] Remove or rewrite `test_baseline_determinism.gd` to avoid hard-coded Windows Godot path; use in-process logic or PATH-based `godot` invocation.【F:tests/test_baseline_determinism.gd†L34-L60】
- [ ] Move debug scripts out of `tests/` or expand exclusions to avoid accidental execution when discovery is broadened.【F:tests/test_runner.gd†L12-L19】

## P4g — De-duplication
- [ ] Merge or remove duplicate config validation tests (`tests/test_config_schema.gd` vs `tests/integration/test_config.gd`) to prevent redundant failures and slow runs.【F:tests/test_config_schema.gd†L1-L118】【F:tests/integration/test_config.gd†L1-L118】

## P4h — Developer Experience
- [ ] Add a `make test` or script wrapper to standardize the command and project path usage.
- [ ] Add a short troubleshooting section for common CLI errors (missing `res://` path, permissions, missing Godot binary).

</details>

---

# Quick Reference: V3 Phase Order

| Phase | Focus | Key Deliverables |
|-------|-------|------------------|
| **P0** | Foundation | Agent state expansion, planner infrastructure |
| **P1a-c** | NeedsPlanner | Proactive food, stamina, comfort/social needs |
| **P1d-f** | HomesteadPlanner | Home establishment, personal structures |
| **P1g-h** | EconomyPlanner | Intent-driven contracts, capability evaluation |
| **P1i-j** | MarketBehaviorPlanner | Market intentions, price intelligence |
| **P1k-m** | CareerPlanner | Career assessment, tool/workshop goals |
| **P2a-c** | CivicPlanner | Faction joining, voting, law proposals |
| **P2d-e** | SocialPlanner | Trust-based trading |
| **P2f-g** | LongTermPlanner | Multi-day goal persistence |
| **P3a-c** | Integration | Priority system, goal processing, tuning |
