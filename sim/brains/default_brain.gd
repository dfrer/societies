## DefaultBrain - Utility AI Implementation
## Evaluates possible actions and selects the one with the highest utility score
class_name DefaultBrain
extends IAgentBrain

# Weight mappings
const WEIGHT_SURVIVAL = 10.0
const WEIGHT_HEALTH = 5.0
const WEIGHT_MONEY = 2.0
const WEIGHT_COMFORT = 1.0
const WEIGHT_SOCIAL = 1.0

# Inertia (preference to keep doing the same thing)
const INERTIA_BONUS = 0.2

var _survival_planner := SurvivalPlanner.new()
var _needs_planner := NeedsPlanner.new()
var _homestead_planner := HomesteadPlanner.new()
var _economy_planner := EconomyPlanner.new()
var _governance_planner := GovernancePlanner.new()
var _planners: Array[IAgentPlanner] = []

func _init() -> void:
	_planners = [
		CommitmentPlanner.new(),
		_needs_planner,
		_homestead_planner,
		_economy_planner,
		_governance_planner
	]
	_planners.sort_custom(func(a, b): return a.get_priority() > b.get_priority())

func decide_action(agent: Agent, world: World, market: Market,
				   contracts_system: ContractsSystem, tuning: Dictionary,
				   recipes: Dictionary = {}, state: SimState = null) -> Dictionary:
	var planner_context := PlannerContext.create(world, market, contracts_system, tuning, recipes, state)

	# 0. Interrupts (Panic checks that override everything)
	var interrupt = _needs_planner.get_interrupt_action(agent, tuning)
	if not interrupt.is_empty():
		_set_intent(state, agent, "INTERRUPT", {"action_type": interrupt.get("type", "")})
		_clear_activity(state, agent)
		return interrupt

	var activity_action := _action_from_current_activity(agent, world, tuning, state)
	if activity_action.has("type"):
		return activity_action
	if state != null:
		var opportunistic := _claim_opportunity_activity(agent, world, tuning, state)
		if opportunistic.has("type"):
			return opportunistic

	var iterations: int = 0
	while iterations < 5:
		if agent.goal_stack.is_empty():
			_generate_high_level_goals(agent, planner_context)

		if agent.goal_stack.is_empty():
			var fallback := _fallback_action(agent, world)
			_set_intent(state, agent, "IDLE", {"action_type": fallback.get("type", "")})
			return fallback # Idle/Explore

		var current_goal: Dictionary = agent.goal_stack.back()

		# Check completion
		if _is_goal_complete(agent, current_goal, world):
			agent.goal_stack.pop_back()
			iterations += 1  # Increment to prevent infinite loop on instant completion
			continue

		var intent := _intent_from_goal(agent, current_goal, world, market, tuning)
		_set_intent(state, agent, intent.get("type", "NONE"), intent.get("data", {}))
		var activity := _commit_activity_for_intent(agent, intent, state)
		if not activity.is_empty():
			var committed_action := _action_from_activity(agent, activity, world, tuning, state)
			if committed_action.has("type"):
				return committed_action

		# Process Goal
		var result = _process_goal(agent, current_goal, world, market, contracts_system, tuning, recipes, state)

		if result is Dictionary:
			if result.get("is_goal", false):
				# New sub-goal
				agent.goal_stack.push_back(result)
				iterations += 1
			elif result.has("type"):
				# Action found!
				return result
			else:
				# Failure/Wait
				agent.goal_stack.pop_back() # Goal failed or invalid?

		iterations += 1

	var idle_action := Actions.idle()
	_set_intent(state, agent, "IDLE", {"action_type": idle_action.get("type", "")})
	return idle_action

# ==============================================================================
# ACTION GENERATION
# ==============================================================================

# ==============================================================================
# GOAL ORIENTED PLANNING (GOAP)
# ==============================================================================

func _fallback_action(agent: Agent, world: World) -> Dictionary:
	# Just explore or idle
	var risk = agent.personality.get("risk_tolerance", 0.5)
	if risk > 0.3:
		return Actions.explore(agent.exploration_direction)
	return Actions.rest()

func _generate_high_level_goals(agent: Agent, context: PlannerContext) -> void:
	for planner in _planners:
		if planner.maybe_add_goal(agent, context):
			return
	_economy_planner.add_progression_goal(agent, context.world, context.tuning, context.recipes, context.state)

# Helper function to determine if agent should claim resources
func _is_goal_complete(agent: Agent, goal: Dictionary, world: World) -> bool:
	match goal.type:
		"CLAIM_RESOURCES":
			# Complete when we've claimed a valuable resource or no tokens left
			return not agent.has_claim_tokens()
		"ESTABLISH_HOMESTEAD":
			if not agent.has_home():
				return false
			var radius: int = int(goal.get("radius", 3))
			if not agent.has_claim_tokens():
				return true
			for dx in range(-radius, radius + 1):
				for dy in range(-radius, radius + 1):
					var x := agent.home_pos.x + dx
					var y := agent.home_pos.y + dy
					if world.is_valid(x, y) and not world.is_claimed(x, y):
						return false
			return true
		"BUILD_PERSONAL_STOCKPILE":
			return agent.has_personal_stockpile()
		"BUILD_PERSONAL_SHELTER":
			return agent.has_personal_shelter()
		"DEPOSIT_SURPLUS":
			if agent.personal_stockpile_id < 0:
				return true
			return _get_total_inventory(agent) <= 20
		"BUILD_STRATEGIC_WORKSHOP":
			# Complete when we have sufficient workshops nearby
			var nearby_workshops = 0
			var search_radius = 15
			for workshop in world.workshops:
				var dist = absi(workshop.pos_x - agent.pos_x) + absi(workshop.pos_y - agent.pos_y)
				if dist <= search_radius and workshop.is_ready():
					nearby_workshops += 1
			return nearby_workshops >= 2
		"OBTAIN_ITEM":
			return agent.has_item(goal.item, goal.get("qty", 1))
		"MAINTAIN_FOOD_BUFFER":
			var target: int = int(goal.get("target_qty", 5))
			var have: int = agent.get_item_count("Berries") + agent.get_item_count("CookedMeal")
			return have >= target
		"EFFICIENT_REST":
			return agent.get_stamina() >= 80.0
		"EAT_FOOD":
			# EAT_FOOD goal is complete after one eat action (hunger should increase)
			# We mark it complete if hunger is back above 50 or we don't have the food anymore
			return agent.get_hunger() >= 50.0 or not agent.has_available_item(goal.item)
		"FULFILL_CONTRACT":
			return agent.active_contract_id == -1 # Completed/Failed
		"GO_TO":
			return agent.is_at(goal.x, goal.y)
		"ACCEPT_CONTRACT":
			return agent.has_active_contract()
		"EXPAND_FACTION":
			if goal.has("target_x"):
				var owner = world.get_claim_owner(goal.target_x, goal.target_y)
				if agent.faction_id > 0:
					var faction_owner = World.faction_owner_id(agent.faction_id)
					return owner == faction_owner
			return false
		"BUILD_ROAD":
			if goal.has("target_x"):
				# Just check if we are there and it's built or being built
				return world.has_road(agent.pos_x, agent.pos_y)
			return false
		"REST":
			return agent.get_stamina() >= 90.0
	return false

