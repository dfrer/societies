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
	pipeline.add_system(ClaimsSystem.new())
	pipeline.add_system(OrganizationsSystem.new())
	pipeline.add_system(JobBoardSystem.new())
	pipeline.add_system(GovernanceSystem.new())
	pipeline.add_system(MetricsSystem.new())
	pipeline.add_system(TimeSystem.new())
	pipeline.add_system(WorkshopSystem.new())
	pipeline.add_system(TaskProjectSystem.new())
	pipeline.add_system(AgentsSystem.new())
	pipeline.add_system(EconomyResolutionSystem.new())


## Initialize a new simulation with the given seed
func init_new(seed_value: int) -> void:
	state = SimState.new()
	state.rng.init_seed(seed_value)
	
	_load_config()
	
	# DEBUG: Trace first few RNG values
	# print("DEBUG: init_new seed: ", seed_value)
	# var test_rand = state.rng.randi()
	# print("DEBUG: First randi: ", test_rand)
	
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
	var npc_count: int = state.tuning.get("npc_count", 10)
	for i in range(npc_count):
		var roll := state.rng.randf()
		var role := "crafter" if roll < crafter_ratio else "gatherer"
		var npc := _create_agent(false, role)
		state.add_agent(npc)

	_init_organization()

	# Claim some tiles for early agents (for testing enforcement)
	_claim_starting_tiles()

func _load_config() -> void:
	var items_data = Serializers.load_json_file("res://config/items.json")
	state.items = items_data if items_data else _get_default_items()
	
	var tuning_config := TuningConfig.load_from_file("res://config/tuning.json")
	var tuning_errors := tuning_config.validate()
	if tuning_errors.size() > 0:
		push_error("TuningConfig validation failed:\n- " + "\n- ".join(tuning_errors))
	state.tuning_config = tuning_config
	state.tuning = tuning_config.get_data_with_defaults()
	
	var recipes_data = Serializers.load_json_file("res://config/recipes.json")
	if recipes_data and recipes_data.has("recipes"):
		for recipe_dict in recipes_data["recipes"]:
			var recipe := Recipe.from_dict(recipe_dict)
			state.recipes[recipe.id] = recipe
	state.update_recipe_sorted_ids()

func _get_default_items() -> Dictionary:
		return {
		"Berries": {"nutrition": 20, "type": "food", "base_value": 10},
		"Logs": {"type": "material", "base_value": 15},
		"Ore": {"type": "material", "base_value": 25},
		"Stone": {"type": "material", "base_value": 12},
		"Planks": {"type": "material", "base_value": 35},
		"Workbench": {"type": "structure", "base_value": 90},
		"CookedMeal": {"nutrition": 40, "type": "food", "base_value": 30},
		"MetalIngot": {"type": "material", "base_value": 60},
		"WoodenAxe": {"type": "tool", "tool_tag": "axe", "base_value": 80, "description": "Basic wooden axe for harvesting trees"},
		"WoodenPickaxe": {"type": "tool", "tool_tag": "pickaxe", "base_value": 90, "description": "Basic wooden pickaxe for mining ore"},
		"Axe": {"type": "tool", "tool_tag": "axe", "base_value": 150},
		"Pickaxe": {"type": "tool", "tool_tag": "pickaxe", "base_value": 180}
	}

func _spawn_resource_nodes() -> void:
	var occupied := {}
	var berry_max_stock: int = state.get_tuning_int("berry_max_stock", 10)
	var tree_max_stock: int = state.get_tuning_int("tree_max_stock", 20)
	var ore_max_stock: int = state.get_tuning_int("ore_max_stock", 30)
	var stone_max_stock: int = state.get_tuning_int("stone_max_stock", 40)
	var node_spawn_attempts: int = state.get_tuning_int("node_spawn_attempts", 100)

	var berry_start_min: int = mini(state.get_tuning_int("berry_start_min", 6), berry_max_stock)
	var berry_start_max: int = mini(state.get_tuning_int("berry_start_max", berry_max_stock), berry_max_stock)
	var tree_start_min: int = mini(state.get_tuning_int("tree_start_min", 10), tree_max_stock)
	var tree_start_max: int = mini(state.get_tuning_int("tree_start_max", tree_max_stock), tree_max_stock)
	var ore_start_min: int = mini(state.get_tuning_int("ore_start_min", 20), ore_max_stock)
	var ore_start_max: int = mini(state.get_tuning_int("ore_start_max", ore_max_stock), ore_max_stock)
	var stone_start_min: int = mini(state.get_tuning_int("stone_start_min", 20), stone_max_stock)
	var stone_start_max: int = mini(state.get_tuning_int("stone_start_max", stone_max_stock), stone_max_stock)

	if berry_start_min > berry_start_max:
		var temp := berry_start_min
		berry_start_min = berry_start_max
		berry_start_max = temp
	if tree_start_min > tree_start_max:
		var temp := tree_start_min
		tree_start_min = tree_start_max
		tree_start_max = temp
	if ore_start_min > ore_start_max:
		var temp := ore_start_min
		ore_start_min = ore_start_max
		ore_start_max = temp
	if stone_start_min > stone_start_max:
		var temp := stone_start_min
		stone_start_min = stone_start_max
		stone_start_max = temp

	for i in range(state.get_tuning_int("berry_nodes_count", 120)):
		var node := _spawn_node("berry", berry_max_stock, berry_start_min, berry_start_max, occupied, node_spawn_attempts)
		if node: state.world.add_resource_node(node)
	for i in range(state.get_tuning_int("tree_nodes_count", 200)):
		var node := _spawn_node("tree", tree_max_stock, tree_start_min, tree_start_max, occupied, node_spawn_attempts)
		if node: state.world.add_resource_node(node)
	for i in range(state.get_tuning_int("ore_nodes_count", 40)):
		var node := _spawn_node("ore", ore_max_stock, ore_start_min, ore_start_max, occupied, node_spawn_attempts)
		if node: state.world.add_resource_node(node)
	# Add stone nodes (permanent resource)
	var stone_count = state.get_tuning_int("stone_nodes_count", 60)
	for i in range(stone_count):
		var node := _spawn_node("stone", stone_max_stock, stone_start_min, stone_start_max, occupied, node_spawn_attempts)
		if node: state.world.add_resource_node(node)

func _spawn_node(type: String, max_stock: int, min_start: int, max_start: int, occupied: Dictionary, spawn_attempts: int) -> ResourceNode:
	for attempt in range(spawn_attempts):
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
	agent.set_hunger(state.tuning.get("initial_hunger", 100.0))
	agent.set_stamina(state.tuning.get("initial_stamina", 100.0))
	var initial_skill: float = state.tuning.get("initial_skill_level", 1.0)
	for skill_name in agent.skills:
		agent.skills[skill_name] = initial_skill
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

func _init_organization() -> void:
	var center := Vector2i(state.get_tuning_int("market_pos_x", 0), state.get_tuning_int("market_pos_y", 0))
	var org := Organization.new()
	org.init_organization(state.next_organization_id, "Settlement", center, state.get_tuning_int("organization_starting_treasury", 200))
	state.next_organization_id += 1
	for agent in state.agents:
		org.add_member(agent.id)
	state.organizations.append(org)
	if state.world.is_valid(center.x, center.y) and state.world.get_claim_owner(center.x, center.y) == 0:
		state.world.set_claim_owner(center.x, center.y, org.get_owner_id())
		state.world.set_zone_tag(center.x, center.y, "town_center")

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
