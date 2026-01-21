## Agent class - simulation entities with needs, behaviors, and enforcement
class_name Agent
extends RefCounted

var id: int = 0
var pos_x: int = 0
var pos_y: int = 0
var money: int = 0
var inventory: Dictionary = {}
var needs: Dictionary = {}
var skills: Dictionary = {}
var faction_id: int = 0
var is_player: bool = false
var role: String = "gatherer"
var personality: Dictionary = {}

## Action state
var current_action: Dictionary = {}
var target_node_id: int = -1
var target_workshop_id: int = -1

## Market/locking state
var locked_money: int = 0
var locked_inventory: Dictionary = {}

## Contract state
var active_contract_id: int = -1

## Enforcement state
var market_ban_until_tick: int = 0
var risk_tolerance: float = 0.5  # 0 = risk-averse, 1 = risk-seeking
var grievance: float = 0.0  # 0 = content, 1 = very aggrieved
var eco_concern: float = 0.0  # 0 = doesn't care about pollution, 1 = cares deeply

## Claim state
var is_claiming: bool = false
var claim_progress: int = 0

## Progression system
var claim_tokens: int = 25
var claim_level: int = 1
var experience: int = 0

## Planning state - for goal-oriented behavior
var goal_type: String = "none"  # Current high-level goal
var goal_target: int = -1       # Target ID for goal (workshop ID, faction ID, etc.)
var goal_progress: int = 0      # Progress toward goal (0-100)
var goal_data: Dictionary = {}  # Flexible goal metadata
var goal_stack: Array = []      # Stack of sub-goals (Dictionaries)



## Memory state - for smarter decisions
var last_gather_success_tick: int = 0
var last_trade_success_tick: int = 0
var exploration_direction: int = 0  # 0-7 compass direction
var known_resource_locations: Array = []  # [{"type": str, "x": int, "y": int, "tick": int}]
var social_memory: Dictionary = {}  # agent_id -> {"last_trade_tick": int, "trust": float}

## Skill experience (for future skill leveling system)
var skill_xp: Dictionary = {}

## Brain for decision-making (pluggable for LLM cognition)
var brain: IAgentBrain = null

func _init() -> void:
	needs = {
		"hunger": 100.0,
		"stamina": 100.0,  # Energy for actions
		"comfort": 100.0,  # Long-term well-being / happiness
		"social": 100.0    # Need for interaction
	}
	skills = {
		"gathering": 1.0,
		"crafting": 1.0,
		"mining": 1.0,
		"trading": 1.0
	}
	skill_xp = {
		"gathering": 0.0,
		"crafting": 0.0,
		"mining": 0.0,
		"trading": 0.0
	}
	
	# Default Personality (will be randomized deterministically by Sim._create_agent)
	personality = {
		"greed": 0.5,
		"laziness": 0.5,
		"social_need": 0.5,
		"risk_tolerance": 0.5
	}
	
	current_action = Actions.idle()
	brain = DefaultBrain.new()


## Serialize agent to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"pos_x": pos_x,
		"pos_y": pos_y,
		"money": money,
		"inventory": inventory.duplicate(),
		"needs": _serialize_needs(),
		"skills": _serialize_skills(),
		"skill_xp": _serialize_skill_xp(),
		"faction_id": faction_id,
		"is_player": is_player,
		"role": role,
		"current_action": current_action.duplicate(),
		"target_node_id": target_node_id,
		"target_workshop_id": target_workshop_id,
		"locked_money": locked_money,
		"locked_inventory": locked_inventory.duplicate(),
		"active_contract_id": active_contract_id,
		"market_ban_until_tick": market_ban_until_tick,
		"risk_tolerance": snappedf(risk_tolerance, 0.00000001),
		"grievance": snappedf(grievance, 0.00000001),
		"eco_concern": snappedf(eco_concern, 0.00000001),
		"is_claiming": is_claiming,
		"claim_progress": claim_progress,
		"claim_tokens": claim_tokens,
		"claim_level": claim_level,
		"experience": experience,
		"goal_type": goal_type,
		"goal_target": goal_target,
		"goal_progress": goal_progress,
		"goal_data": goal_data.duplicate(),
		"goal_stack": goal_stack.duplicate(true),
		"last_gather_success_tick": last_gather_success_tick,
		"last_trade_success_tick": last_trade_success_tick,
		"exploration_direction": exploration_direction,
		"known_resource_locations": _serialize_known_resource_locations(),
		"social_memory": _serialize_social_memory(),
		"personality": _serialize_personality()
	}