func _process_goal(agent: Agent, goal: Dictionary, world: World, market: Market,
				   contracts_system: ContractsSystem, tuning: Dictionary,
				   recipes: Dictionary, state: SimState = null) -> Dictionary:
	match goal.type:
		"CLAIM_RESOURCES":
			return _plan_claim_resources(agent, goal, world, tuning)
		"ESTABLISH_HOMESTEAD":
			return _plan_establish_homestead(agent, goal, world, tuning)
		"BUILD_PERSONAL_STOCKPILE":
			return _plan_build_personal_stockpile(agent, goal, world, tuning, state)
		"BUILD_PERSONAL_SHELTER":
			return _plan_build_personal_shelter(agent, goal, world, tuning, state)
		"DEPOSIT_SURPLUS":
			return _plan_deposit_surplus(agent, goal, world, tuning, state)
		"BUILD_STRATEGIC_WORKSHOP":
			return _plan_build_strategic_workshop(agent, goal, world, tuning)
		"OBTAIN_ITEM":
			return _plan_obtain_item(agent, goal, world, market, recipes, tuning, state)
		"EAT_FOOD":
			# Directly return eat action for the food item
			if goal.item == "CookedMeal":
				return Actions.eat_meal()
			else:
				return Actions.eat()
		"MAINTAIN_FOOD_BUFFER":
			var target: int = int(goal.get("target_qty", 5))
			var have: int = agent.get_item_count("Berries") + agent.get_item_count("CookedMeal")
			var need: int = target - have
			if need > 0:
				return {"type": "OBTAIN_ITEM", "item": "Berries", "qty": need, "is_goal": true}
			return {}
		"EFFICIENT_REST":
			var shelter_id: int = int(goal.get("shelter_id", -1))
			if shelter_id < 0:
				return Actions.rest()
			if state == null:
				return Actions.rest()
			var shelter = state.structures.get_structure(shelter_id)
			if shelter == null:
				return Actions.rest()
			if agent.is_at(shelter.pos_x, shelter.pos_y):
				return Actions.sleep_rest()
			return Actions.move_to_position(shelter.pos_x, shelter.pos_y)
		"GO_TO":
			# Simple Move Action
			if agent.is_at(goal.x, goal.y): return {} # Done (should be caught by is_complete)
			return Actions.move_to_position(goal.x, goal.y)
		"FULFILL_CONTRACT":
			return _plan_fulfill_contract(agent, goal, contracts_system, market)
		"ACCEPT_CONTRACT":
			var contract_id = goal.contract_id
			# Just do it
			return Actions.accept_contract(contract_id)
		"EXPAND_FACTION":
			return _plan_expand_faction(agent, goal, world, tuning, state)
		"BUILD_ROAD":
			return _plan_build_road(agent, goal, world, tuning)
		"REST":
			return Actions.rest()

	return {}

func _intent_from_goal(agent: Agent, goal: Dictionary, world: World, market: Market, tuning: Dictionary) -> Dictionary:
	match goal.type:
		"OBTAIN_ITEM":
			var node_type: String = ""
			var item_name: String = goal.get("item", "")
			if item_name == "Berries":
				node_type = "berry"
			elif item_name == "Logs":
				node_type = "tree"
			elif item_name == "Ore":
				node_type = "ore"
			elif item_name == "Stone":
				node_type = "stone"
			if node_type != "":
				return {"type": "GATHER_RESOURCE", "data": {"item": item_name, "node_type": node_type}}
			return {"type": "OBTAIN_ITEM", "data": {"item": item_name}}
		"MAINTAIN_FOOD_BUFFER":
			return {"type": "GATHER_RESOURCE", "data": {"item": "Berries", "node_type": "berry"}}
		"EFFICIENT_REST":
			return {"type": "REST", "data": {"shelter_id": goal.get("shelter_id", -1)}}
		"EAT_FOOD":
			return {"type": "EAT_FOOD", "data": {"item": goal.get("item", "")}}
		"FULFILL_CONTRACT":
			return {"type": "FULFILL_CONTRACT", "data": {"contract_id": goal.get("contract_id", -1)}}
		"BUILD_STRATEGIC_WORKSHOP":
			return {"type": "BUILD_WORKSHOP", "data": {}}
		"CLAIM_RESOURCES":
			return {"type": "CLAIM_RESOURCES", "data": {}}
		"ESTABLISH_HOMESTEAD":
			return {"type": "ESTABLISH_HOMESTEAD", "data": goal.duplicate(true)}
		"BUILD_PERSONAL_STOCKPILE":
			return {"type": "BUILD_PERSONAL_STOCKPILE", "data": goal.duplicate(true)}
		"BUILD_PERSONAL_SHELTER":
			return {"type": "BUILD_PERSONAL_SHELTER", "data": goal.duplicate(true)}
		"DEPOSIT_SURPLUS":
			return {"type": "DEPOSIT_SURPLUS", "data": {}}
		"EXPAND_FACTION":
			return {"type": "EXPAND_FACTION", "data": goal.duplicate(true)}
		"BUILD_ROAD":
			return {"type": "BUILD_ROAD", "data": goal.duplicate(true)}
		"REST":
			return {"type": "REST", "data": {}}
		"ACCEPT_CONTRACT":
			return {"type": "ACCEPT_CONTRACT", "data": {"contract_id": goal.get("contract_id", -1)}}
		"GO_TO":
			return {"type": "GO_TO", "data": {"x": goal.get("x", agent.pos_x), "y": goal.get("y", agent.pos_y)}}
	return {"type": "IDLE", "data": {}}

func _set_intent(state: SimState, agent: Agent, intent_type: String, data: Dictionary) -> void:
	if state == null:
		return
	if agent.current_intent_id >= 0:
		var existing := state.get_intent(agent.current_intent_id)
		if existing.get("type", "") == intent_type and existing.get("status", "") == "active" \
			and existing.get("data", {}) == data:
			return
		state.resolve_intent(agent.current_intent_id, "superseded", state.tick)
	var intent_id: int = state.create_intent(agent.id, intent_type, data, state.tick)
	agent.current_intent_id = intent_id

func _clear_activity(state: SimState, agent: Agent) -> void:
	if state == null:
		return
	if agent.current_activity_id >= 0:
		state.job_board.release_activity(agent.current_activity_id, state.tick)
	agent.current_activity_id = -1

