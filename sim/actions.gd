## Actions - action system for agent behaviors
class_name Actions
extends RefCounted

## Action types
const TYPE_IDLE := "IDLE"
const TYPE_MOVE_TO_NODE := "MOVE_TO_NODE"
const TYPE_MOVE_TO_MARKET := "MOVE_TO_MARKET"
const TYPE_MOVE_TO_WORKSHOP := "MOVE_TO_WORKSHOP"
const TYPE_MOVE_TO_DELIVERY := "MOVE_TO_DELIVERY"
const TYPE_MOVE_TO_POSITION := "MOVE_TO_POSITION"
const TYPE_GATHER_NODE := "GATHER_NODE"
const TYPE_EAT := "EAT"
const TYPE_EAT_MEAL := "EAT_MEAL"
const TYPE_PLACE_BUY_ORDER := "PLACE_BUY_ORDER"
const TYPE_PLACE_SELL_ORDER := "PLACE_SELL_ORDER"
const TYPE_QUEUE_CRAFT := "QUEUE_CRAFT"
const TYPE_ACCEPT_CONTRACT := "ACCEPT_CONTRACT"
const TYPE_DELIVER_CONTRACT := "DELIVER_CONTRACT"
## New action types for expanded brain
const TYPE_REST := "REST"
const TYPE_SLEEP := "SLEEP"
const TYPE_EXPLORE := "EXPLORE"
const TYPE_CLAIM_TILE := "CLAIM_TILE"
const TYPE_BUILD_WORKSHOP := "BUILD_WORKSHOP"
const TYPE_VOTE := "VOTE"
const TYPE_CONTRIBUTE_TO_PROJECT := "CONTRIBUTE_TO_PROJECT"
const TYPE_BUILD_ROAD := "BUILD_ROAD"
const TYPE_WITHDRAW_STOCKPILE := "WITHDRAW_STOCKPILE"
const TYPE_DEPOSIT_STOCKPILE := "DEPOSIT_STOCKPILE"
const TYPE_BUILD_SITE := "BUILD_SITE"

## Create actions
static func idle() -> Dictionary:
	return {"type": TYPE_IDLE}

static func move_to_node(node_id: int) -> Dictionary:
	return {"type": TYPE_MOVE_TO_NODE, "node_id": node_id}

static func move_to_market() -> Dictionary:
	return {"type": TYPE_MOVE_TO_MARKET}

static func move_to_workshop(workshop_id: int) -> Dictionary:
	return {"type": TYPE_MOVE_TO_WORKSHOP, "workshop_id": workshop_id}

static func move_to_delivery(contract_id: int) -> Dictionary:
	return {"type": TYPE_MOVE_TO_DELIVERY, "contract_id": contract_id}

static func gather_node(node_id: int) -> Dictionary:
	return {"type": TYPE_GATHER_NODE, "node_id": node_id}

static func eat() -> Dictionary:
	return {"type": TYPE_EAT}

static func eat_meal() -> Dictionary:
	return {"type": TYPE_EAT_MEAL}

static func place_buy_order(item: String) -> Dictionary:
	return {"type": TYPE_PLACE_BUY_ORDER, "item": item}

static func place_sell_order(item: String) -> Dictionary:
	return {"type": TYPE_PLACE_SELL_ORDER, "item": item}

static func queue_craft(recipe_id: String, workshop_id: int = -1) -> Dictionary:
	return {"type": TYPE_QUEUE_CRAFT, "recipe_id": recipe_id, "workshop_id": workshop_id}

static func accept_contract(contract_id: int) -> Dictionary:
	return {"type": TYPE_ACCEPT_CONTRACT, "contract_id": contract_id}

static func deliver_contract(contract_id: int) -> Dictionary:
	return {"type": TYPE_DELIVER_CONTRACT, "contract_id": contract_id}

## New action creators for expanded brain

static func rest() -> Dictionary:
	return {"type": TYPE_REST}

static func sleep_rest() -> Dictionary:
	return {"type": TYPE_SLEEP}

static func explore(direction: int) -> Dictionary:
	return {"type": TYPE_EXPLORE, "direction": direction}

static func move_to_position(x: int, y: int) -> Dictionary:
	return {"type": TYPE_MOVE_TO_POSITION, "target_x": x, "target_y": y}

static func claim_tile(x: int, y: int, for_faction: bool = false) -> Dictionary:
	return {"type": TYPE_CLAIM_TILE, "target_x": x, "target_y": y, "for_faction": for_faction}

static func build_workshop(x: int, y: int) -> Dictionary:
	return {"type": TYPE_BUILD_WORKSHOP, "target_x": x, "target_y": y}

