## EconomyPlanner - handles contracts, resources, and progression goals
class_name EconomyPlanner
extends IAgentPlanner

func get_priority() -> int:
	return 60

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	return add_primary_goal(agent, context.world, context.market, context.contracts_system, context.tuning, context.recipes, context.state)

func add_primary_goal(agent: Agent, world: World, market: Market,
		contracts_system: ContractsSystem, tuning: Dictionary,
		recipes: Dictionary, state: SimState = null) -> bool:
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
		var best_contract = contracts_system.find_best_contract(agent, market, tuning)
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