func _commit_activity_for_intent(agent: Agent, intent: Dictionary, state: SimState) -> Dictionary:
	if state == null:
		return {}
	if intent.get("type", "") == "GATHER_RESOURCE":
		var node_type: String = intent.get("data", {}).get("node_type", "")
		var candidates := state.job_board.get_available_activities(JobBoard.ACTIVITY_GATHER_NODE)
		for activity in candidates:
			var data: Dictionary = activity.get("data", {})
			if data.get("node_type", "") != node_type:
				continue
			if state.job_board.claim_activity(activity.get("activity_id", -1), agent.id, state.tick):
				agent.current_activity_id = int(activity.get("activity_id", -1))
				return activity
	elif intent.get("type", "") == "ACCEPT_CONTRACT":
		var contract_id := int(intent.get("data", {}).get("contract_id", -1))
		var candidates := state.job_board.get_available_activities(JobBoard.ACTIVITY_ACCEPT_CONTRACT)
		for activity in candidates:
			var data: Dictionary = activity.get("data", {})
			if int(data.get("contract_id", -1)) != contract_id:
				continue
			if state.job_board.claim_activity(activity.get("activity_id", -1), agent.id, state.tick):
				agent.current_activity_id = int(activity.get("activity_id", -1))
				return activity
	return {}

func _claim_opportunity_activity(agent: Agent, world: World, tuning: Dictionary, state: SimState) -> Dictionary:
	var contract_threshold: float = float(tuning.get("contract_prefer_profit_threshold", 8))
	var best_contract := state.contracts_system.find_best_contract(agent, state.market, tuning)
	if best_contract != null:
		var score := state.contracts_system.score_contract(best_contract, agent, state.market, tuning)
		if score >= contract_threshold:
			var candidates := state.job_board.get_available_activities(JobBoard.ACTIVITY_ACCEPT_CONTRACT)
			for activity in candidates:
				var data: Dictionary = activity.get("data", {})
				if int(data.get("contract_id", -1)) != best_contract.id:
					continue
				if state.job_board.claim_activity(activity.get("activity_id", -1), agent.id, state.tick):
					agent.current_activity_id = int(activity.get("activity_id", -1))
					_set_intent(state, agent, "JOB_BOARD_TASK", {"activity_type": JobBoard.ACTIVITY_ACCEPT_CONTRACT})
					return _action_from_activity(agent, activity, world, tuning, state)
	var priority := [
		JobBoard.ACTIVITY_DELIVER_TO_PROJECT,
		JobBoard.ACTIVITY_BUILD_SITE,
		JobBoard.ACTIVITY_CRAFT_AT_STATION,
		JobBoard.ACTIVITY_HAUL,
		JobBoard.ACTIVITY_FARM_TASK
	]
	for activity_type in priority:
		var candidates := state.job_board.get_available_activities(activity_type)
		for activity in candidates:
			if state.job_board.claim_activity(activity.get("activity_id", -1), agent.id, state.tick):
				agent.current_activity_id = int(activity.get("activity_id", -1))
				_set_intent(state, agent, "JOB_BOARD_TASK", {"activity_type": activity_type})
				return _action_from_activity(agent, activity, world, tuning, state)
	return {}

func _action_from_current_activity(agent: Agent, world: World, tuning: Dictionary, state: SimState) -> Dictionary:
	if state == null or agent.current_activity_id < 0:
		return {}
	var activity := state.job_board.get_activity(agent.current_activity_id)
	if activity.is_empty():
		agent.current_activity_id = -1
		return {}
	var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
	if status != JobBoard.STATUS_CLAIMED:
		agent.current_activity_id = -1
		return {}
	if int(activity.get("worker_id", -1)) != agent.id:
		agent.current_activity_id = -1
		return {}
	return _action_from_activity(agent, activity, world, tuning, state)

func _action_from_activity(agent: Agent, activity: Dictionary, world: World, tuning: Dictionary, state: SimState) -> Dictionary:
	var activity_type: String = activity.get("type", "")
	match activity_type:
		JobBoard.ACTIVITY_GATHER_NODE:
			var data: Dictionary = activity.get("data", {})
			var node_id: int = int(data.get("node_id", -1))
			var node := world.get_node_by_id(node_id)
			if node == null or not node.has_stock(1):
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			if agent.is_at(node.pos_x, node.pos_y):
				return Actions.gather_node(node.id)
			return Actions.move_to_node(node.id)
		JobBoard.ACTIVITY_ACCEPT_CONTRACT:
			var contract_id := int(activity.get("data", {}).get("contract_id", -1))
			if contract_id < 0:
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			return Actions.accept_contract(contract_id)
		JobBoard.ACTIVITY_DELIVER_TO_PROJECT:
			var data: Dictionary = activity.get("data", {})
			var project_id: int = int(data.get("project_id", -1))
			var item_type: String = data.get("item_type", "")
			var qty: int = int(data.get("quantity", 1))
			var project := state.communal_projects.get_project(project_id)
			if project == null or project.status != CommunalProject.STATUS_COLLECTING:
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			if agent.has_available_item(item_type, qty):
				if agent.is_at(project.pos_x, project.pos_y):
					return Actions.contribute_to_project(project_id, item_type, qty)
				return Actions.move_to_position(project.pos_x, project.pos_y)
			var stockpile := state.structures.find_communal_stockpile_with_item(item_type, 1)
			if stockpile == null:
				state.job_board.release_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			if agent.is_at(stockpile.pos_x, stockpile.pos_y):
				return Actions.withdraw_stockpile(stockpile.id, item_type, qty)
			return Actions.move_to_position(stockpile.pos_x, stockpile.pos_y)
		JobBoard.ACTIVITY_BUILD_SITE:
			var project_id: int = int(activity.get("data", {}).get("project_id", -1))
			if project_id < 0:
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			return Actions.build_site(project_id)
		JobBoard.ACTIVITY_HAUL:
			var data: Dictionary = activity.get("data", {})
			var source_id: int = int(data.get("source_id", -1))
			var destination_id: int = int(data.get("destination_id", -1))
			var item_type: String = data.get("item_type", "")
			var qty: int = int(data.get("quantity", 1))
			var destination_type: String = data.get("destination_type", "stockpile")
			var source := state.structures.get_structure(source_id)
			if source == null or item_type == "" or qty <= 0:
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			if agent.has_available_item(item_type, qty):
				if destination_type == "project":
					var project := state.communal_projects.get_project(destination_id)
					if project == null:
						state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
						agent.current_activity_id = -1
						return {}
					if agent.is_at(project.pos_x, project.pos_y):
						return Actions.contribute_to_project(project.id, item_type, qty)
					return Actions.move_to_position(project.pos_x, project.pos_y)
				var destination := state.structures.get_structure(destination_id)
				if destination == null:
					state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
					agent.current_activity_id = -1
					return {}
				if agent.is_at(destination.pos_x, destination.pos_y):
					return Actions.deposit_stockpile(destination.id, item_type, qty)
				return Actions.move_to_position(destination.pos_x, destination.pos_y)
			if agent.is_at(source.pos_x, source.pos_y):
				return Actions.withdraw_stockpile(source.id, item_type, qty, true)
			return Actions.move_to_position(source.pos_x, source.pos_y)
		JobBoard.ACTIVITY_CRAFT_AT_STATION:
			var data: Dictionary = activity.get("data", {})
			var station_id: int = int(data.get("station_id", -1))
			var recipe_id: String = data.get("recipe_id", "")
			var workshop := state.world.get_workshop_by_id(station_id)
			var recipe: Recipe = state.recipes.get(recipe_id)
			if workshop == null or recipe == null or not workshop.is_ready():
				_release_craft_reservations(data, state)
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			if not _station_supports_recipe(workshop, recipe):
				_release_craft_reservations(data, state)
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			var stockpile_id: int = int(data.get("stockpile_id", -1))
			var stockpile := state.structures.get_structure(stockpile_id)
			for item in recipe.inputs:
				var required: int = recipe.inputs[item]
				var available: int = agent.get_available_item(item)
				if available < required:
					if stockpile == null:
						state.job_board.release_activity(activity.get("activity_id", -1), state.tick)
						agent.current_activity_id = -1
						return {}
					if agent.is_at(stockpile.pos_x, stockpile.pos_y):
						return Actions.withdraw_stockpile(stockpile.id, item, required - available, true)
					return Actions.move_to_position(stockpile.pos_x, stockpile.pos_y)
			_release_craft_reservations(data, state)
			if agent.is_at(workshop.pos_x, workshop.pos_y):
				return Actions.queue_craft(recipe.id, workshop.id)
			return Actions.move_to_position(workshop.pos_x, workshop.pos_y)
		JobBoard.ACTIVITY_FARM_TASK:
			var data: Dictionary = activity.get("data", {})
			var plot_id: int = int(data.get("plot_id", -1))
			var task_type: String = data.get("task_type", "")
			var crop_type: String = data.get("crop_type", "Berries")
			var plot := state.world.get_farm_plot(plot_id)
			if plot.is_empty():
				state.job_board.cancel_activity(activity.get("activity_id", -1), state.tick)
				agent.current_activity_id = -1
				return {}
			var plot_x: int = int(plot.get("x", agent.pos_x))
			var plot_y: int = int(plot.get("y", agent.pos_y))
			match task_type:
				"TILL":
					if agent.is_at(plot_x, plot_y):
						return Actions.till_farm(plot_id)
					return Actions.move_to_position(plot_x, plot_y)
				"PLANT":
					if agent.is_at(plot_x, plot_y):
						return Actions.plant_farm(plot_id, crop_type)
					return Actions.move_to_position(plot_x, plot_y)
				"HARVEST":
					if agent.is_at(plot_x, plot_y):
						return Actions.harvest_farm(plot_id)
					return Actions.move_to_position(plot_x, plot_y)
				"DELIVER":
					var stockpile_id: int = int(data.get("stockpile_id", -1))
					var stockpile := state.structures.get_structure(stockpile_id)
					if stockpile == null:
						state.job_board.release_activity(activity.get("activity_id", -1), state.tick)
						agent.current_activity_id = -1
						return {}
					if agent.has_available_item(crop_type, 1):
						if agent.is_at(stockpile.pos_x, stockpile.pos_y):
							return Actions.deliver_farm(plot_id, stockpile_id, crop_type)
						return Actions.move_to_position(stockpile.pos_x, stockpile.pos_y)
					if agent.is_at(plot_x, plot_y):
						return Actions.deliver_farm(plot_id, stockpile_id, crop_type)
					return Actions.move_to_position(plot_x, plot_y)
			state.job_board.release_activity(activity.get("activity_id", -1), state.tick)
			agent.current_activity_id = -1
			return {}
	return {}
	