static func vote(proposal_id: int, vote_for: bool) -> Dictionary:
	return {"type": TYPE_VOTE, "proposal_id": proposal_id, "vote_for": vote_for}

static func contribute_to_project(project_id: int, item: String, qty: int) -> Dictionary:
	return {"type": TYPE_CONTRIBUTE_TO_PROJECT, "project_id": project_id, "item": item, "qty": qty}

static func build_road(x: int, y: int) -> Dictionary:
	return {"type": TYPE_BUILD_ROAD, "target_x": x, "target_y": y}

static func withdraw_stockpile(structure_id: int, item: String, qty: int, require_reserved: bool = false) -> Dictionary:
	return {
		"type": TYPE_WITHDRAW_STOCKPILE,
		"structure_id": structure_id,
		"item": item,
		"qty": qty,
		"require_reserved": require_reserved
	}

static func deposit_stockpile(structure_id: int, item: String, qty: int) -> Dictionary:
	return {"type": TYPE_DEPOSIT_STOCKPILE, "structure_id": structure_id, "item": item, "qty": qty}

static func build_site(project_id: int) -> Dictionary:
	return {"type": TYPE_BUILD_SITE, "project_id": project_id}

## Execute one tick of an action (with enforcement)
static func execute_action(agent: Agent, action: Dictionary, world: World, 
						   market: Market, crafting: Crafting, contracts_system: ContractsSystem,
						   enforcement: Enforcement, state: SimState,
						   recipes: Dictionary, tuning: Dictionary, current_tick: int) -> bool:
	var action_type: String = action.get("type", TYPE_IDLE)
	
	match action_type:
		TYPE_IDLE:
			return true
		TYPE_MOVE_TO_NODE:
			return _execute_move_to_node(agent, action, world, tuning)
		TYPE_MOVE_TO_MARKET:
			return _execute_move_to_market(agent, world, tuning)
		TYPE_MOVE_TO_WORKSHOP:
			return _execute_move_to_workshop(agent, action, world, tuning)
		TYPE_MOVE_TO_DELIVERY:
			return _execute_move_to_delivery(agent, action, world, contracts_system, tuning)
		TYPE_MOVE_TO_POSITION:
			return _execute_move_to_position(agent, action, world, tuning)
		TYPE_GATHER_NODE:
			return _execute_gather(agent, action, world, enforcement, state, tuning, current_tick)
		TYPE_EAT:
			return _execute_eat(agent, tuning)
		TYPE_EAT_MEAL:
			return _execute_eat_meal(agent, tuning)
		TYPE_PLACE_BUY_ORDER:
			return _execute_buy_order(agent, action, market, enforcement, state, world, tuning, current_tick)
		TYPE_PLACE_SELL_ORDER:
			return _execute_sell_order(agent, action, market, enforcement, state, world, tuning, current_tick)
		TYPE_QUEUE_CRAFT:
			return _execute_queue_craft(agent, action, world, crafting, recipes, state, tuning)
		TYPE_ACCEPT_CONTRACT:
			return _execute_accept_contract(agent, action, contracts_system, current_tick, state)
		TYPE_DELIVER_CONTRACT:
			return _execute_deliver_contract(agent, action, contracts_system, state)
		# New action types
		TYPE_REST:
			return _execute_rest(agent, tuning)
		TYPE_SLEEP:
			return _execute_sleep(agent, tuning)
		TYPE_EXPLORE:
			return _execute_explore(agent, action, world, tuning, current_tick)
		TYPE_CLAIM_TILE:
			return _execute_claim_tile(agent, action, world, state, tuning, current_tick)
		TYPE_BUILD_WORKSHOP:
			return _execute_build_workshop(agent, action, world, tuning, current_tick)
		TYPE_VOTE:
			return _execute_vote(agent, action, state)
		TYPE_CONTRIBUTE_TO_PROJECT:
			return _execute_contribute_project(agent, action, state)
		TYPE_BUILD_ROAD:
			return _execute_build_road(agent, action, world, state, tuning, current_tick)
		TYPE_WITHDRAW_STOCKPILE:
			return _execute_withdraw_stockpile(agent, action, state)
		TYPE_DEPOSIT_STOCKPILE:
			return _execute_deposit_stockpile(agent, action, state)
		TYPE_BUILD_SITE:
			return _execute_build_site(agent, action, state, world, tuning)
		_:
			return true

