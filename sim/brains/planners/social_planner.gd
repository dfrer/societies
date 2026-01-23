## SocialPlanner - handles relationship-driven goals
class_name SocialPlanner
extends "res://sim/brains/planners/i_agent_planner.gd"

func get_priority() -> int:
	return 50

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	var tuning_data: Dictionary = context.tuning if context.tuning is Dictionary else {}
	var trust_threshold: float = float(tuning_data.get("social_trust_threshold", 0.6))
	var social_need_threshold: float = float(tuning_data.get("social_need_threshold", 60.0))
	var comfort_need_threshold: float = float(tuning_data.get("comfort_need_threshold", 60.0))

	var social_need: float = float(agent.needs.get("social", 100.0))
	var comfort_need: float = float(agent.needs.get("comfort", 100.0))
	var needs_high := social_need < social_need_threshold or comfort_need < comfort_need_threshold

	if needs_high and not _has_trusted_partner(agent, trust_threshold):
		agent.goal_stack.push_front({
			"type": "FIND_TRADING_PARTNER",
			"trust_threshold": trust_threshold,
			"is_goal": true
		})
		return true

	return false

func _has_trusted_partner(agent: Agent, trust_threshold: float) -> bool:
	for partner_id in agent.social_memory:
		var memory: Dictionary = agent.social_memory[partner_id]
		var trust: float = float(memory.get("trust", 0.5))
		if trust >= trust_threshold:
			return true
	return false
