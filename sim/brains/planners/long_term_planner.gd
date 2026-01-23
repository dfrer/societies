## LongTermPlanner - manages persistent, multi-day goals
class_name LongTermPlanner
extends "res://sim/brains/planners/i_agent_planner.gd"

func get_priority() -> int:
	return 30

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	var tuning_data: Dictionary = context.tuning if context.tuning is Dictionary else {}

	_maybe_add_save_for_item_goal(agent, context.market, tuning_data, context.state)

	var completed_indices: Array[int] = []
	for i in range(agent.long_term_goals.size()):
		var long_term_goal: Dictionary = agent.long_term_goals[i]
		if long_term_goal.get("type", "") != "SAVE_FOR_ITEM":
			continue
		var item: String = long_term_goal.get("item", "")
		var target_money: int = int(long_term_goal.get("target_money", 0))
		if _is_save_goal_complete(agent, item, target_money):
			completed_indices.append(i)
			continue
		if not _has_goal_in_stack(agent.goal_stack, "SAVE_FOR_ITEM"):
			agent.goal_stack.push_front({
				"type": "SAVE_FOR_ITEM",
				"item": item,
				"target_money": target_money,
				"is_goal": true
			})
			return true

	for idx in range(completed_indices.size() - 1, -1, -1):
		agent.long_term_goals.remove_at(completed_indices[idx])

	return false

func _maybe_add_save_for_item_goal(agent: Agent, market: Market, tuning: Dictionary, state: SimState) -> void:
	if agent.has_tool("Axe"):
		return

	var ref_price := 0
	if market != null:
		ref_price = int(market.get_ref_price("Axe"))
	var default_target: int = int(tuning.get("long_term_axe_target_money", 120))
	var savings_buffer: float = float(tuning.get("long_term_savings_buffer", 1.2))
	var target_money: int = int(maxi(default_target, int(ref_price * savings_buffer)))

	if agent.get_available_money() >= target_money:
		return

	for existing in agent.long_term_goals:
		if existing.get("type", "") == "SAVE_FOR_ITEM" and existing.get("item", "") == "Axe":
			return

	var started_tick: int = state.tick if state != null else 0
	agent.long_term_goals.append({
		"type": "SAVE_FOR_ITEM",
		"item": "Axe",
		"target_money": target_money,
		"started_tick": started_tick,
		"progress": 0.0
	})

func _is_save_goal_complete(agent: Agent, item: String, target_money: int) -> bool:
	if item != "" and agent.has_item(item, 1):
		return true
	if agent.get_available_money() >= target_money:
		return true
	return false

func _has_goal_in_stack(goal_stack: Array, goal_type: String) -> bool:
	for goal in goal_stack:
		if goal.get("type", "") == goal_type:
			return true
	return false