## Move toward a resource node
static func _execute_move_to_node(agent: Agent, action: Dictionary, world: World, tuning: Dictionary) -> bool:
	var node_id: int = action.get("node_id", -1)
	var target_node: ResourceNode = world.get_node_by_id(node_id)
	if target_node == null:
		return true
	return _move_toward(agent, target_node.pos_x, target_node.pos_y, world, tuning)

## Move toward market
static func _execute_move_to_market(agent: Agent, world: World, tuning: Dictionary) -> bool:
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	return _move_toward(agent, market_x, market_y, world, tuning)

## Move toward a workshop
static func _execute_move_to_workshop(agent: Agent, action: Dictionary, world: World, tuning: Dictionary) -> bool:
	var workshop_id: int = action.get("workshop_id", -1)
	var workshop: Workshop = world.get_workshop_by_id(workshop_id)
	if workshop == null:
		return true
	return _move_toward(agent, workshop.pos_x, workshop.pos_y, world, tuning)

## Move toward delivery location
static func _execute_move_to_delivery(agent: Agent, action: Dictionary, world: World, contracts_system: ContractsSystem, tuning: Dictionary) -> bool:
	var contract_id: int = action.get("contract_id", -1)
	var contract := contracts_system.get_contract(contract_id)
	if contract == null:
		return true
	return _move_toward(agent, contract.delivery_pos_x, contract.delivery_pos_y, world, tuning)

## Move toward arbitrary position
static func _execute_move_to_position(agent: Agent, action: Dictionary, world: World, tuning: Dictionary) -> bool:
	var target_x: int = action.get("target_x", agent.pos_x)
	var target_y: int = action.get("target_y", agent.pos_y)
	return _move_toward(agent, target_x, target_y, world, tuning)

## Move one step toward target (with stamina drain)
static func _move_toward(agent: Agent, target_x: int, target_y: int, world: World = null, tuning: Dictionary = {}) -> bool:
	var dx: int = target_x - agent.pos_x
	var dy: int = target_y - agent.pos_y
	
	if dx == 0 and dy == 0:
		return true
	
	# Drain stamina for movement
	var move_cost: float = tuning.get("stamina_drain_move", 0.5)
	
	# Road speedup: reduce stamina cost if moving from/to a road? 
	# Let's check both current and next tile for simplicity
	# Wait, we only know current pos and target x/y.
	# Let's check if current tile has road.
	if world != null and world.has_road(agent.pos_x, agent.pos_y):
		move_cost *= tuning.get("road_stamina_drain_multiplier", 0.5)
	
	agent.drain_stamina(move_cost)
	
	if dx != 0:
		agent.pos_x += 1 if dx > 0 else -1
	elif dy != 0:
		agent.pos_y += 1 if dy > 0 else -1
	
	return agent.pos_x == target_x and agent.pos_y == target_y

