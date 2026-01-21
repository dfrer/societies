## Sim - main simulation controller
class_name Sim
extends RefCounted


var state: SimState = null
var pipeline: SimPipeline = null

func _init() -> void:
	state = SimState.new()
	_init_pipeline()

func _init_pipeline() -> void:
	pipeline = SimPipeline.new()
	pipeline.add_system(EnvironmentSystem.new())
	pipeline.add_system(EconomySystem.new())
	pipeline.add_system(GovernanceSystem.new())
	pipeline.add_system(MetricsSystem.new())
	pipeline.add_system(TimeSystem.new())
	pipeline.add_system(WorkshopSystem.new())
	pipeline.add_system(AgentsSystem.new())
	pipeline.add_system(EconomyResolutionSystem.new())


## Initialize a new simulation with the given seed
func init_new(seed_value: int) -> void:
	state = SimState.new()
	state.rng.init_seed(seed_value)
	
	_load_config()
	
	# Initialize default laws for unclaimed land (owner 0)
	var default_laws := Laws.new()
	default_laws.init_from_tuning(state.tuning)
	state.laws_by_owner[0] = default_laws
	
	state.market.init_prices(state.items)
	
	var world_w: int = state.tuning.get("world_w", 96)
	var world_h: int = state.tuning.get("world_h", 96)
	state.world.init_world(world_w, world_h)
	
	_spawn_resource_nodes()
	_spawn_initial_workshops()
	
	var player := _create_agent(true, "gatherer")
	state.add_agent(player)
	
	var crafter_ratio: float = state.tuning.get("crafter_ratio", 0.3)
	for i in range(10):
		var roll := state.rng.randf()
		var role := "crafter" if roll < crafter_ratio else "gatherer"
		var npc := _create_agent(false, role)
		state.add_agent(npc)
	
	# Claim some tiles for early agents (for testing enforcement)
	_claim_starting_tiles()

func _load_config() -> void:
	var items_data = Serializers.load_json_file("res://config/items.json")
	state.items = items_data if items_data else _get_default_items()
	
	var tuning_data = Serializers.load_json_file("res://config/tuning.json")
	state.tuning = tuning_data if tuning_data else _get_default_tuning()
	
	var recipes_data = Serializers.load_json_file("res://config/recipes.json")
	if recipes_data and recipes_data.has("recipes"):
		for recipe_dict in recipes_data["recipes"]:
			var recipe := Recipe.from_dict(recipe_dict)
			state.recipes[recipe.id] = recipe

func _get_default_items() -> Dictionary:
		return {
		"Berries": {"nutrition": 20, "type": "food", "base_value": 10},
		"Logs": {"type": "material", "base_value": 15},
		"Ore": {"type": "material", "base_value": 25},
		"Stone": {"type": "material", "base_value": 12},
		"Planks": {"type": "material", "base_value": 35},
		"CookedMeal": {"nutrition": 40, "type": "food", "base_value": 30},
		"MetalIngot": {"type": "material", "base_value": 60},
		"Axe": {"type": "tool", "base_value": 150},
		"Pickaxe": {"type": "tool", "base_value": 180}
	}

func _get_default_tuning() -> Dictionary:
	return {
		"world_w": 96, "world_h": 96, "ticks_per_day": 24,
		"hunger_drain_per_tick": 0.5, "eat_threshold": 50, "berry_nutrition": 20,
		"meal_nutrition": 40, "starting_money": 100, "starting_berries": 10,
		"berry_regen_per_day": 3, "tree_regen_per_day": 2,
		"pollution_impact": 0.5, "pollution_decay_per_day": 0.05, "pollution_per_ore": 0.01,
		"urgent_hunger_threshold": 25, "target_food_buffer": 5,
		"berry_nodes_count": 120, "tree_nodes_count": 200, "ore_nodes_count": 40,
		"market_pos_x": 48, "market_pos_y": 48, "market_match_interval_ticks": 6,
		"order_ttl_ticks": 48, "price_ema_alpha": 0.2,
		"bid_scarcity_strength": 0.6, "ask_surplus_strength": 0.6,
		"min_price": 1, "max_price": 1000,
		"sell_surplus_food_over": 15, "sell_logs_over": 5, "sell_ore_over": 3,
		"sell_planks_over": 15, "sell_meals_over": 8,
		"workshop_start_count": 1, "workshop_build_planks": 10, "workshop_build_ticks": 80,
		"crafter_ratio": 0.3, "crafter_planks_buffer": 10, "crafter_meal_buffer": 6,
		"craft_profit_margin": 1.15, "axe_tree_bonus": 2, "pickaxe_ore_bonus": 2,
		"daily_contract_post_chance": 0.15, "contract_deadline_days": 2,
		"contract_payout_multiplier": 1.2, "contract_accept_min_profit": 5,
		"max_active_contracts_per_agent": 1,
		"claim_cost_coins": 20, "claim_ticks": 40, "detect_chance": 0.8,
		"fine_base": 10, "market_ban_days_on_repeat": 2, "repeat_threshold": 3,
		"violation_window_days": 2, "harvest_permit_required_default": true,
		"build_permit_required_default": true
	}

