## EconomyPlanner - handles contracts, resources, and progression goals
class_name EconomyPlanner
extends IAgentPlanner

var _world: World = null
var _recipes: Dictionary = {}

func get_priority() -> int:
	return 60

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	return add_primary_goal(agent, context.world, context.market, context.contracts_system, context.tuning, context.recipes, context.state)

func add_primary_goal(agent: Agent, world: World, market: Market,
		contracts_system: ContractsSystem, tuning: Dictionary,
		recipes: Dictionary, state: SimState = null) -> bool:
	_world = world
	_recipes = recipes

	# Resource competition (claim resources before others)
	if agent.has_claim_tokens() and _should_claim_resources(agent, world, tuning):
		agent.goal_stack.push_back({"type": "CLAIM_RESOURCES", "is_goal": true})
		return true

	# Contracts
	if agent.has_active_contract():
		var contract = contracts_system.get_contract(agent.active_contract_id)
		if contract and contract.status == Contract.STATUS_ACCEPTED:
			agent.goal_stack.push_back({"type": "FULFILL_CONTRACT", "contract_id": contract.id, "is_goal": true})
			return true
	else:
		var failed_request: Dictionary = agent.goal_data.get("failed_item_request", {})
		if not failed_request.is_empty():
			var item: String = failed_request.get("item", "")
			var qty: int = int(failed_request.get("qty", 1))
			var posting := _should_post_contract_for_item(agent, item, qty, market, tuning)
			if posting.get("should_post", false) and not contracts_system.has_active_contract_for_issuer("agent", agent.id, item):
				agent.goal_stack.push_back({
					"type": "POST_CONTRACT_FOR_NEED",
					"item": item,
					"qty": qty,
					"payout": int(posting.get("payout", 0)),
					"is_goal": true
				})
				agent.goal_data.erase("failed_item_request")
				return true
		var best_contract = contracts_system.find_best_contract(agent, market, tuning, world, recipes)
		if best_contract != null:
			agent.goal_stack.push_back({"type": "ACCEPT_CONTRACT", "contract_id": best_contract.id, "is_goal": true})
			return true

	return false

func add_progression_goal(agent: Agent, world: World, tuning: Dictionary, recipes: Dictionary, state: SimState = null) -> bool:
	# First tools
	if not agent.has_tool("Axe") and _has_allowed_recipe("Axe", recipes, world):
		agent.goal_stack.push_back({"type": "OBTAIN_ITEM", "item": "Axe", "qty": 1, "is_goal": true})
		return true

	# Additional tools
	if not agent.has_tool("Mallet") and _has_allowed_recipe("Mallet", recipes, world):
		agent.goal_stack.push_back({"type": "OBTAIN_ITEM", "item": "Mallet", "qty": 1, "is_goal": true})
		return true

	if agent.has_tool("Mallet") and _should_build_workshop(agent, world, tuning):
		agent.goal_stack.push_back({"type": "BUILD_STRATEGIC_WORKSHOP", "is_goal": true})
		return true

	if not agent.has_tool("Shovel") and _has_allowed_recipe("Shovel", recipes, world):
		agent.goal_stack.push_back({"type": "OBTAIN_ITEM", "item": "Shovel", "qty": 1, "is_goal": true})
		return true

	# Default: gather/craft profitable items
	agent.goal_stack.push_back({"type": "OBTAIN_ITEM", "item": "Planks", "qty": 1, "is_goal": true})
	return true

func _has_allowed_recipe(output_item: String, recipes: Dictionary, world: World) -> bool:
	for recipe_id in recipes:
		var recipe: Recipe = recipes[recipe_id]
		if recipe.outputs.has(output_item):
			if recipe.tier == "advanced":
				if recipe.station == "hand":
					continue
			if not _is_station_available(recipe, world):
				continue
			return true
	return false