## Gather from a node (with enforcement check)
static func _execute_gather(agent: Agent, action: Dictionary, world: World, 
							enforcement: Enforcement, state: SimState,
							tuning: Dictionary, current_tick: int) -> bool:
	var node_id: int = action.get("node_id", -1)
	var target_node: ResourceNode = world.get_node_by_id(node_id)
	
	if target_node == null:
		return true
	
	if agent.pos_x != target_node.pos_x or agent.pos_y != target_node.pos_y:
		return true
	
	if not target_node.has_stock(1):
		return true
	
	# Check for illegal harvest
	var tile_owner := world.get_claim_owner(target_node.pos_x, target_node.pos_y)
	if tile_owner > 0 and tile_owner != agent.id:
		# Check faction membership for faction-owned tiles
		var has_permit := false
		if World.is_faction_owner(tile_owner):
			var faction_id := World.faction_id_from_owner(tile_owner)
			has_permit = (agent.faction_id == faction_id)
		else:
			has_permit = (agent.id == tile_owner)
		
		if not has_permit:
			var laws := state.get_laws(tile_owner)
			var result := enforcement.process_illegal_harvest_with_state(
				agent, tile_owner, laws, state.rng, tuning, state.items, current_tick, state)
			if not result["allowed"]:
				# Surface reason even if UI doesn't display yet
				state.log_event("enforcement_blocked", {
					"agent_id": agent.id,
					"action": "gather",
					"reason_code": result["reason_code"],
					"details": result["details"]
				})
				return true  # Gather blocked
	
	var gather_amount := 1
	if target_node.type == "tree":
		if agent.has_tool("Axe"):
			gather_amount = tuning.get("axe_tree_bonus", 2)
		elif agent.has_tool("WoodenAxe"):
			gather_amount = maxi(1, tuning.get("axe_tree_bonus", 2) - 1)
	elif target_node.type == "ore":
		if agent.has_tool("Pickaxe"):
			gather_amount = tuning.get("pickaxe_ore_bonus", 2)
		elif agent.has_tool("WoodenPickaxe"):
			gather_amount = maxi(1, tuning.get("pickaxe_ore_bonus", 2) - 1)
	
	var removed := 0
	if target_node.type == "berry":
		var local_pollution := world.get_pollution(target_node.pos_x, target_node.pos_y)
		var start: float = tuning.get("food_yield_pollution_start", 0.3)
		var step: float = tuning.get("food_yield_pollution_step", 0.1)
		
		var effective_yield := 1
		if local_pollution >= start:
			effective_yield = maxi(0, 1 - int(floor((local_pollution - start) / step)))
		
		# Alternative deterministic check if yield becomes 0/1 alternating
		if effective_yield > 0 and local_pollution >= 0.6:
			# Yield 0 every other tick based on agent and tick for determinism
			if (agent.id + current_tick) % 2 == 0:
				effective_yield = 0
		
		removed = target_node.remove_stock(effective_yield)
	else:
		removed = target_node.remove_stock(gather_amount)
	
	if removed > 0:
		var item_type := target_node.get_item_type()
		# Apply stamina efficiency penalty
		var efficiency := agent.get_stamina_efficiency(tuning)
		var actual_received := int(ceil(removed * efficiency))
		agent.add_item(item_type, actual_received)
		
		# Drain stamina for gathering
		var gather_cost: float = tuning.get("stamina_drain_gather", 2.0)
		agent.drain_stamina(gather_cost)
		
		# Add skill XP and general experience
		var skill_xp_amount: float = tuning.get("skill_xp_per_action", 0.1)
		if target_node.type == "ore":
			agent.add_skill_xp("mining", skill_xp_amount, tuning)
		else:
			agent.add_skill_xp("gathering", skill_xp_amount, tuning)
		
		# Add general experience for successful gathering
		agent.add_experience(1, tuning)  # 1 XP for gathering
		
		if target_node.type == "ore":
			var pollution_per_ore: float = tuning.get("pollution_per_ore", 0.01)
			var current_pollution := world.get_pollution(target_node.pos_x, target_node.pos_y)
			world.set_pollution(target_node.pos_x, target_node.pos_y, 
							   current_pollution + pollution_per_ore * removed)
	
	return true

## Eat berries
static func _execute_eat(agent: Agent, tuning: Dictionary) -> bool:
	if agent.has_available_item("Berries", 1):
		agent.remove_item("Berries", 1)
		var nutrition: float = tuning.get("berry_nutrition", 20)
		agent.set_hunger(agent.get_hunger() + nutrition)
	return true

## Eat cooked meal
static func _execute_eat_meal(agent: Agent, tuning: Dictionary) -> bool:
	if agent.has_available_item("CookedMeal", 1):
		agent.remove_item("CookedMeal", 1)
		var nutrition: float = tuning.get("meal_nutrition", 40)
		agent.set_hunger(agent.get_hunger() + nutrition)
	return true

## Place a buy order (with market ban and embargo check)
static func _execute_buy_order(agent: Agent, action: Dictionary, market: Market, 
							   enforcement: Enforcement, state: SimState, world: World,
							   tuning: Dictionary, current_tick: int) -> bool:
	if not agent.is_at_market(tuning):
		return true
	
	# Check market ban and embargo
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	var access := market.can_place_order(agent, state, world, Vector2i(market_x, market_y))
	if not access["allowed"]:
		state.log_event("order_denied", {
			"agent_id": agent.id,
			"reason_code": access["reason_code"],
			"details": access.get("details", {}),
			"type": "buy",
			"item": action.get("item", "Berries")
		})
		return true  # Silently blocked (ban or embargo)
	
	var item: String = action.get("item", "Berries")
	
	if market.has_active_order(agent.id, item, "buy"):
		return true

	var global_berry_stock := world.get_total_stock("berry")
	var food_scarce: bool = global_berry_stock < int(tuning.get("berry_scarcity_threshold", 100))

	var price: int = agent.calculate_bid_price(item, market, tuning, food_scarce)
	var qty: int = 5
	
	var total_cost: int = qty * price
	if agent.get_available_money() < total_cost:
		qty = agent.get_available_money() / price
		if qty <= 0:
			return true
		total_cost = qty * price
	
	if not agent.lock_money(total_cost):
		return true
	
	var ttl: int = tuning.get("order_ttl_ticks", 48)
	var order := market.create_order("buy", item, qty, price, agent.id, current_tick, ttl)
	market.place_buy_order(order)
	
	return true

