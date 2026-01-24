## CareerPlanner - assigns careers and drives specialization goals
class_name CareerPlanner
extends IAgentPlanner

func get_priority() -> int:
	return DefaultBrain.PRIORITY_CAREER

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	if agent.career_type == "none" or agent.career_type == "":
		var assessed := _assess_career(agent, context.world)
		if assessed != "":
			agent.career_type = assessed
			agent.role = assessed
			agent.preferred_resource = CareerRegistry.get_preferred_resource(assessed)

	var career_def := CareerRegistry.get_career_definition(agent.career_type)
	if career_def.is_empty():
		return false

	var required_tools: Array = career_def.get("required_tools", [])
	for tool_name in required_tools:
		if not agent.has_tool(tool_name):
			agent.goal_stack.push_back({
				"type": "ACQUIRE_TOOL",
				"item": tool_name,
				"is_goal": true
			})
			return true

	var preferred_workshops: Array = career_def.get("preferred_workshops", [])
	if not preferred_workshops.is_empty():
		var has_access := false
		for workshop_type in preferred_workshops:
			if context.world.has_workshop_type(workshop_type):
				has_access = true
				break
		if not has_access:
			agent.goal_stack.push_back({
				"type": "SECURE_WORKSHOP_ACCESS",
				"workshop_type": preferred_workshops[0],
				"is_goal": true
			})
			return true

	var preferred_resource: String = career_def.get("preferred_resource", "")
	if preferred_resource != "":
		if _should_secure_resource_access(agent, context.world, preferred_resource, context.tuning):
			agent.goal_stack.push_back({
				"type": "SECURE_RESOURCE_ACCESS",
				"resource_type": preferred_resource,
				"is_goal": true
			})
			return true

	return false

func _assess_career(agent: Agent, world: World) -> String:
	if world == null:
		return "gatherer"
	var workshop_radius := 12
	var resource_radius := 14
	if _has_nearby_station(agent, world, ["baker", "stove"], workshop_radius) and _resource_is_abundant(agent, world, "grain", resource_radius):
		return "baker"
	if _has_nearby_station(agent, world, ["kitchen", "stove"], workshop_radius) and _resource_is_abundant(agent, world, "berry", resource_radius):
		return "cook"
	if _has_nearby_station(agent, world, ["forge", "smithy", "smelter"], workshop_radius) and _resource_is_abundant(agent, world, "ore", resource_radius):
		return "blacksmith"
	var best_type := "berry"
	var best_score := 999999
	for node in world.resource_nodes:
		if node == null:
			continue
		if node.type not in ["berry", "tree", "ore"]:
			continue
		var dist := absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
		var score := dist
		if node.type == "tree" and agent.has_tool("Axe"):
			score -= 3
		if node.type == "ore" and agent.has_tool("Pickaxe"):
			score -= 3
		if score < best_score:
			best_score = score
			best_type = node.type
	match best_type:
		"tree":
			return "logger"
		"ore":
			return "miner"
		_:
			return "gatherer"

func _has_nearby_station(agent: Agent, world: World, types: Array, radius: int) -> bool:
	for workshop in world.workshops:
		if workshop == null or not workshop.is_ready():
			continue
		if workshop.workshop_type not in types:
			continue
		var dist := absi(workshop.pos_x - agent.pos_x) + absi(workshop.pos_y - agent.pos_y)
		if dist <= radius:
			return true
	return false

func _resource_is_abundant(agent: Agent, world: World, resource_type: String, radius: int) -> bool:
	var found := 0
	for node in world.resource_nodes:
		if node == null:
			continue
		if node.type != resource_type:
			continue
		var dist := absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
		if dist <= radius:
			found += 1
			if found >= 2:
				return true
	return false

func _should_secure_resource_access(agent: Agent, world: World, resource_type: String, tuning: Dictionary) -> bool:
	if world == null:
		return false
	var search_radius: int = int(tuning.get("career_resource_access_radius", 12))
	var best_dist := 999999
	for node in world.resource_nodes:
		if node == null:
			continue
		if node.type != resource_type:
			continue
		var dist := absi(node.pos_x - agent.pos_x) + absi(node.pos_y - agent.pos_y)
		if dist < best_dist:
			best_dist = dist
	if best_dist <= search_radius:
		return false
	return true