# --------------------
# PLANNER HELPERS
# --------------------

func _manhattan_distance(agent: Agent, node: ResourceNode) -> int:
	return absi(agent.pos_x - node.pos_x) + absi(agent.pos_y - node.pos_y)

func _plan_obtain_item(agent: Agent, goal: Dictionary, world: World, market: Market, recipes: Dictionary, tuning: Dictionary, state: SimState = null) -> Dictionary:
	var item = goal.item

	# 1. Check if we can simply gather it
	var node_type: String = ""
	if item == "Berries": node_type = "berry"
	elif item == "Logs": node_type = "tree"
	elif item == "Ore": node_type = "ore"
	
	if node_type != "":
		# It's a resource!
		# Pin a node to this goal to avoid oscillation between nearby nodes.
		var node_switch_threshold: int = int(tuning.get("node_switch_threshold", 3))
		var node_blocked_threshold: int = int(tuning.get("node_blocked_ticks_threshold", 3))
		var node_id: int = int(goal.get("node_id", -1))
		var node: ResourceNode = null
		if node_id >= 0:
			node = world.get_node_by_id(node_id)
			var should_abandon := false
			if node == null or node.type != node_type:
				should_abandon = true
			elif not node.has_stock(1) or not _can_harvest_resource_node(agent, world, state, node):
				var blocked_ticks: int = int(goal.get("node_blocked_ticks", 0)) + 1
				goal["node_blocked_ticks"] = blocked_ticks
				if blocked_ticks >= node_blocked_threshold:
					var alternative := _find_nearest_accessible_node_of_type(agent, world, state, node_type)
					if alternative != null:
						var current_dist: int = _manhattan_distance(agent, node)
						var alt_dist: int = _manhattan_distance(agent, alternative)
						if alt_dist < current_dist - node_switch_threshold:
							should_abandon = true
			else:
				goal.erase("node_blocked_ticks")
			if should_abandon:
				goal.erase("node_id")
				goal.erase("node_blocked_ticks")
				node = null
		if node == null:
			node = _find_nearest_accessible_node_of_type(agent, world, state, node_type)
			if node:
				goal["node_id"] = node.id
				goal["node_commitment_ticks"] = 0
				goal.erase("node_blocked_ticks")
		if node:
			goal["node_id"] = node.id
			goal["node_commitment_ticks"] = int(goal.get("node_commitment_ticks", 0)) + 1
			if agent.is_at(node.pos_x, node.pos_y):
				return Actions.gather_node(node.id)
			else:
				return Actions.move_to_node(node.id) # Or push GO_TO goal, but Action is fine
				
	# 2. Check if we can craft it
	var recipe = _find_recipe_for(item, recipes, world, agent, state)
	if recipe:
		# Check inputs
		for input_item in recipe.inputs:
			if not agent.has_available_item(input_item, recipe.inputs[input_item]):
				# Missing input! Push sub-goal
				return {"type": "OBTAIN_ITEM", "item": input_item, "qty": recipe.inputs[input_item], "is_goal": true}
		
		# Have inputs!
		if recipe.station == "hand":
			return Actions.queue_craft(recipe.id)
		elif recipe.station == "workbench":
			if not agent.has_available_item("Workbench"):
				return {"type": "OBTAIN_ITEM", "item": "Workbench", "qty": 1, "is_goal": true}
			return Actions.queue_craft(recipe.id)
		else:
			# Workshop
			var workshop = world.find_closest_workshop(agent.pos_x, agent.pos_y, recipe.station)
			if workshop:
				if agent.is_at_workshop(workshop):
					return Actions.queue_craft(recipe.id, workshop.id)
				else:
					return Actions.move_to_workshop(workshop.id)
			else:
				# No workshop exists! Deadlock? 
				# In Phase 3 we ensured Public Workshop or Handheld planks.
				pass
	
	# 3. Check if we can BUY it
	# If we have money > Ref Price
	var price = market.get_ref_price(item)
	if agent.get_available_money() >= price:
		# Place buy order
		if not market.has_active_order(agent.id, item, "buy"):
			return Actions.place_buy_order(item)
		else:
			# Already have purchase order, maybe wait?
			# Or move to market?
			if not agent.is_at_market(tuning): # Assume agent.gd logic
				# Tuning needed for is_at_market? agent.is_at_market(tuning)
				# But check implementation.
				pass
			# If order exists but not filled, we just wait.
			# Return Idle?
			pass
				
	return {}