## Place a sell order (with market ban and embargo check)
static func _execute_sell_order(agent: Agent, action: Dictionary, market: Market,
								enforcement: Enforcement, state: SimState, world: World,
								tuning: Dictionary, current_tick: int) -> bool:
	if not agent.is_at_market(tuning):
		return true
	
	# Check market ban and embargo
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	var access := market.can_place_order(agent, state, world, Vector2i(market_x, market_y))
	if not access["allowed"]:
		state.log_event("order_denied", {
			"agent_id": agent.id,
			"reason_code": access["reason_code"],
			"details": access.get("details", {}),
			"type": "sell",
			"item": action.get("item", "Berries")
		})
		return true  # Silently blocked (ban or embargo)
	
	var item: String = action.get("item", "Berries")
	
	if market.has_active_order(agent.id, item, "sell"):
		return true
	
	var qty: int
	match item:
		"Berries":
			qty = agent.get_available_item(item) - tuning.get("target_food_buffer", 5)
		"Logs":
			qty = agent.get_available_item(item) - tuning.get("sell_logs_over", 5)
		"Ore":
			qty = agent.get_available_item(item) - tuning.get("sell_ore_over", 3)
		"Planks":
			qty = agent.get_available_item(item) - tuning.get("sell_planks_over", 15)
		"CookedMeal":
			qty = agent.get_available_item(item) - tuning.get("sell_meals_over", 8)
		_:
			qty = agent.get_available_item(item)
	
	if qty <= 0:
		return true
	
	qty = mini(qty, agent.get_available_item(item))
	if qty <= 0:
		return true
	
	if not agent.lock_item(item, qty):
		return true

	var global_berry_stock := world.get_total_stock("berry")
	var food_scarce: bool = global_berry_stock < int(tuning.get("berry_scarcity_threshold", 100))

	var price: int = agent.calculate_ask_price(item, market, tuning, food_scarce)
	var ttl: int = tuning.get("order_ttl_ticks", 48)
	var order := market.create_order("sell", item, qty, price, agent.id, current_tick, ttl)
	market.place_sell_order(order)
	
	return true

## Queue a crafting job
static func _execute_queue_craft(agent: Agent, action: Dictionary, world: World,
								   crafting: Crafting, recipes: Dictionary, 
								   state: SimState, tuning: Dictionary = {}) -> bool:
	var recipe_id: String = action.get("recipe_id", "")
	var workshop_id: int = action.get("workshop_id", -1)
	
	var recipe: Recipe = recipes.get(recipe_id)
	var is_handheld: bool = recipe != null and recipe.station == "hand"
	var workshop: Workshop = world.get_workshop_by_id(workshop_id)
	
	if recipe == null:
		return true

	if recipe.tier == "advanced":
		if is_handheld:
			return true
		if not world.has_advanced_workshop():
			return true
	
	if not is_handheld:
		if workshop == null or not workshop.is_ready() or not workshop.has_queue_space():
			return true
		if recipe.tier == "advanced" and workshop.workshop_type == "general":
			return true
		if not agent.is_at_workshop(workshop):
			return true
		
		# Check workshop permissions
		var access_result = workshop.can_agent_use(agent, state)
		if not access_result.get("allowed", false):
			# Silently block workshop access (could add logging later)
			return true
		
		# Check usage fee affordability
		if not workshop.can_afford_usage(agent):
			return true
	
	if not Crafting.can_craft(agent, recipe):
		return true
	
	# Drain stamina for crafting
	var craft_cost: float = tuning.get("stamina_drain_craft", 3.0)
	agent.drain_stamina(craft_cost)
	
	# Add crafting skill XP and general experience
	var skill_xp_amount: float = tuning.get("skill_xp_per_action", 0.1)
	agent.add_skill_xp("crafting", skill_xp_amount * 2.0, tuning)  # Crafting gives more XP
	
	# Add general experience for crafting (more than gathering)
	agent.add_experience(2, tuning)  # 2 XP for crafting
	
	if is_handheld:
		Crafting.consume_inputs(agent, recipe)
		Crafting.give_outputs(agent, recipe)
	else:
		Crafting.consume_inputs(agent, recipe)
		var job := crafting.create_job(recipe, agent.id, workshop.id)
		workshop.add_job(job)
	
	return true

## Accept a contract
static func _execute_accept_contract(agent: Agent, action: Dictionary, 
									 contracts_system: ContractsSystem, current_tick: int, state: SimState) -> bool:
	var contract_id: int = action.get("contract_id", -1)
	
	if contracts_system.accept_contract(contract_id, agent, current_tick, state):
		agent.active_contract_id = contract_id
	
	return true