func _serialize_personality() -> Dictionary:
	var result := {}
	for k in personality:
		result[k] = snappedf(float(personality[k]), 0.00000001)
	return result

func _serialize_known_resource_locations() -> Array:
	var result := []
	for loc in known_resource_locations:
		result.append({
			"type": loc.get("type", ""),
			"x": int(loc.get("x", 0)),
			"y": int(loc.get("y", 0)),
			"tick": int(loc.get("tick", 0))
		})
	return result

func _serialize_social_memory() -> Dictionary:
	var result := {}
	for agent_id in social_memory:
		var mem: Dictionary = social_memory[agent_id]
		result[str(agent_id)] = {
			"trust": snappedf(float(mem.get("trust", 0.5)), 0.00000001),
			"last_trade_tick": int(mem.get("last_trade_tick", 0)),
			"trade_count": int(mem.get("trade_count", 0))
		}
	return result

func _serialize_needs() -> Dictionary:
	var result := {}
	for k in needs:
		result[k] = snappedf(float(needs[k]), 0.00000001)
	return result

func _serialize_skills() -> Dictionary:
	var result := {}
	for k in skills:
		result[k] = snappedf(float(skills[k]), 0.00000001)
	return result

func _serialize_skill_xp() -> Dictionary:
	var result := {}
	for k in skill_xp:
		result[k] = snappedf(float(skill_xp[k]), 0.00000001)
	return result