func _plan_fulfill_contract(agent: Agent, goal: Dictionary, contracts_system: ContractsSystem, market: Market) -> Dictionary:
	var contract = contracts_system.get_contract(goal.contract_id)
	if not contract: return {}
	
	if agent.has_item(contract.item, contract.qty):
		# Deliver
		if agent.is_at(contract.delivery_pos_x, contract.delivery_pos_y):
			return Actions.deliver_contract(contract.id)
		else:
			return Actions.move_to_delivery(contract.id)
	else:
		# Obtain item
		return {"type": "OBTAIN_ITEM", "item": contract.item, "qty": contract.qty, "is_goal": true}
		
func _is_recipe_allowed(recipe: Recipe, world: World) -> bool:
	if recipe == null:
		return false
	if recipe.tier != "advanced":
		return _is_recipe_station_available(recipe, world)
	if recipe.station == "hand":
		return false
	return _is_recipe_station_available(recipe, world)

func _is_recipe_station_available(recipe: Recipe, world: World) -> bool:
	if recipe.station == "hand" or recipe.station == "workbench":
		return true
	return world.has_workshop_type(recipe.station)

func _get_sorted_recipe_ids(recipes: Dictionary, state: SimState) -> Array:
	if state != null and not state.recipe_sorted_ids.is_empty():
		return state.recipe_sorted_ids
	var sorted_ids: Array = recipes.keys()
	sorted_ids.sort()
	if state != null and state.recipe_sorted_ids.is_empty():
		state.recipe_sorted_ids = sorted_ids
	return sorted_ids

func _find_recipe_for(output_item: String, recipes: Dictionary, world: World, agent: Agent, state: SimState = null) -> Recipe:
	# Prefer handheld recipes first (like planks_hand)
	# Sort keys for deterministic iteration order
	var sorted_ids := _get_sorted_recipe_ids(recipes, state)
	for id in sorted_ids:
		var r: Recipe = recipes[id]
		if r.outputs.has(output_item) and r.station == "hand" and _is_recipe_allowed(r, world):
			return r

	for id in sorted_ids:
		var r: Recipe = recipes[id]
		if r.outputs.has(output_item) and r.station == "workbench" and _is_recipe_allowed(r, world):
			return r
			
	for id in sorted_ids:
		var r: Recipe = recipes[id]
		if r.outputs.has(output_item) and _is_recipe_allowed(r, world):
			return r 
	return null

func _station_supports_recipe(workshop: Workshop, recipe: Recipe) -> bool:
	if workshop == null or recipe == null:
		return false
	if recipe.station == "" or recipe.station == "workshop":
		return true
	return workshop.workshop_type == recipe.station

func _release_craft_reservations(data: Dictionary, state: SimState) -> void:
	var stockpile_id: int = int(data.get("stockpile_id", -1))
	var reserved_inputs: Dictionary = data.get("reserved_inputs", {})
	if stockpile_id < 0 or reserved_inputs.is_empty():
		return
	var stockpile := state.structures.get_structure(stockpile_id)
	if stockpile == null:
		return
	for item in reserved_inputs:
		stockpile.release_reserved_item(item, int(reserved_inputs[item]))

func _score_action(action, agent: Agent, world: World, market: Market, 
				   contracts_system: ContractsSystem, tuning: Dictionary) -> float:
	var score = 0.0
	var action_type = action.get("type", "")
	
	# Pre-calculate common state
	var hunger = agent.needs.get("hunger", 0.0)
	var stamina = agent.needs.get("stamina", 0.0)
	var money = agent.get_available_money()

	# Check active contract needs
	var needed_contract_item = ""
	if agent.has_active_contract():
		var contract = contracts_system.get_contract(agent.active_contract_id)
		if contract and contract.status == Contract.STATUS_ACCEPTED:
			if agent.get_available_item(contract.item) < contract.qty:
				needed_contract_item = contract.item
	
	# Evaluators
	match action_type:
		Actions.TYPE_IDLE:
			score = 0.1 # Baseline
			
		Actions.TYPE_REST, Actions.TYPE_SLEEP:
			score = _eval_rest(agent, action_type)
			
		Actions.TYPE_EAT, Actions.TYPE_EAT_MEAL:
			score = _eval_eat(agent, action)
		
		Actions.TYPE_GATHER_NODE:
			score = _eval_gather(agent, action, world, tuning, needed_contract_item)
			
		Actions.TYPE_MOVE_TO_NODE:
			score = _eval_move_to_node(agent, action, world, tuning, needed_contract_item)
			
		Actions.TYPE_MOVE_TO_MARKET:
			score = _eval_move_to_market(agent, action)
			
		Actions.TYPE_PLACE_SELL_ORDER:
			score = _eval_sell(agent, action, market, tuning)
			
		Actions.TYPE_PLACE_BUY_ORDER:
			score = _eval_buy(agent, action, market, tuning, needed_contract_item)
			
		Actions.TYPE_QUEUE_CRAFT:
			score = _eval_craft(agent, action, world, market, tuning)
			
		Actions.TYPE_MOVE_TO_WORKSHOP:
			score = _eval_move_to_workshop(agent, action, world)
			
		Actions.TYPE_EXPLORE:
			score = _eval_explore(agent, world)

		Actions.TYPE_ACCEPT_CONTRACT:
			score = _eval_accept_contract(agent, action, contracts_system, market, tuning)
			
		Actions.TYPE_DELIVER_CONTRACT:
			score = _eval_deliver_contract(agent, action, contracts_system)
			
		Actions.TYPE_MOVE_TO_DELIVERY:
			score = _eval_move_to_delivery(agent, action, contracts_system)

		Actions.TYPE_MOVE_TO_POSITION:
			score = _eval_move_to_position(agent, action)
			
		Actions.TYPE_CLAIM_TILE:
			score = _eval_claim_tile(agent, action, world)
			
		Actions.TYPE_BUILD_WORKSHOP:
			score = _eval_build_workshop(agent, action)
			
		Actions.TYPE_VOTE:
			score = _eval_vote(agent, action)
			
		Actions.TYPE_CONTRIBUTE_TO_PROJECT:
			score = _eval_contribute_project(agent, action)
		Actions.TYPE_BUILD_ROAD:
			score = _eval_build_road(agent, action, world)
			
	return score

# ==============================================================================
# EVALUATORS
# ==============================================================================

func _eval_rest(agent: Agent, type: String) -> float:
	var stamina = agent.get_stamina()
	# Curve: Rapidly increases as stamina drops below 20. Zero if > 90.
	# 100 - stamina
	var deficit = 100.0 - stamina
	
	var utility = 0.0
	
	# Critical Exhaustion (Survival)
	if stamina < 10.0:
		utility += 100.0 # Override everything
		
	# Regular Fatigue
	# Sigmoid curve around 30 stamina
	utility += _curve_sigmoid(deficit, 70.0, 0.1) * WEIGHT_HEALTH
	
	# Laziness Bonus
	var laziness = agent.personality.get("laziness", 0.5)
	utility *= (1.0 + laziness)
	
	if type == Actions.TYPE_SLEEP:
		utility *= 1.2 # Sleep is better than rest
	
	return utility

