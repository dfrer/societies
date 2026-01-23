## NeedsPlanner - handles survival interrupts and proactive needs
class_name NeedsPlanner
extends IAgentPlanner

func get_priority() -> int:
	return 120

func get_interrupt_action(agent: Agent, tuning: Variant) -> Dictionary:
	var tuning_data: Dictionary = tuning if tuning is Dictionary else {}
	var emergency_threshold: float = float(tuning_data.get("emergency_hunger_threshold", 15.0))
	if agent.get_hunger() < emergency_threshold:
		if agent.has_available_item("CookedMeal"):
			return Actions.eat_meal()
		if agent.has_available_item("Berries"):
			return Actions.eat()
	if agent.get_stamina() <= 0.0:
		return Actions.sleep_rest()
	return {}

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	var eat_threshold: float = context.tuning.get("eat_threshold", 50.0)
	if agent.get_hunger() < eat_threshold:
		if agent.has_available_item("CookedMeal"):
			agent.goal_stack.push_back({"type": "EAT_FOOD", "item": "CookedMeal", "is_goal": true})
			return true
		if agent.has_available_item("Berries"):
			agent.goal_stack.push_back({"type": "EAT_FOOD", "item": "Berries", "is_goal": true})
			return true
		agent.goal_stack.push_back({"type": "OBTAIN_ITEM", "item": "Berries", "qty": 5, "is_goal": true})
		return true

	var stamina_threshold: float = context.tuning.get("stamina_low_threshold", 20.0)
	if agent.get_stamina() < stamina_threshold:
		var shelter := _find_nearby_shelter(agent, context)
		if shelter != null:
			agent.goal_stack.push_back({"type": "EFFICIENT_REST", "shelter_id": shelter.id, "is_goal": true})
		else:
			agent.goal_stack.push_back({"type": "REST", "is_goal": true})
		return true

	var proactive_buffer: int = int(context.tuning.get("proactive_food_buffer", 5))
	if agent.get_hunger() >= 50.0 and get_food_inventory(agent) < proactive_buffer:
		agent.goal_stack.push_back({"type": "MAINTAIN_FOOD_BUFFER", "target_qty": proactive_buffer, "is_goal": true})
		return true

	var comfort: float = float(agent.needs.get("comfort", 100.0))
	if comfort < 30.0 and not agent.has_personal_shelter():
		agent.goal_stack.push_back({"type": "BUILD_PERSONAL_SHELTER", "is_goal": true})
		return true

	var social: float = float(agent.needs.get("social", 100.0))
	if social < 30.0 and agent.faction_id == 0:
		agent.goal_stack.push_back({"type": "FIND_COMMUNITY", "is_goal": true})
		return true

	return false

func get_food_inventory(agent: Agent) -> int:
	return agent.get_item_count("Berries") + agent.get_item_count("CookedMeal")

func _find_nearby_shelter(agent: Agent, context: PlannerContext) -> StructureState:
	if context.state == null:
		return null
	var at_shelter := context.state.structures.get_structure_at(agent.pos_x, agent.pos_y)
	if at_shelter and at_shelter.structure_type == StructureState.TYPE_SHELTER:
		return at_shelter
	if agent.shelter_id >= 0:
		return context.state.structures.get_structure(agent.shelter_id)
	return null
