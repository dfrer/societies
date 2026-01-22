## TestFixtures - Factory methods for creating standardized test objects
class_name TestFixtures
extends RefCounted

## Create a minimal SimState for unit testing
static func make_sim_state(seed_val: int = 12345) -> SimState:
	var state := SimState.new()
	state.rng.init_seed(seed_val)
	
	# Load configs
	var items_data = Serializers.load_json_file("res://config/items.json")
	state.items = items_data if items_data else _get_default_items()
	
	var tuning_data = Serializers.load_json_file("res://config/tuning.json")
	if tuning_data:
		var tuning_config := TuningConfig.new()
		tuning_config.load_from_dict(tuning_data)
		state.tuning_config = tuning_config
		state.tuning = tuning_config.get_data_with_defaults()
	
	# CRITICAL: Initialize world dimensions to prevent bounds errors
	var world_w: int = 96
	var world_h: int = 96
	if tuning_data and tuning_data.has("world_w"):
		world_w = int(tuning_data["world_w"])
	if tuning_data and tuning_data.has("world_h"):
		world_h = int(tuning_data["world_h"])
	state.world.init_world(world_w, world_h)
	
	return state

## Create a test agent with optional overrides
static func make_agent(overrides: Dictionary = {}) -> Agent:
	var agent := Agent.new()
	agent.id = overrides.get("id", 1)
	agent.pos_x = overrides.get("pos_x", 48)
	agent.pos_y = overrides.get("pos_y", 48)
	agent.money = overrides.get("money", 100)
	agent.set_hunger(overrides.get("hunger", 80.0))
	agent.faction_id = overrides.get("faction_id", 0)
	agent.role = overrides.get("role", "gatherer")
	agent.risk_tolerance = overrides.get("risk_tolerance", 0.5)
	
	# Apply personality overrides
	if overrides.has("personality"):
		for key in overrides["personality"]:
			agent.personality[key] = overrides["personality"][key]
	
	# Apply inventory overrides
	if overrides.has("inventory"):
		for item in overrides["inventory"]:
			agent.inventory[item] = overrides["inventory"][item]
	
	return agent

## Create a test contract
static func make_contract(issuer_id: int, item: String, qty: int, payout: int, 
						  deadline_tick: int = 100, overrides: Dictionary = {}) -> Contract:
	var contract := Contract.new()
	contract.id = overrides.get("id", 1)
	contract.issuer_id = issuer_id
	contract.item = item
	contract.qty = qty
	contract.payout = payout
	contract.deadline_tick = deadline_tick
	contract.delivery_pos_x = overrides.get("delivery_pos_x", 48)
	contract.delivery_pos_y = overrides.get("delivery_pos_y", 48)
	contract.status = overrides.get("status", Contract.STATUS_POSTED)
	contract.worker_id = overrides.get("worker_id", 0)
	contract.created_tick = overrides.get("created_tick", 0)
	return contract

## Create a test market with items initialized
static func make_market(items: Dictionary = {}) -> Market:
	var market := Market.new()
	if items.is_empty():
		items = _get_default_items()
	market.init_prices(items)
	return market

## Create test laws with optional overrides
static func make_laws(overrides: Dictionary = {}) -> Laws:
	var laws := Laws.new()
	laws.harvest_permit_required = overrides.get("harvest_permit_required", false)
	laws.build_permit_required = overrides.get("build_permit_required", false)
	laws.sales_tax_rate = overrides.get("sales_tax_rate", 0)
	# Note: Laws class doesn't have tariff_rate property
	return laws

## Create a test faction
static func make_faction(id: int, overrides: Dictionary = {}) -> Faction:
	var faction := Faction.new()
	faction.id = id
	faction.name = overrides.get("name", "Test Faction %d" % id)
	faction.home_pos = overrides.get("home_pos", Vector2i(48, 48))
	faction.treasury = overrides.get("treasury", 100)
	
	if overrides.has("members"):
		for member_id in overrides["members"]:
			faction.add_member(member_id)
	
	return faction

## Create an RNG with a fixed seed for deterministic testing
static func make_rng(seed_val: int = 12345) -> RNG:
	var rng := RNG.new()
	rng.init_seed(seed_val)
	return rng

## Create a minimal world for testing
static func make_world(width: int = 16, height: int = 16) -> World:
	var world := World.new()
	world.init_world(width, height)
	return world

## Create an enforcement instance
static func make_enforcement() -> Enforcement:
	return Enforcement.new()

## Get default items dictionary
static func _get_default_items() -> Dictionary:
	return {
		"Berries": {"nutrition": 20, "type": "food", "base_value": 5},
		"Logs": {"type": "material", "base_value": 10},
		"Ore": {"type": "material", "base_value": 15},
		"Planks": {"type": "material", "base_value": 20},
		"CookedMeal": {"nutrition": 40, "type": "food", "base_value": 30},
		"MetalIngot": {"type": "material", "base_value": 60},
		"Axe": {"type": "tool", "base_value": 150},
		"Pickaxe": {"type": "tool", "base_value": 180}
	}

## Create a sim for integration testing (full initialization)
static func make_sim(seed_val: int = 12345) -> Sim:
	var sim := Sim.new()
	sim.init_new(seed_val)
	return sim

## Helper to run simulation for N ticks
static func run_ticks(sim: Sim, ticks: int) -> void:
	sim.step(ticks)