## Deliver items for a contract
static func _execute_deliver_contract(agent: Agent, action: Dictionary,
									  contracts_system: ContractsSystem, state: SimState) -> bool:
	var contract_id: int = action.get("contract_id", -1)
	var contract := contracts_system.get_contract(contract_id)
	
	if contract == null:
		agent.active_contract_id = -1
		return true
	
	if not agent.is_at(contract.delivery_pos_x, contract.delivery_pos_y):
		return true
	
	if contracts_system.complete_delivery(contract_id, agent, state):
		agent.active_contract_id = -1
	
	return true

## Calculate distance to node
static func distance_to_node(agent: Agent, node: ResourceNode) -> int:
	return absi(agent.pos_x - node.pos_x) + absi(agent.pos_y - node.pos_y)

# ============================================
# NEW ACTION EXECUTORS FOR EXPANDED BRAIN
# ============================================

## Rest action - recover stamina
static func _execute_rest(agent: Agent, tuning: Dictionary) -> bool:
	var recover_amount: float = tuning.get("stamina_recover_rest", 5.0)
	agent.recover_stamina(recover_amount)
	return true

## Sleep action - enhanced stamina recovery (at home/safe location)
static func _execute_sleep(agent: Agent, tuning: Dictionary) -> bool:
	var recover_amount: float = tuning.get("stamina_recover_sleep", 10.0)
	agent.recover_stamina(recover_amount)
	return true

## Explore action - move in exploration direction to find new resources
static func _execute_explore(agent: Agent, action: Dictionary, world: World, 
							 tuning: Dictionary, current_tick: int) -> bool:
	var direction: int = action.get("direction", 0)  # 0-7 compass direction
	var step: int = tuning.get("exploration_step_distance", 5)
	
	# Direction vectors: N, NE, E, SE, S, SW, W, NW
	var dx_table := [0, 1, 1, 1, 0, -1, -1, -1]
	var dy_table := [-1, -1, 0, 1, 1, 1, 0, -1]
	
	var dx: int = dx_table[direction % 8]
	var dy: int = dy_table[direction % 8]
	
	var target_x: int = clampi(agent.pos_x + dx * step, 0, world.width - 1)
	var target_y: int = clampi(agent.pos_y + dy * step, 0, world.height - 1)
	
	# Move one step toward exploration target
	var result := _move_toward(agent, target_x, target_y, world, tuning)
	
	# Remember any resources found at current position
	var node_at := world.get_node_at(agent.pos_x, agent.pos_y)
	if node_at != null and node_at.stock > 0:
		agent.remember_resource(node_at.type, agent.pos_x, agent.pos_y, current_tick)
	
	# Update exploration direction periodically
	if result:  # Reached target
		agent.exploration_direction = (agent.exploration_direction + 1) % 8
	
	return result

## Claim tile action - claim land for the agent
static func _execute_claim_tile(agent: Agent, action: Dictionary, world: World,
							   state: SimState, tuning: Dictionary, current_tick: int) -> bool:
	var target_x: int = action.get("target_x", agent.pos_x)
	var target_y: int = action.get("target_y", agent.pos_y)
	
	# Must be at the tile
	if not agent.is_at(target_x, target_y):
		_move_toward(agent, target_x, target_y, world, tuning)
		return false
	
	# Check if already claimed
	var current_owner := world.get_claim_owner(target_x, target_y)
	if current_owner > 0:
		agent.is_claiming = false
		agent.claim_progress = 0
		return true  # Can't claim - already owned
	
	# Start or continue claiming
	var for_faction: bool = action.get("for_faction", false)
	var faction = null
	if for_faction and agent.faction_id > 0:
		# Find faction
		for f in state.factions:
			if f.id == agent.faction_id:
				faction = f
				break
	
	if not agent.is_claiming:
		var claim_cost: int = tuning.get("claim_cost_coins", 20)
		
		# Check claim token requirement (only for personal claims, not faction claims)
		if not for_faction and not agent.has_claim_tokens():
			return true  # No claim tokens available
			
		# Check affordability
		if for_faction and faction != null:
			if faction.treasury < claim_cost:
				return true # Faction broke
		elif agent.get_available_money() < claim_cost:
			return true  # Can't afford
			
		agent.is_claiming = true
		agent.claim_progress = 0
	
	# Drain stamina for claiming
	var claim_stamina: float = tuning.get("stamina_drain_claim", 2.0)
	agent.drain_stamina(claim_stamina)
	
	agent.claim_progress += 1
	var required_ticks: int = tuning.get("claim_ticks", 40)
	
	if agent.claim_progress >= required_ticks:
		# Complete claim
		var claim_cost: int = tuning.get("claim_cost_coins", 20)
		var owner_id = agent.id
		
		if for_faction and faction != null:
			faction.treasury -= claim_cost
			owner_id = World.faction_owner_id(faction.id)
		else:
			agent.debit_available_money(claim_cost)
			# Use claim token for personal claims
			agent.use_claim_tokens()
			# Grant experience for claiming
			agent.add_experience(5, tuning)  # 5 XP for claiming land
			
		world.set_claim_owner(target_x, target_y, owner_id)
		agent.is_claiming = false
		agent.claim_progress = 0
		return true
	
	return false