## Deserialize agent from dictionary
static func from_dict(d: Dictionary) -> Agent:
	var agent := Agent.new()
	agent.id = int(d.get("id", 0))
	agent.pos_x = int(d.get("pos_x", 0))
	agent.pos_y = int(d.get("pos_y", 0))
	agent.money = int(d.get("money", 0))
	# Convert inventory values to int (JSON may parse as float)
	var inv: Dictionary = d.get("inventory", {})
	agent.inventory = {}
	for key in inv:
		agent.inventory[key] = int(inv[key])
	# Convert needs values to float (with defaults for new needs)
	var needs_data: Dictionary = d.get("needs", {"hunger": 100.0, "stamina": 100.0})
	agent.needs = {}
	for key in needs_data:
		agent.needs[key] = snappedf(float(needs_data[key]), 0.00000001)
	# Ensure stamina exists
	if not agent.needs.has("stamina"):
		agent.needs["stamina"] = 100.0
	if not agent.needs.has("comfort"):
		agent.needs["comfort"] = 100.0
	if not agent.needs.has("social"):
		agent.needs["social"] = 100.0
	# Convert skills values to float
	var skills_data: Dictionary = d.get("skills", {})
	agent.skills = {}
	for key in skills_data:
		agent.skills[key] = snappedf(float(skills_data[key]), 0.00000001)
	# Convert skill_xp values to float
	var skill_xp_data: Dictionary = d.get("skill_xp", {})
	agent.skill_xp = {}
	for key in skill_xp_data:
		agent.skill_xp[key] = snappedf(float(skill_xp_data[key]), 0.00000001)
	agent.faction_id = int(d.get("faction_id", 0))
	agent.is_player = d.get("is_player", false)
	agent.role = d.get("role", "gatherer")
	# Convert current_action with proper types
	var action_data: Dictionary = d.get("current_action", {"type": "IDLE"})
	agent.current_action = {}
	# List of keys that should be integers
	var int_action_keys := ["node_id", "workshop_id", "contract_id", "target_x", "target_y", "x", "y",
							"direction", "proposal_id", "project_id", "qty"]
	for key in action_data:
		var val = action_data[key]
		if key in int_action_keys:
			agent.current_action[key] = int(val)
		else:
			agent.current_action[key] = val
	agent.target_node_id = int(d.get("target_node_id", -1))
	agent.target_workshop_id = int(d.get("target_workshop_id", -1))
	agent.locked_money = int(d.get("locked_money", 0))
	# Convert locked_inventory values to int
	var locked_inv: Dictionary = d.get("locked_inventory", {})
	agent.locked_inventory = {}
	for key in locked_inv:
		agent.locked_inventory[key] = int(locked_inv[key])
	agent.active_contract_id = int(d.get("active_contract_id", -1))
	agent.market_ban_until_tick = int(d.get("market_ban_until_tick", 0))
	agent.risk_tolerance = snappedf(float(d.get("risk_tolerance", 0.5)), 0.00000001)
	agent.grievance = snappedf(float(d.get("grievance", 0.0)), 0.00000001)
	agent.eco_concern = snappedf(float(d.get("eco_concern", 0.0)), 0.00000001)
	agent.is_claiming = d.get("is_claiming", false)
	agent.claim_progress = int(d.get("claim_progress", 0))
	agent.claim_tokens = int(d.get("claim_tokens", 25))
	agent.claim_level = int(d.get("claim_level", 1))
	agent.experience = int(d.get("experience", 0))
	# Deserialize planning state
	agent.goal_type = d.get("goal_type", "none")
	agent.goal_target = int(d.get("goal_target", -1))
	agent.goal_progress = int(d.get("goal_progress", 0))
	agent.goal_data = d.get("goal_data", {}).duplicate()
	agent.goal_stack = d.get("goal_stack", []).duplicate(true)
	# Deserialize memory state
	agent.last_gather_success_tick = int(d.get("last_gather_success_tick", 0))
	agent.last_trade_success_tick = int(d.get("last_trade_success_tick", 0))
	agent.exploration_direction = int(d.get("exploration_direction", 0))
	# Deserialize known_resource_locations with proper int types
	var locs_data: Array = d.get("known_resource_locations", [])
	agent.known_resource_locations = []
	for loc in locs_data:
		agent.known_resource_locations.append({
			"type": loc.get("type", ""),
			"x": int(loc.get("x", 0)),
			"y": int(loc.get("y", 0)),
			"tick": int(loc.get("tick", 0))
		})
	# Deserialize social_memory with proper types
	var social_data: Dictionary = d.get("social_memory", {})
	agent.social_memory = {}
	for agent_id_str in social_data:
		var agent_id := int(agent_id_str)
		var mem: Dictionary = social_data[agent_id_str]
		agent.social_memory[agent_id] = {
			"trust": snappedf(float(mem.get("trust", 0.5)), 0.00000001),
			"last_trade_tick": int(mem.get("last_trade_tick", 0)),
			"trade_count": int(mem.get("trade_count", 0))
		}
	# Deserialize personality with float precision normalization
	var personality_data: Dictionary = d.get("personality", {
		"greed": 0.5, "laziness": 0.5, "social_need": 0.5, "risk_tolerance": 0.5
	})
	agent.personality = {}
	for key in personality_data:
		agent.personality[key] = snappedf(float(personality_data[key]), 0.00000001)
	
	# CRITICAL: Initialize brain for decision-making capability
	agent.brain = DefaultBrain.new()
	
	return agent

## Get available money
func get_available_money() -> int:
	return maxi(0, money - locked_money)

## Get available item quantity
func get_available_item(item_name: String) -> int:
	var total: int = inventory.get(item_name, 0)
	var locked: int = locked_inventory.get(item_name, 0)
	return maxi(0, total - locked)

## Lock money for a buy order
func lock_money(amount: int) -> bool:
	if get_available_money() >= amount:
		locked_money += amount
		return true
	return false

## Release locked money
func release_locked_money(amount: int) -> void:
	locked_money = maxi(0, locked_money - amount)