func _spawn_resource_nodes() -> void:
	var occupied := {}
	for i in range(state.tuning.get("berry_nodes_count", 120)):
		var node := _spawn_node("berry", 10, 6, 10, occupied)
		if node: state.world.add_resource_node(node)
	for i in range(state.tuning.get("tree_nodes_count", 200)):
		var node := _spawn_node("tree", 20, 10, 20, occupied)
		if node: state.world.add_resource_node(node)
	for i in range(state.tuning.get("ore_nodes_count", 40)):
		var node := _spawn_node("ore", 30, 20, 30, occupied)
		if node: state.world.add_resource_node(node)
	# Add stone nodes (permanent resource)
	var stone_count = state.tuning.get("stone_nodes_count", 60)
	for i in range(stone_count):
		var node := _spawn_node("stone", 40, 20, 40, occupied)
		if node: state.world.add_resource_node(node)

func _spawn_node(type: String, max_stock: int, min_start: int, max_start: int, occupied: Dictionary) -> ResourceNode:
	for attempt in range(100):
		var x := state.rng.randi_range(0, state.world.width - 1)
		var y := state.rng.randi_range(0, state.world.height - 1)
		var key := "%d,%d" % [x, y]
		if not occupied.has(key):
			occupied[key] = true
			var node := ResourceNode.new()
			node.id = state.next_node_id
			state.next_node_id += 1
			node.type = type
			node.pos_x = x
			node.pos_y = y
			node.max_stock = max_stock
			node.stock = state.rng.randi_range(min_start, max_start)
			node.set_type_spoilage()
			# Add some quality variation
			node.quality = state.rng.randf_range(0.8, 1.2)
			return node
	return null

func _spawn_initial_workshops() -> void:
	var count: int = state.tuning.get("workshop_start_count", 1)
	var market_x: int = state.tuning.get("market_pos_x", 48)
	var market_y: int = state.tuning.get("market_pos_y", 48)
	
	for i in range(count):
		var workshop := Workshop.new()
		workshop.id = state.next_workshop_id
		state.next_workshop_id += 1
		workshop.pos_x = market_x + i
		workshop.pos_y = market_y
		workshop.is_built = true
		state.world.add_workshop(workshop)

func _create_agent(is_player: bool, role: String) -> Agent:
	var agent := Agent.new()
	agent.id = state.next_agent_id
	state.next_agent_id += 1
	agent.is_player = is_player
	agent.role = role
	agent.pos_x = state.rng.randi_range(0, state.world.width - 1)
	agent.pos_y = state.rng.randi_range(0, state.world.height - 1)
	agent.money = state.tuning.get("starting_money", 100)
	agent.add_item("Berries", state.tuning.get("starting_berries", 10))
	agent.set_hunger(100.0)
	agent.risk_tolerance = state.rng.randf()  # Random risk tolerance
	agent.eco_concern = state.rng.randf()  # Random environmental concern
	
	# Initialize claim and progression system
	agent.claim_tokens = state.tuning.get("starting_agents_claim_count", 25)
	agent.claim_level = 1
	agent.experience = 0
	# Set personality deterministically using simulation RNG
	agent.personality["greed"] = state.rng.randf_range(0.3, 1.0)
	agent.personality["laziness"] = state.rng.randf_range(0.1, 0.8)
	agent.personality["social_need"] = state.rng.randf_range(0.2, 0.9)
	agent.personality["risk_tolerance"] = state.rng.randf_range(0.1, 0.9)
	return agent

## Claim some resource node tiles for the first few agents
func _claim_starting_tiles() -> void:
	var agents_to_claim := mini(5, state.agents.size())
	for i in range(agents_to_claim):
		var agent: Agent = state.agents[i]
		# Find a random unclaimed resource node
		for node in state.world.resource_nodes:
			if not state.world.is_claimed(node.pos_x, node.pos_y):
				state.world.set_claim_owner(node.pos_x, node.pos_y, agent.id)
				# Create laws for this owner
				if not state.laws_by_owner.has(agent.id):
					var agent_laws := Laws.new()
					agent_laws.init_from_tuning(state.tuning)
					state.laws_by_owner[agent.id] = agent_laws
				break

## Run simulation for n ticks
func step(n: int = 1) -> void:
	for i in range(n):
		_tick()

func _tick() -> void:
	pipeline.execute(self, state)

func _daily_update() -> void:
	# Deprecated: Logic moved to Systems
	pass

func checksum() -> String:
	var state_dict := state.to_dict()
	state_dict.erase("metrics_history")
	# Sanitize events to ensure int/float consistency for comparison
	state_dict["events"] = _sanitize_events_for_checksum(state_dict.get("events", []))

	var json_str := Serializers.to_sorted_json(state_dict)
	return Serializers.sha256_hash(json_str)