## Build workshop action - construct a workshop at position
static func _execute_build_workshop(agent: Agent, action: Dictionary, world: World,
								tuning: Dictionary, current_tick: int) -> bool:
	var target_x: int = action.get("target_x", agent.pos_x)
	var target_y: int = action.get("target_y", agent.pos_y)
	
	# Must be at the location
	if not agent.is_at(target_x, target_y):
		_move_toward(agent, target_x, target_y, world, tuning)
		return false
	
	# Check if workshop already exists at this location
	if world.has_workshop_at(target_x, target_y):
		agent.clear_goal()
		return true
	
	# Determine what workshop type to build based on available materials
	var workshop_type = "general"
	var planks_needed = tuning.get("workshop_build_planks", 10)
	var stone_needed = 0
	
	# Check for specialized workshop materials
	var carpenter_planks = tuning.get("workshop_carpenter_planks", 12)
	var carpenter_stone = tuning.get("workshop_carpenter_stone", 5)
	var smithy_stone = tuning.get("workshop_smithy_stone", 15)
	var smithy_metal = tuning.get("workshop_smithy_metal", 8)
	
	# Try to build best available workshop type
	if agent.has_available_item("Planks", carpenter_planks) and agent.has_available_item("Stone", carpenter_stone):
		workshop_type = "carpenter"
		planks_needed = carpenter_planks
		stone_needed = carpenter_stone
	elif agent.has_available_item("Stone", smithy_stone) and agent.has_available_item("MetalIngot", smithy_metal):
		workshop_type = "smithy"
		planks_needed = 0
		stone_needed = smithy_stone
	elif agent.has_available_item("Planks", planks_needed):
		workshop_type = "general"
	
	if not agent.has_tool("Mallet"):
		return true # Need Mallet to build
	
	# Drain stamina for building
	var build_stamina: float = tuning.get("stamina_drain_build", 5.0)
	agent.drain_stamina(build_stamina)
	
	# Consume materials and build
	agent.remove_item("Planks", planks_needed)
	if stone_needed > 0:
		agent.remove_item("Stone", stone_needed)
	if workshop_type == "smithy":
		agent.remove_item("MetalIngot", smithy_metal)
	
	var workshop := Workshop.new()
	workshop.pos_x = target_x
	workshop.pos_y = target_y
	workshop.built_by = agent.id
	workshop.build_start_tick = current_tick
	workshop.build_ticks_remaining = tuning.get("workshop_build_ticks", 80)
	
	# Set workshop type and ownership
	workshop.workshop_type = workshop_type
	workshop.owner_id = agent.id
	workshop.access_policy = "private"  # Personal workshops default to private
	
	# Set efficiency bonus based on type
	match workshop_type:
		"carpenter":
			workshop.efficiency_bonus = 0.8  # Faster wood crafting
		"smithy":
			workshop.efficiency_bonus = 0.8  # Faster metal crafting
		"kitchen":
			workshop.efficiency_bonus = 0.7  # Faster food crafting
		_:
			workshop.efficiency_bonus = 1.0  # Standard speed
	
	world.add_workshop(workshop)
	
	# Grant experience for building
	var xp_reward = 10
	if workshop_type != "general":
		xp_reward = 15  # Bonus XP for specialized workshops
	agent.add_experience(xp_reward, tuning)
	
	agent.clear_goal()
	return true

## Vote action - cast vote on faction proposal
static func _execute_vote(agent: Agent, action: Dictionary, state: SimState) -> bool:
	var proposal_id: int = action.get("proposal_id", -1)
	var vote_for: bool = action.get("vote_for", true)
	
	if agent.faction_id == 0:
		return true  # Not in faction
	
	# Find agent's faction
	var faction: Faction = null
	for f in state.factions:
		if f.id == agent.faction_id:
			faction = f
			break
	
	if faction == null:
		return true
	
	# Cast vote
	faction.vote_on_proposal(proposal_id, agent.id, vote_for)
	
	state.log_event("vote_cast", {
		"agent_id": agent.id,
		"faction_id": faction.id,
		"proposal_id": proposal_id,
		"vote_for": vote_for
	})
	
	return true