func _eval_eat(agent: Agent, action: Dictionary) -> float:
	var hunger = agent.get_hunger()
	
	# Critical Starvation (Survival)
	if hunger < 15.0:
		return 200.0 # Panic
		
	# Regular Hunger
	# Curve: Linear increase as hunger drops
	var deficit = 100.0 - hunger
	var utility = _curve_logit(deficit / 100.0) * WEIGHT_SURVIVAL
	
	var is_item_better = (action.get("type") == Actions.TYPE_EAT_MEAL)
	if is_item_better:
		utility *= 1.5
		
	return utility
	
func _eval_gather(agent: Agent, action: Dictionary, world: World, tuning: Dictionary, contract_item: String = "") -> float:
	var node = world.get_node_by_id(action.get("node_id"))
	if node == null: return -1.0
	
	# Value based on what we get
	var utility = 0.0
	var greed = agent.personality.get("greed", 0.5)

	var node_yields = ""
	match node.type:
		"berry": node_yields = "Berries"
		"tree": node_yields = "Logs"
		"ore": node_yields = "Ore" # Approximate
	
	# Contract Bonus
	if contract_item != "" and node_yields == contract_item:
		utility += 50.0 # High priority to fulfilling contract

	match node.type:
		"berry":
			# Food Value
			var hunger = agent.get_hunger()
			# Was: (100 - hunger) * 0.1 => Max 10.0
			# New: Scale to be competitive with Buying (50-100)
			
			if hunger < 50.0:
				var deficit = 100.0 - hunger
				utility += _curve_logit(deficit / 100.0) * WEIGHT_SURVIVAL # ~30-60
			else:
				utility += (100.0 - hunger) * 0.5 # Casual gathering
				
			# Market Value (Profit) if we have surplus
			if agent.get_item_count("Berries") > 5:
				utility += 10.0 * greed
				
		"tree":
			utility += 5.0 * greed
			
		"ore":
			utility += 8.0 * greed # Ore is valuable
			
	# Distance Cost (already there = 0)
	# Stamina Cost
	utility -= 2.0 * (1.0 - agent.personality.get("laziness", 0.5))
	
	return utility

func _eval_move_to_node(agent: Agent, action: Dictionary, world: World, tuning: Dictionary, contract_item: String = "") -> float:
	var node = world.get_node_by_id(action.get("node_id"))
	if node == null: return -1.0
	
	# Project utility of gathering there, minus travel cost
	var gather_utility = _eval_gather(agent, {"type": Actions.TYPE_GATHER_NODE, "node_id": node.id}, world, tuning, contract_item)
	var dist = absi(agent.pos_x - node.pos_x) + absi(agent.pos_y - node.pos_y)
	
	return gather_utility - (dist * 0.5)
	
func _eval_move_to_market(agent: Agent, action: Dictionary) -> float:
	# Utility depends on:
	# 1. Need to sell (Overflowing inventory)
	# 2. Need to buy (Starving + has money)
	
	var utility = 0.0
	var money = agent.get_available_money()
	
	# Already close? (Don't prioritize moving if we are basically there)
	# But actually this action usually means "Go to the exact market tile"
	
	# Need to Buy
	if agent.get_hunger() < 40.0 and money > 10:
		if not agent.has_available_item("Berries") and not agent.has_available_item("CookedMeal"):
			# Was 30.0. lowering slightly to allow Gathering (which can be 50+) to win if nearby.
			# But if gathering is far, market might win.
			# Let's make this proportional to hunger too.
			var urgency = (40.0 - agent.get_hunger())
			utility += 15.0 + urgency # Max ~55
			
	# Need to Sell (Inventory Pressure)
	var inventory_size = 0
	# Sort inventory keys for deterministic iteration order
	var sorted_items: Array = agent.inventory.keys()
	sorted_items.sort()
	for k in sorted_items:
		inventory_size += agent.inventory[k]
		
	if inventory_size > 20: # Higher threshold
		utility += 5.0 # Lower base utility
		
	return utility

func _eval_sell(agent: Agent, action: Dictionary, market: Market, tuning: Dictionary) -> float:
	var item = action.get("item")
	var greed = agent.personality.get("greed", 0.5)
	
	# Profit potential
	var price = market.get_ref_price(item)
	var utility = price * 0.1 * greed
	
	# Inventory clearance
	utility += 2.0
	
	return utility

func _eval_buy(agent: Agent, action: Dictionary, market: Market, tuning: Dictionary, contract_item: String = "") -> float:
	var item = action.get("item")
	var money = agent.get_available_money()
	
	# Check affordability (estimate)
	var ref_price = market.get_ref_price(item)
	if money < ref_price:
		return 0.0 # Can't afford
		
	# Contract Requirement
	if contract_item != "" and item == contract_item:
		# If we have money, buying the contract item is very good
		return 60.0 # Beats most idle things
	
	if item == "Berries" or item == "CookedMeal":
		# Buying Food
		var hunger = agent.get_hunger()
		if hunger < 50.0:
			var urgency = (50.0 - hunger)
			# Exponential ramp up as hunger gets critical to ensure we don't just Idle
			if hunger < 20.0:
				urgency *= 4.0
			return 50.0 + urgency
			
	if item == "Axe" or item == "Pickaxe":
		# Investment
		if agent.role == "gatherer" and item == "Axe": return 20.0
		if agent.role == "miner" and item == "Pickaxe": return 20.0
		
	return 0.0

func _eval_craft(agent: Agent, action: Dictionary, world: World, market: Market, tuning: Dictionary) -> float:
	# Check profitability
	var greed = agent.personality.get("greed", 0.5)
	# Simplified: Assume profitable if here
	return 15.0 * greed

func _eval_move_to_workshop(agent: Agent, action: Dictionary, world: World) -> float:
	# Anticipate crafting utility
	return 10.0 # Placeholder

func _eval_explore(agent: Agent, world: World) -> float:
	# Explore if nothing better to do
	# Curiosity / Risk taking
	var risk = agent.personality.get("risk_tolerance", 0.5)
	return 2.0 * risk

# ==============================================================================
# NEW EVALUATORS
# ==============================================================================

func _eval_accept_contract(agent: Agent, action: Dictionary, contracts_system: ContractsSystem, market: Market, tuning: Dictionary) -> float:
	var contract_id = action.get("contract_id")
	var contract = contracts_system.get_contract(contract_id)
	if contract == null: return -1.0
	
	var greed = agent.personality.get("greed", 0.5)
	var have: int = agent.get_available_item(contract.item)
	var need: int = maxi(0, contract.qty - have)
	var ref_price: float = market.get_ref_price(contract.item)
	var estimated_cost: float = need * ref_price
	var profit = contract.payout - estimated_cost
	
	# High base utility to ensure agents actually pick up contracts
	return 20.0 + (profit * 0.1 * greed)