## Ensure events have consistent types for checksum comparison
func _sanitize_events_for_checksum(events: Array) -> Array:
	var int_keys := ["tick", "day", "agent_id", "contract_id", "issuer_id", "worker_id",
					 "faction_id", "founder_id", "payout", "qty", "deadline", "refund",
					 "fine", "owner_id", "proposal_id", "proposer_id", "tariff_rate",
					 "law_owner_id", "votes_for", "votes_against", "x", "y",
					 "sales_tax_rate", "fine_base", "fine_applied"]
	var result := []
	for event in events:
		var fixed_event := {}
		fixed_event["tick"] = int(event.get("tick", 0))
		fixed_event["type"] = event.get("type", "")
		var data: Dictionary = event.get("data", {})
		fixed_event["data"] = _sanitize_data_recursive(data, int_keys)
		result.append(fixed_event)
	return result


func _sanitize_data_recursive(data: Variant, int_keys: Array) -> Variant:
	if data is Dictionary:
		var fixed := {}
		for key in data:
			var val = data[key]
			if key in int_keys:
				fixed[key] = int(val)
			elif val is Dictionary:
				fixed[key] = _sanitize_data_recursive(val, int_keys)
			elif val is Array:
				fixed[key] = _sanitize_data_recursive(val, int_keys)
			else:
				fixed[key] = val
		return fixed
	elif data is Array:
		var fixed := []
		for item in data:
			if item is Dictionary:
				fixed.append(_sanitize_data_recursive(item, int_keys))
			elif item is float or item is int:
				fixed.append(int(item))
			else:
				fixed.append(item)
		return fixed
	else:
		return data

func save_json(path: String) -> bool:
	return Serializers.save_json_file(path, state.to_dict())

func load_json(path: String) -> bool:
	var data = Serializers.load_json_file(path)
	if data == null: return false
	state = SimState.from_dict(data)
	return true

func get_state() -> SimState: return state
func get_tick() -> int: return state.tick
func get_day() -> int: return state.tick / state.tuning.get("ticks_per_day", 24)
func get_agent_count() -> int: return state.agents.size()
func get_alive_agent_count() -> int:
	var count := 0
	for agent in state.agents:
		if agent.is_alive(): count += 1
	return count
func get_average_hunger() -> float: return state.get_average_hunger()
func get_total_money() -> int:
	var total := 0
	for agent in state.agents:
		total += agent.money + agent.locked_money
	return total

func get_total_wealth() -> int:
	var total := get_total_money()
	total += state.contracts_system.get_total_escrow()
	total += state.world_fines_sink
	total += state.taxes_collected
	# Faction money
	for f in state.factions:
		total += f.treasury
	return total

func get_total_item(item_name: String) -> int:
	var total := 0
	for agent in state.agents: total += agent.get_item_count(item_name)
	return total
func get_total_locked_item(item_name: String) -> int:
	var total := 0
	for agent in state.agents: total += agent.locked_inventory.get(item_name, 0)
	return total
func get_total_stock(type: String) -> int: return state.world.get_total_stock(type)
func get_average_pollution() -> float: return state.world.get_average_pollution()
func get_ref_price(item: String) -> float: return state.market.get_ref_price(item)
func get_total_trades() -> int: return state.market.total_trades
func get_trade_count(item: String) -> int: return state.market.get_trade_count(item)
func get_workshop_count() -> int: return state.world.get_workshop_count()
func get_crafted_count(item: String) -> int: return state.crafting.get_crafted_count(item)
func get_active_job_count() -> int: return Crafting.get_active_job_count(state.world.workshops)
func get_total_escrow() -> int: return state.contracts_system.get_total_escrow()
func get_contracts_posted() -> int: return state.contracts_system.stats.get("posted", 0)
func get_contracts_accepted() -> int: return state.contracts_system.stats.get("accepted", 0)
func get_contracts_completed() -> int: return state.contracts_system.stats.get("completed", 0)
func get_contracts_expired() -> int: return state.contracts_system.stats.get("expired", 0)
func get_active_contracts() -> Array: 
	var active := []
	for contract in state.contracts_system.contracts:
		if contract.is_active():
			active.append(contract)
	return active
func get_claimed_tiles_count() -> int: return state.world.get_claimed_tiles_count()
func get_violations_detected() -> int: return state.enforcement.violations_detected
func get_fines_collected() -> int: return state.enforcement.fines_collected
func get_banned_agents_count() -> int:
	var count := 0
	for agent in state.agents:
		if agent.is_market_banned(state.tick):
			count += 1
	return count
func get_claims_by_owner() -> Dictionary: return state.world.get_claims_by_owner()

## Faction getters
func get_faction_count() -> int: return state.factions.size()
func get_factions() -> Array: return state.factions
func get_taxes_collected() -> int: return state.taxes_collected
func get_world_fines_sink() -> int: return state.world_fines_sink

func get_faction_claims_count(faction_id: int) -> int:
	var owner_id := World.owner_id_for_faction(faction_id)
	var count := 0
	for tile_owner in state.world.tiles:
		if tile_owner == owner_id:
			count += 1
	return count

func get_active_proposals_count() -> int:
	var count := 0
	for faction in state.factions:
		count += faction.get_active_proposals(state.tick).size()
	return count

func get_resolved_proposals_count() -> int:
	return state.factions_system.stats.get("proposals_resolved", 0)