func _is_station_available(recipe: Recipe, world: World) -> bool:
	if recipe.station == "hand" or recipe.station == "workbench":
		return true
	return world.has_workshop_type(recipe.station)

func _should_post_contract_for_item(agent: Agent, item: String, qty: int, market: Market, tuning: Dictionary) -> Dictionary:
	if item == "" or qty <= 0:
		return {"should_post": false, "payout": 0}

	var can_produce: bool = _can_agent_produce_item(agent, item)
	if can_produce:
		return {"should_post": false, "payout": 0}

	var ref_price: float = market.get_ref_price(item)
	var last_price: float = float(market.last_trade_price.get(item, ref_price))
	var has_sell_order := false
	for order in market.sell_orders:
		if order.get("item", "") == item and int(order.get("qty", 0)) > 0:
			has_sell_order = true
			break
	var supply_low: bool = (last_price > ref_price * 1.2) or not has_sell_order

	var payout: int = int(ceil(ref_price * qty * 1.5))
	if supply_low and agent.get_available_money() >= payout:
		return {"should_post": true, "payout": payout}
	return {"should_post": false, "payout": payout}

func _can_agent_produce_item(agent: Agent, item: String) -> bool:
	if _world == null:
		return false

	if _is_resource_item(item):
		return _has_required_tool(agent, item)

	var recipe: Recipe = _find_recipe_for_output(item, _recipes, _world)
	if recipe == null:
		return false
	if recipe.station == "workbench" and not agent.has_available_item("Workbench"):
		return false
	if recipe.station != "hand" and recipe.station != "workbench" and not _world.has_workshop_type(recipe.station):
		return false
	return true

func _is_resource_item(item: String) -> bool:
	return item in ["Berries", "Logs", "Ore", "Stone"]

func _has_required_tool(agent: Agent, item: String) -> bool:
	match item:
		"Logs":
			return agent.has_tool("Axe") or agent.has_tool("WoodenAxe")
		"Ore":
			return agent.has_tool("Pickaxe") or agent.has_tool("WoodenPickaxe")
		_:
			return true

func _find_recipe_for_output(output_item: String, recipes: Dictionary, world: World) -> Recipe:
	for recipe_id in recipes:
		var recipe: Recipe = recipes[recipe_id]
		if recipe.outputs.has(output_item) and _is_recipe_allowed(recipe, world):
			return recipe
	return null

func _is_recipe_allowed(recipe: Recipe, world: World) -> bool:
	if recipe == null:
		return false
	if recipe.tier != "advanced":
		return _is_station_available(recipe, world)
	if recipe.station == "hand":
		return false
	return _is_station_available(recipe, world)

# Helper function to determine if agent should claim resources
func _should_claim_resources(agent: Agent, world: World, tuning: Dictionary) -> bool:
	var search_radius: int = int(tuning.get("claim_search_radius", 8))
	var valuable_found = false

	for node in world.resource_nodes:
		var dist = absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
		if dist <= search_radius and not world.is_claimed(node.pos_x, node.pos_y):
			if node.type == "berry" and agent.get_hunger() < 60:
				return true
			elif node.type == "tree" and not agent.has_tool("Axe"):
				return true
			elif node.type == "ore" and not agent.has_tool("Pickaxe"):
				return true
			valuable_found = true

	return valuable_found

# Helper function to determine if agent should build a workshop
func _should_build_workshop(agent: Agent, world: World, tuning: Dictionary) -> bool:
	var nearby_workshops = 0
	var search_radius: int = int(tuning.get("workshop_nearby_radius", 15))

	for workshop in world.workshops:
		var dist = absi(workshop.pos_x - agent.pos_x) + absi(workshop.pos_y - agent.pos_y)
		if dist <= search_radius and workshop.is_ready():
			nearby_workshops += 1

	if nearby_workshops < 2:
		var planks_needed: int = int(tuning.get("workshop_build_planks", 10))
		if agent.has_available_item("Planks", planks_needed):
			return true

	return false