## Contribute to communal project - add resources to a faction project
static func _execute_contribute_project(agent: Agent, action: Dictionary, 
									state: SimState) -> bool:
	var project_id: int = action.get("project_id", -1)
	var item: String = action.get("item", "")
	var qty: int = action.get("qty", 1)
	
	if project_id < 0 or item == "" or qty <= 0:
		return true
	
	# Use the communal projects system to contribute
	var contributed := state.communal_projects.contribute_to_project(project_id, agent, item, qty, state)
	if contributed < qty:
		state.communal_projects.release_project_reservation(project_id, item, qty - contributed)
	return contributed > 0

static func _execute_withdraw_stockpile(agent: Agent, action: Dictionary, state: SimState) -> bool:
	var structure_id: int = action.get("structure_id", -1)
	var item: String = action.get("item", "")
	var qty: int = action.get("qty", 1)
	var require_reserved: bool = action.get("require_reserved", false)
	if structure_id < 0 or item == "" or qty <= 0:
		return true
	var structure := state.structures.get_structure(structure_id)
	if structure == null:
		return true
	if not agent.is_at(structure.pos_x, structure.pos_y):
		return false
	var to_withdraw := qty
	if require_reserved:
		to_withdraw = structure.release_reserved_item(item, qty)
		if to_withdraw <= 0:
			return true
	var removed := structure.remove_item(item, to_withdraw)
	if removed > 0:
		agent.add_item(item, removed)
	return true

static func _execute_deposit_stockpile(agent: Agent, action: Dictionary, state: SimState) -> bool:
	var structure_id: int = action.get("structure_id", -1)
	var item: String = action.get("item", "")
	var qty: int = action.get("qty", 1)
	if structure_id < 0 or item == "" or qty <= 0:
		return true
	var structure := state.structures.get_structure(structure_id)
	if structure == null:
		return true
	if not agent.is_at(structure.pos_x, structure.pos_y):
		return false
	var available := agent.get_available_item(item)
	var to_deposit := mini(available, qty)
	if to_deposit <= 0:
		return true
	var free_capacity := structure.get_free_capacity()
	if free_capacity <= 0:
		return true
	var final_qty := mini(to_deposit, free_capacity)
	agent.remove_item(item, final_qty)
	structure.add_item(item, final_qty)
	return true

static func _execute_build_site(agent: Agent, action: Dictionary, state: SimState, world: World, tuning: Dictionary) -> bool:
	var project_id: int = action.get("project_id", -1)
	if project_id < 0:
		return true
	var project := state.communal_projects.get_project(project_id)
	if project == null:
		return true
	if not agent.is_at(project.pos_x, project.pos_y):
		_move_toward(agent, project.pos_x, project.pos_y, world, tuning)
		return false
	return true

## Build road action - construct a road at position
static func _execute_build_road(agent: Agent, action: Dictionary, world: World,
							state: SimState, tuning: Dictionary, current_tick: int) -> bool:
	var target_x: int = action.get("target_x", agent.pos_x)
	var target_y: int = action.get("target_y", agent.pos_y)
	
	# Must be at the location
	if not agent.is_at(target_x, target_y):
		_move_toward(agent, target_x, target_y, world, tuning)
		return false
	
	# Already has road?
	if world.has_road(target_x, target_y):
		return true
	
	# Start a road project if none exists
	var faction_id = agent.faction_id
	var existing_projects = state.communal_projects.get_projects_near(target_x, target_y, 0)
	var project = null
	for p in existing_projects:
		if p.project_type == "road":
			project = p
			break
	
	if project == null:
		if not agent.has_tool("Shovel"):
			return true # Need Shovel to start/work on road
		project = state.communal_projects.start_project(faction_id, "road", target_x, target_y, agent.id, current_tick, state)
	
	if project != null:
		# Contribute Planks automatically if available
		if not agent.has_tool("Shovel"):
			return true # Need Shovel to contribute to road work
		var planks_needed = project.requirements.get("Planks", 0) - project.contributions.get("Planks", 0)
		if planks_needed > 0 and agent.has_available_item("Planks", 1):
			state.communal_projects.contribute_to_project(project.id, agent, "Planks", 1, state)
			
	return true