## Lock items for a sell order
func lock_item(item_name: String, qty: int) -> bool:
	if get_available_item(item_name) >= qty:
		locked_inventory[item_name] = locked_inventory.get(item_name, 0) + qty
		return true
	return false

## Release locked items (unlock only - does not consume)
func release_locked_item(item_name: String, qty: int) -> void:
	if item_name in locked_inventory:
		locked_inventory[item_name] = maxi(0, locked_inventory[item_name] - qty)
		if locked_inventory[item_name] <= 0:
			locked_inventory.erase(item_name)
	# NOTE: Do NOT remove from inventory here. Expiration should not destroy items.

## Consume locked items (unlock AND consume - used for settled trades)
func consume_locked_item(item_name: String, qty: int) -> void:
	# First unlock
	release_locked_item(item_name, qty)
	# Then consume from actual inventory
	if item_name in inventory:
		inventory[item_name] = maxi(0, inventory[item_name] - qty)
		if inventory[item_name] <= 0:
			inventory.erase(item_name)

## Check if has item
func has_item(item_name: String, qty: int = 1) -> bool:
	return inventory.get(item_name, 0) >= qty

## Check if has available items
func has_available_item(item_name: String, qty: int = 1) -> bool:
	return get_available_item(item_name) >= qty

## Add item to inventory
func add_item(item_name: String, qty: int) -> void:
	inventory[item_name] = inventory.get(item_name, 0) + qty

## Remove item from inventory
func remove_item(item_name: String, qty: int) -> bool:
	if not has_available_item(item_name, qty):
		return false
	inventory[item_name] = inventory.get(item_name, 0) - qty
	if inventory[item_name] <= 0:
		inventory.erase(item_name)
	return true

## Get item count
func get_item_count(item_name: String) -> int:
	return inventory.get(item_name, 0)

## Check if has a tool
func has_tool(tool_name: String) -> bool:
	return get_item_count(tool_name) > 0

## Get hunger
func get_hunger() -> float:
	return needs.get("hunger", 100.0)

## Set hunger
func set_hunger(value: float) -> void:
	needs["hunger"] = clampf(value, 0.0, 100.0)

## Is alive
func is_alive() -> bool:
	return get_hunger() > 0.0

## Is at position
func is_at(x: int, y: int) -> bool:
	return pos_x == x and pos_y == y

## Is at market
func is_at_market(tuning: Dictionary) -> bool:
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	return is_at(market_x, market_y)

## Is at workshop
func is_at_workshop(workshop: Workshop) -> bool:
	if workshop == null:
		return false
	return is_at(workshop.pos_x, workshop.pos_y)

## Has active contract
func has_active_contract() -> bool:
	return active_contract_id > 0

## Is market banned
func is_market_banned(current_tick: int) -> bool:
	return market_ban_until_tick > current_tick

## Should risk illegal action
func should_risk_illegal(hunger: float, urgent_threshold: float) -> bool:
	# If starving, always risk
	if hunger < urgent_threshold:
		return true
	# Otherwise based on risk tolerance
	return risk_tolerance > 0.7

## Calculate bid price
func calculate_bid_price(item: String, market: Market, tuning: Dictionary, food_scarce: bool = false) -> int:
	var ref: float = market.get_ref_price(item)
	
	# Scarcity uplift for food
	if food_scarce and item in ["Berries", "CookedMeal"]:
		var uplift: float = tuning.get("food_scarcity_multiplier", 0.4)
		ref *= (1.0 + uplift)
	
	var target: int = tuning.get("target_food_buffer", 5)
	var have: int = get_item_count(item)
	var scarcity: float = clampf(float(target - have) / maxf(float(target), 1.0), -1.0, 1.0)
	var strength: float = tuning.get("bid_scarcity_strength", 0.6)
	
	# Scarcity uplift for food
	if item in ["Berries", "CookedMeal"]:
		var berry_stock := market.get_trade_count("Berries") # Not perfect, but use world stocks?
		# Actually, let's use the tuning param check if passed/available. 
		# If not available, we can't easily check global stock without world.
		# For now, let's assume world-level sensing happens in decide_action.
		pass

	var bid_multiplier: float = 1.0 + (strength * scarcity)
	
	# Desperation: Starvation (for food)
	if item in ["Berries", "CookedMeal"]:
		var hunger = get_hunger()
		if hunger < 20.0:
			bid_multiplier += 1.5 # Massive bid to ensure survival
		elif hunger < 50.0:
			bid_multiplier += 0.5
			
	var bid: int = int(round(ref * bid_multiplier))
	return clampi(bid, tuning.get("min_price", 1), tuning.get("max_price", 1000))

