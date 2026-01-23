## SurvivalPlanner - handles survival interrupts and basic needs
class_name SurvivalPlanner
extends IAgentPlanner

func get_priority() -> int:
	return 90

func get_interrupt_action(agent: Agent, tuning: Dictionary) -> Dictionary:
	var emergency_threshold: float = tuning.get("emergency_hunger_threshold", 15.0)
	if agent.get_hunger() < emergency_threshold:
		if agent.has_available_item("CookedMeal"):
			return Actions.eat_meal()
		if agent.has_available_item("Berries"):
			return Actions.eat()
	if agent.get_stamina() <= 0.0:
		return Actions.sleep_rest() # Must rest
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
	return false