func _eval_deliver_contract(agent: Agent, action: Dictionary, contracts_system: ContractsSystem) -> float:
	var contract_id = action.get("contract_id")
	var contract = contracts_system.get_contract(contract_id)
	if contract == null: return -1.0
	
	# Very high utility - if we are AT the spot with items, DO IT
	return 100.0

func _eval_move_to_delivery(agent: Agent, action: Dictionary, contracts_system: ContractsSystem) -> float:
	var contract_id = action.get("contract_id")
	var contract = contracts_system.get_contract(contract_id)
	if contract == null: return -1.0
	
	var dist = absi(agent.pos_x - contract.delivery_pos_x) + absi(agent.pos_y - contract.delivery_pos_y)
	return 40.0 - (dist * 0.5)

func _eval_move_to_position(agent: Agent, action: Dictionary) -> float:
	return 5.0 # Low priority default

func _eval_claim_tile(agent: Agent, action: Dictionary, world: World) -> float:
	var greed = agent.personality.get("greed", 0.5)
	return 10.0 * greed

func _eval_build_workshop(agent: Agent, action: Dictionary) -> float:
	var greed = agent.personality.get("greed", 0.5)
	return 15.0 * greed

func _eval_vote(agent: Agent, action: Dictionary) -> float:
	# Always vote if possible
	return 10.0

func _eval_contribute_project(agent: Agent, action: Dictionary) -> float:
	var social = agent.personality.get("social_need", 0.5)
	return 10.0 * social

func _eval_build_road(agent: Agent, action: Dictionary, world: World) -> float:
	var greed = agent.personality.get("greed", 0.5)
	# Faction members like roads
	if agent.faction_id != 0:
		return 12.0 * greed
	return 5.0 * greed

# ==============================================================================
# UTILITY CURVES
# ==============================================================================

# Logistic curve (S-curve)
# x: input 0..1
# k: steepness
# x0: midpoint
func _curve_sigmoid(x: float, x0: float, k: float) -> float:
	return 1.0 / (1.0 + exp(-k * (x - x0)))

# Logit-like (clamped to prevent NaN from log of negative/zero)
func _curve_logit(x: float) -> float:
	# Clamp x to prevent division issues: when x approaches 1, denominator goes to 0 or negative
	var clamped_x := clampf(x, 0.0, 0.95)
	var denom := (1.0 / (clamped_x + 0.01)) - 1.0
	if denom <= 0.0:
		return 10.0  # Cap at high value instead of NaN/inf
	return -log(denom)
	
# ==============================================================================
# HELPERS
# ==============================================================================

func _find_nearby_nodes(agent: Agent, world: World, tuning: Dictionary, radius: int) -> Array:
	var nodes = []
	for node in world.resource_nodes:
		if node.stock > 0:
			var dist = absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
			if dist <= radius:
				nodes.append(node)
	
	# Fallback: finding *any* node if none nearby
	if nodes.is_empty():
		for node in world.resource_nodes:
			if node.stock > 0:
				nodes.append(node)
				if nodes.size() > 5: # Just get a few
					break
					
	return nodes

func _can_harvest_node(agent: Agent, node: ResourceNode, world: World, state: SimState) -> bool:
	if node == null or not node.has_stock(1):
		return false
	if state == null:
		return true
	var owner_id := world.get_claim_owner(node.pos_x, node.pos_y)
	var laws := state.get_laws(owner_id)
	var permit := laws.check_harvest_permit(agent.id, agent.faction_id, owner_id)
	return bool(permit.get("allowed", true))

func _find_nearest_node_of_type(agent: Agent, world: World, type: String) -> ResourceNode:
	var best_node = null
	var best_dist = 999999

	# Optimize: Use grid partitioning eventually. For now, scan all (usually < 100)
	for node in world.resource_nodes:
		if node.type == type and node.stock > 0:
			var dist = absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
			if dist < best_dist:
				best_dist = dist
				best_node = node
				
	return best_node

func _find_nearest_accessible_node_of_type(agent: Agent, world: World, state: SimState, type: String) -> ResourceNode:
	var best_node = null
	var best_dist = 999999

	for node in world.resource_nodes:
		if node.type != type or node.stock <= 0:
			continue
		if not _can_harvest_resource_node(agent, world, state, node):
			continue
		var dist = absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
		if dist < best_dist:
			best_dist = dist
			best_node = node

	return best_node

func _can_harvest_resource_node(agent: Agent, world: World, state: SimState, node: ResourceNode) -> bool:
	if node == null or state == null:
		return true
	var owner := world.get_claim_owner(node.pos_x, node.pos_y)
	if owner <= 0:
		return true
	var laws := state.get_laws(owner)
	return laws.has_harvest_permit(agent.id, agent.faction_id, owner)

func _plan_expand_faction(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary, state: SimState) -> Dictionary:
	if agent.faction_id == 0:
		return {}
		
	# 1. Check if we already have a target
	if goal.has("target_x") and goal.has("target_y"):
		var tx = goal.target_x
		var ty = goal.target_y
		
		# Check if valid still (not claimed by someone else?)
		var owner = world.get_claim_owner(tx, ty)
		if owner > 0:
			# Claimed! 
			# If by us, we are done (but _is_goal_complete should catch this).
			# If by other, we failed.
			# Let's return {} to force goal pop if we can't claim it.
			var my_faction_owner = World.faction_owner_id(agent.faction_id)
			if owner == my_faction_owner:
				return {} # Done
			else:
				# Claimed by someone else, abort this specific target
				# Clear target and retry? Or just abort goal?
				# Abort goal is safer to prevent infinite loop
				return {}
				
		# Proceed with claim
		if agent.is_at(tx, ty):
			return Actions.claim_tile(tx, ty, true)
		else:
			return Actions.claim_tile(tx, ty, true)

	# 2. Find new target
	var faction_owner_id: int = World.faction_owner_id(agent.faction_id)
	var all_claims: Dictionary = world.get_claims_by_owner()
	var my_claims: Array = all_claims.get(faction_owner_id, [])
	
	if my_claims.is_empty():
		return {}

	var candidates = []
	var visited = {} 
	
	for tile in my_claims:
		var neighbors = [
			Vector2i(tile.x, tile.y - 1),
			Vector2i(tile.x, tile.y + 1),
			Vector2i(tile.x - 1, tile.y),
			Vector2i(tile.x + 1, tile.y)
		]
		
		for n in neighbors:
			if not visited.has(n):
				visited[n] = true
				if world.is_valid(n.x, n.y) and not world.is_claimed(n.x, n.y):
					var dist = absi(agent.pos_x - n.x) + absi(agent.pos_y - n.y)
					candidates.append({"x": n.x, "y": n.y, "dist": dist})
	
	if candidates.is_empty():
		return {}
		
	candidates.sort_custom(func(a, b): return a.dist < b.dist)
	var best = candidates[0]
	
	# Cache it!
	goal["target_x"] = best.x
	goal["target_y"] = best.y
	
	return Actions.claim_tile(best.x, best.y, true)

