## HomesteadPlanner - handles personal territory and structures
class_name HomesteadPlanner
extends IAgentPlanner

func get_priority() -> int:
	return 70

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	if not agent.has_home() and agent.has_claim_tokens():
		var site := _find_best_homestead_site(agent, context.world, context.state, context.tuning)
		if site != Vector2i(-1, -1):
			var radius: int = int(context.tuning.get("homestead_claim_radius", 3))
			agent.goal_stack.push_back({
				"type": "ESTABLISH_HOMESTEAD",
				"x": site.x,
				"y": site.y,
				"radius": radius,
				"is_goal": true
			})
			return true

	if agent.has_home():
		if not agent.has_personal_stockpile():
			var planks_needed: int = int(context.tuning.get("personal_stockpile_planks", 12))
			var stone_needed: int = int(context.tuning.get("personal_stockpile_stone", 6))
			if agent.has_available_item("Planks", planks_needed) and agent.has_available_item("Stone", stone_needed):
				var stockpile_site := _find_personal_structure_site(agent, context.state)
				agent.goal_stack.push_back({
					"type": "BUILD_PERSONAL_STOCKPILE",
					"x": stockpile_site.x,
					"y": stockpile_site.y,
					"is_goal": true
				})
				return true
		elif not agent.has_personal_shelter():
			var planks_needed: int = int(context.tuning.get("personal_shelter_planks", 20))
			var stone_needed: int = int(context.tuning.get("personal_shelter_stone", 10))
			if agent.has_available_item("Planks", planks_needed) and agent.has_available_item("Stone", stone_needed):
				var shelter_site := _find_personal_structure_site(agent, context.state)
				agent.goal_stack.push_back({
					"type": "BUILD_PERSONAL_SHELTER",
					"x": shelter_site.x,
					"y": shelter_site.y,
					"is_goal": true
				})
				return true

		if agent.has_personal_stockpile():
			if _get_total_inventory(agent) > 20:
				agent.goal_stack.push_back({"type": "DEPOSIT_SURPLUS", "is_goal": true})
				return true

	return false

func _find_best_homestead_site(agent: Agent, world: World, state: SimState, tuning: Dictionary) -> Vector2i:
	if world == null:
		return Vector2i(-1, -1)
	var search_radius: int = int(tuning.get("claim_search_radius", 8))
	var homestead_radius: int = int(tuning.get("homestead_claim_radius", 3))
	var resource_radius: int = int(tuning.get("tile_claim_search_radius", 3))
	var best_score := -999999.0
	var best_site := Vector2i(-1, -1)

	for dx in range(-search_radius, search_radius + 1):
		for dy in range(-search_radius, search_radius + 1):
			var x := agent.pos_x + dx
			var y := agent.pos_y + dy
			if not world.is_valid(x, y):
				continue
			if world.is_claimed(x, y):
				continue
			if _has_structure_or_project_at(state, x, y):
				continue
			if not _is_homestead_area_clear(world, state, x, y, homestead_radius):
				continue

			var score := 0.0
			score += _score_resources(world, x, y, resource_radius, tuning)
			score += _score_distance_from_agents(agent, state, x, y)
			var pollution_penalty: float = float(tuning.get("tile_value_pollution_penalty", 0.5))
			score -= world.get_pollution(x, y) * pollution_penalty * 10.0

			if score > best_score:
				best_score = score
				best_site = Vector2i(x, y)

	return best_site

func _score_resources(world: World, x: int, y: int, radius: int, tuning: Dictionary) -> float:
	var score := 0.0
	var berry_weight: float = float(tuning.get("tile_value_berry_weight", 2.0))
	var tree_weight: float = float(tuning.get("tile_value_tree_weight", 1.0))
	var ore_weight: float = float(tuning.get("tile_value_ore_weight", 1.5))
	var stone_weight: float = float(tuning.get("tile_value_stone_weight", 1.0))
	for node in world.resource_nodes:
		var dist := absi(node.pos_x - x) + absi(node.pos_y - y)
		if dist > radius:
			continue
		var weight := 0.0
		match node.type:
			"berry":
				weight = berry_weight
			"tree":
				weight = tree_weight
			"ore":
				weight = ore_weight
			"stone":
				weight = stone_weight
			_:
				weight = 0.0
		score += weight * float(radius - dist + 1)
	return score

func _score_distance_from_agents(agent: Agent, state: SimState, x: int, y: int) -> float:
	if state == null:
		return 0.0
	var min_dist := 99999
	for other in state.agents:
		if other.id == agent.id:
			continue
		var dist := absi(other.pos_x - x) + absi(other.pos_y - y)
		if dist < min_dist:
			min_dist = dist
	if min_dist == 99999:
		return 0.0
	return float(min_dist) * 0.25

func _is_homestead_area_clear(world: World, state: SimState, center_x: int, center_y: int, radius: int) -> bool:
	for dx in range(-radius, radius + 1):
		for dy in range(-radius, radius + 1):
			var x := center_x + dx
			var y := center_y + dy
			if not world.is_valid(x, y):
				return false
			if world.is_claimed(x, y):
				return false
			if _has_structure_or_project_at(state, x, y):
				return false
	return true

func _has_structure_or_project_at(state: SimState, x: int, y: int) -> bool:
	if state == null:
		return false
	for structure in state.structures.structures:
		if structure.pos_x == x and structure.pos_y == y:
			return true
	for workshop in state.world.workshops:
		if workshop.pos_x == x and workshop.pos_y == y:
			return true
	for project in state.communal_projects.projects:
		if project.is_active() and project.pos_x == x and project.pos_y == y:
			return true
	return false

func _find_personal_structure_site(agent: Agent, state: SimState) -> Vector2i:
	var origin := agent.home_pos
	if state == null:
		return origin
	var search_radius := 2
	for dx in range(-search_radius, search_radius + 1):
		for dy in range(-search_radius, search_radius + 1):
			var x := origin.x + dx
			var y := origin.y + dy
			if x == origin.x and y == origin.y:
				if not _has_structure_or_project_at(state, x, y):
					return Vector2i(x, y)
				continue
			if not _has_structure_or_project_at(state, x, y):
				return Vector2i(x, y)
	return origin

func _get_total_inventory(agent: Agent) -> int:
	var total := 0
	for item in agent.inventory:
		total += int(agent.inventory[item])
	return total