## Calculate ask price
func calculate_ask_price(item: String, market: Market, tuning: Dictionary, food_scarce: bool = false) -> int:
	var ref: float = market.get_ref_price(item)
	
	var target: int = 0
	if item == "Berries": # Don't sell berries if we need them, but logic handled in brain
		target = tuning.get("target_food_buffer", 5)
		
	var have: int = get_item_count(item)
	var surplus: float = clampf(float(have - target) / maxf(float(target), 1.0), -1.0, 1.0)
	var strength: float = tuning.get("ask_surplus_strength", 0.6)
	
	var ask_multiplier: float = 1.0 - (strength * surplus)
	
	# Desperation: Poverty (Need money for food)
	# If we are selling NON-FOOD items, and we are starving or poor, SELL CHEAP!
	if item != "Berries" and item != "CookedMeal":
		var money = get_available_money()
		var hunger = get_hunger()
		
		# Extreme Urgency
		if money < 20 or hunger < 30.0:
			ask_multiplier *= 0.5 # Fire sale
		elif money < 50:
			ask_multiplier *= 0.8
			
	var ask: int = int(round(ref * ask_multiplier))
	return clampi(ask, tuning.get("min_price", 1), tuning.get("max_price", 1000))

## Decide action - delegates to the brain for decision-making
func decide_action(world: World, market: Market, contracts_system: ContractsSystem, 
				   tuning: Dictionary, recipes: Dictionary = {}, state: SimState = null) -> Dictionary:
	return brain.decide_action(self, world, market, contracts_system, tuning, recipes, state)

# ========================
# STAMINA SYSTEM
# ========================

## Get current stamina
func get_stamina() -> float:
	return needs.get("stamina", 100.0)

## Set stamina (clamped 0-100)
func set_stamina(value: float) -> void:
	needs["stamina"] = clampf(value, 0.0, 100.0)

## Drain stamina by amount (returns actual drained)
func drain_stamina(amount: float) -> float:
	var current := get_stamina()
	var drain := minf(current, amount)
	set_stamina(current - drain)
	return drain

## Recover stamina by amount
func recover_stamina(amount: float) -> void:
	set_stamina(get_stamina() + amount)

## Check if agent is exhausted (stamina too low to work efficiently)
func is_exhausted(tuning: Dictionary) -> bool:
	var threshold: float = tuning.get("stamina_low_threshold", 20.0)
	return get_stamina() < threshold

## Check if agent has zero stamina (must rest)
func must_rest() -> bool:
	return get_stamina() <= 0.0

## Get work efficiency based on stamina
func get_stamina_efficiency(tuning: Dictionary) -> float:
	if is_exhausted(tuning):
		return tuning.get("stamina_exhausted_yield_penalty", 0.5)
	return 1.0

# ========================
# SKILL SYSTEM (STUBS)
# ========================

## Get skill level
func get_skill_level(skill_name: String) -> float:
	return skills.get(skill_name, 1.0)

## Add skill XP (future: level up when threshold reached)
func add_skill_xp(skill_name: String, amount: float, tuning: Dictionary) -> void:
	if not skill_xp.has(skill_name):
		skill_xp[skill_name] = 0.0
	skill_xp[skill_name] += amount
	# Future: check level up threshold and increase skill level

# ========================
# GOAL PLANNING
# ========================