func _plan_build_road(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary) -> Dictionary:
	var tx = goal.get("target_x", -1)
	var ty = goal.get("target_y", -1)
	
	if tx == -1: return {}
	
	# Move toward target area if not already building a route
	# Simple: build road at current position if on the way to market/home
	if world.has_road(agent.pos_x, agent.pos_y):
		# Move one step toward target
		return Actions.move_to_position(tx, ty)
	else:
		return Actions.build_road(agent.pos_x, agent.pos_y)

func _plan_build_strategic_workshop(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary) -> Dictionary:
	# Find best location for workshop
	var best_location = null
	var best_score = -999.0
	
	# Try positions around agent
	var radius = 5
	for dx in range(-radius, radius + 1):
		for dy in range(-radius, radius + 1):
			var x = agent.pos_x + dx
			var y = agent.pos_y + dy
			
			if not world.is_valid(x, y):
				continue
			if world.has_workshop_at(x, y):
				continue
			if world.is_claimed(x, y):
				continue
				
			# Score based on resources nearby
			var score = 0.0
			for node in world.resource_nodes:
				var dist = absi(node.pos_x - x) + absi(node.pos_y - y)
				if dist <= 10:
					score += 5.0 - (dist * 0.5)
			
			# Prefer locations near market for trade access
			var market_x = tuning.get("market_pos_x", 48)
			var market_y = tuning.get("market_pos_y", 48)
			var market_dist = absi(x - market_x) + absi(y - market_y)
			score += 10.0 - (market_dist * 0.1)
			
			if score > best_score:
				best_score = score
				best_location = Vector2i(x, y)
	
	if best_location == null:
		return {}
	
	# Move to location and build
	if agent.is_at(best_location.x, best_location.y):
		return Actions.build_workshop(best_location.x, best_location.y)
	else:
		return Actions.move_to_position(best_location.x, best_location.y)

func _plan_claim_resources(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary) -> Dictionary:
	# Find nearest valuable unclaimed resource
	var search_radius = 15
	var best_node = null
	var best_score = -999.0
	
	for node in world.resource_nodes:
		if world.is_claimed(node.pos_x, node.pos_y):
			continue
			
		var dist = absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
		if dist > search_radius:
			continue
			
		# Score based on resource type and agent needs
		var score = 0.0
		match node.type:
			"berry":
				if agent.get_hunger() < 60:
					score = 100.0 - (dist * 2.0)
			"tree":
				if not agent.has_tool("Axe"):
					score = 80.0 - (dist * 2.0)
				else:
					score = 40.0 - (dist * 2.0)
			"ore":
				if not agent.has_tool("Pickaxe"):
					score = 90.0 - (dist * 2.0)
				else:
					score = 50.0 - (dist * 2.0)
		
		if score > best_score:
			best_score = score
			best_node = node
	
	if best_node == null:
		return {}  # No resources found
	
	# Move to and claim the resource
	if agent.is_at(best_node.pos_x, best_node.pos_y):
		return Actions.claim_tile(best_node.pos_x, best_node.pos_y, false)
	else:
		return Actions.move_to_position(best_node.pos_x, best_node.pos_y)

func _plan_establish_homestead(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary) -> Dictionary:
	var target_x: int = int(goal.get("x", agent.pos_x))
	var target_y: int = int(goal.get("y", agent.pos_y))
	var radius: int = int(goal.get("radius", tuning.get("homestead_claim_radius", 3)))
	if not agent.is_at(target_x, target_y):
		return Actions.move_to_position(target_x, target_y)
	if world.get_claim_owner(target_x, target_y) != agent.id:
		if not agent.has_claim_tokens():
			return {}
		return Actions.claim_tile(target_x, target_y, false)
	if not agent.has_home():
		agent.home_pos = Vector2i(target_x, target_y)
	if not agent.has_claim_tokens():
		return {}
	var next_tile := _find_unclaimed_tile_in_radius(world, agent.home_pos.x, agent.home_pos.y, radius)
	if next_tile == Vector2i(-1, -1):
		return {}
	if agent.is_at(next_tile.x, next_tile.y):
		return Actions.claim_tile(next_tile.x, next_tile.y, false)
	return Actions.move_to_position(next_tile.x, next_tile.y)

func _plan_build_personal_stockpile(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary,
		state: SimState) -> Dictionary:
	if state == null:
		return {}
	if agent.personal_stockpile_id >= 0:
		return {}
	var target_x: int = int(goal.get("x", agent.home_pos.x))
	var target_y: int = int(goal.get("y", agent.home_pos.y))
	if not agent.is_at(target_x, target_y):
		return Actions.move_to_position(target_x, target_y)
	var existing := state.structures.get_structure_at(target_x, target_y)
	if existing != null:
		if existing.structure_type == StructureState.TYPE_STOCKPILE and existing.owner_id == agent.id:
			agent.personal_stockpile_id = existing.id
			if existing.id not in agent.owned_structures:
				agent.owned_structures.append(existing.id)
		return {}
	return Actions.build_personal_stockpile(target_x, target_y)

func _plan_build_personal_shelter(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary,
		state: SimState) -> Dictionary:
	if state == null:
		return {}
	if agent.shelter_id >= 0:
		return {}
	var target_x: int = int(goal.get("x", agent.home_pos.x))
	var target_y: int = int(goal.get("y", agent.home_pos.y))
	if not agent.is_at(target_x, target_y):
		return Actions.move_to_position(target_x, target_y)
	var existing := state.structures.get_structure_at(target_x, target_y)
	if existing != null:
		if existing.structure_type == StructureState.TYPE_SHELTER and existing.owner_id == agent.id:
			agent.shelter_id = existing.id
			if existing.id not in agent.owned_structures:
				agent.owned_structures.append(existing.id)
		return {}
	return Actions.build_personal_shelter(target_x, target_y)

func _plan_deposit_surplus(agent: Agent, goal: Dictionary, world: World, tuning: Dictionary,
		state: SimState) -> Dictionary:
	if state == null:
		return {}
	if agent.personal_stockpile_id < 0:
		return {}
	if _get_total_inventory(agent) <= 20:
		return {}
	var structure := state.structures.get_structure(agent.personal_stockpile_id)
	if structure == null:
		return {}
	if not agent.is_at(structure.pos_x, structure.pos_y):
		return Actions.move_to_position(structure.pos_x, structure.pos_y)
	for item_name in agent.inventory:
		var qty := int(agent.get_available_item(item_name))
		if qty <= 0:
			continue
		return Actions.deposit_to_personal_stockpile(structure.id, item_name, qty)
	return {}

func _find_unclaimed_tile_in_radius(world: World, center_x: int, center_y: int, radius: int) -> Vector2i:
	for dx in range(-radius, radius + 1):
		for dy in range(-radius, radius + 1):
			var x := center_x + dx
			var y := center_y + dy
			if not world.is_valid(x, y):
				continue
			if world.is_claimed(x, y):
				continue
			return Vector2i(x, y)
	return Vector2i(-1, -1)

func _get_total_inventory(agent: Agent) -> int:
	var total := 0
	for item in agent.inventory:
		total += int(agent.inventory[item])
	return total