## Set a new goal
func set_goal(type: String, target: int = -1, data: Dictionary = {}) -> void:
	goal_type = type
	goal_target = target
	goal_progress = 0
	goal_data = data.duplicate()

## Clear current goal
func clear_goal() -> void:
	goal_type = "none"
	goal_target = -1
	goal_progress = 0
	goal_data = {}
	goal_stack.clear()

	goal_data = {}
	
## Check if has active goal
func has_goal() -> bool:
	return goal_type != "none" and goal_type != ""

## Advance goal progress
func advance_goal(amount: int = 1) -> void:
	goal_progress += amount

## Check if goal is complete
func is_goal_complete(threshold: int = 100) -> bool:
	return goal_progress >= threshold

# ========================
# MEMORY SYSTEM
# ========================

## Remember a resource location (capacity configurable via tuning)
func remember_resource(type: String, x: int, y: int, tick: int, capacity: int = 20) -> void:
	# Keep list bounded
	if known_resource_locations.size() >= capacity:
		known_resource_locations.pop_front()
	known_resource_locations.append({
		"type": type,
		"x": x,
		"y": y,
		"tick": tick
	})

## Get remembered resources of a type
func get_remembered_resources(type: String) -> Array:
	var result := []
	for loc in known_resource_locations:
		if loc["type"] == type:
			result.append(loc)
	return result

## Update social memory for an agent (trust values configurable via tuning)
## Capacity limit prevents unbounded memory growth over long simulations
const SOCIAL_MEMORY_CAPACITY := 30

func update_social_memory(other_id: int, interaction_type: String, tick: int, initial_trust: float = 0.5, trade_increase: float = 0.05) -> void:
	# If at capacity and this is a new agent, remove the least trusted/oldest entry
	if not social_memory.has(other_id) and social_memory.size() >= SOCIAL_MEMORY_CAPACITY:
		var to_remove := -1
		var min_score := INF
		for agent_id in social_memory:
			var mem: Dictionary = social_memory[agent_id]
			# Score = trust * recency_factor (lower = more likely to forget)
			var trust: float = mem.get("trust", 0.5)
			var last_tick: int = mem.get("last_trade_tick", 0)
			var recency: float = 1.0 / maxf(1.0, float(tick - last_tick))
			var score: float = trust * recency
			if score < min_score:
				min_score = score
				to_remove = agent_id
		if to_remove >= 0:
			social_memory.erase(to_remove)

	if not social_memory.has(other_id):
		social_memory[other_id] = {"trust": initial_trust, "last_trade_tick": 0, "trade_count": 0}

	if interaction_type == "trade":
		social_memory[other_id]["last_trade_tick"] = tick
		social_memory[other_id]["trade_count"] += 1
		# Slight trust increase on successful trade
		social_memory[other_id]["trust"] = minf(1.0, social_memory[other_id]["trust"] + trade_increase)


## Get trust level with another agent
func get_trust(other_id: int) -> float:
	if social_memory.has(other_id):
		return social_memory[other_id].get("trust", 0.5)
	return 0.5

# ========================
# PROGRESSION SYSTEM
# ========================

## Add experience and check for level up
func add_experience(amount: int, tuning: Dictionary) -> bool:
	experience += amount
	var xp_per_level = tuning.get("agent_claim_xp_per_level", 50)
	var max_level = tuning.get("agent_level_max", 4)
	
	if claim_level < max_level and experience >= claim_level * xp_per_level:
		claim_level += 1
		var tokens_per_level = tuning.get("agent_claim_tokens_per_level", 5)
		claim_tokens += tokens_per_level
		return true  # Leveled up
	return false

## Check if agent has claim tokens available
func has_claim_tokens(amount: int = 1) -> bool:
	return claim_tokens >= amount

## Use claim tokens (for claiming territory)
func use_claim_tokens(amount: int = 1) -> bool:
	if has_claim_tokens(amount):
		claim_tokens -= amount
		return true
	return false

## Get max claims based on level
func get_max_claims() -> int:
	return 25 + (claim_level - 1) * 5
