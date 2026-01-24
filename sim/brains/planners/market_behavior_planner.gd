## MarketBehaviorPlanner - handles intentional market visits and price memory
class_name MarketBehaviorPlanner
extends IAgentPlanner

func get_priority() -> int:
	return DefaultBrain.PRIORITY_MARKET_BEHAVIOR

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	var intentions := _gather_market_intentions(agent, context.market, context.tuning)
	var current_tick: int = context.state.tick if context.state != null else 0
	var refresh_ticks: int = int(context.tuning.get("market_price_memory_max_age_ticks", 240))
	if current_tick > 0 and (agent.last_market_visit_tick <= 0 or current_tick - agent.last_market_visit_tick > refresh_ticks):
		_add_intention(intentions, {"type": "CHECK_PRICES"})
	intentions = _normalize_intentions(intentions, current_tick, context.tuning)
	if intentions.is_empty():
		agent.market_intentions = []
		return false
	
	agent.market_intentions = intentions.duplicate(true)
	
	if not agent.goal_stack.is_empty():
		var existing: Dictionary = agent.goal_stack.back()
		if existing.get("type", "") == "GO_TO_MARKET_WITH_INTENT":
			return false
	
	var market_x: int = int(context.tuning.get("market_pos_x", 48))
	var market_y: int = int(context.tuning.get("market_pos_y", 48))
	agent.goal_stack.push_back({
		"type": "GO_TO_MARKET_WITH_INTENT",
		"intentions": intentions.duplicate(true),
		"market_x": market_x,
		"market_y": market_y,
		"is_goal": true
	})
	return true

func _gather_market_intentions(agent: Agent, market: Market, tuning: Dictionary) -> Array[Dictionary]:
	var intentions: Array[Dictionary] = []
	
	# Retain existing intentions (remove duplicates later)
	for intention in agent.market_intentions:
		if intention is Dictionary:
			intentions.append(intention.duplicate(true))
	
	# BUY_NEEDS: Need-driven purchases after failed obtain attempts
	var failed_request: Dictionary = agent.goal_data.get("failed_item_request", {})
	if not failed_request.is_empty():
		var item: String = failed_request.get("item", "")
		var qty: int = int(failed_request.get("qty", 1))
		if item != "" and not _should_skip_market_buy(agent, item, tuning):
			_add_intention(intentions, {
				"type": "BUY_NEEDS",
				"item": item,
				"qty": qty,
				"max_price": int(market.get_ref_price(item))
			})
	
	# SELL_SURPLUS: Offload surplus inventory
	var surplus_targets := {
		"Berries": int(tuning.get("sell_surplus_food_over", 6)),
		"Logs": int(tuning.get("sell_logs_over", 3)),
		"Ore": int(tuning.get("sell_ore_over", 2)),
		"Planks": int(tuning.get("sell_planks_over", 5)),
		"CookedMeal": int(tuning.get("sell_meals_over", 3))
	}
	for item_name in surplus_targets.keys():
		var surplus_over: int = surplus_targets[item_name]
		if agent.get_available_item(item_name) > surplus_over:
			_add_intention(intentions, {
				"type": "SELL_SURPLUS",
				"item": item_name,
				"qty": agent.get_available_item(item_name) - surplus_over
			})
	
	# FIND_WORK: Visit market to look for contracts
	if not agent.has_active_contract():
		_add_intention(intentions, {
			"type": "FIND_WORK"
		})
	
	# CHECK_PRICES: Update price memory if empty or stale
	if agent.market_price_memory.is_empty() or agent.last_market_visit_tick <= 0:
		_add_intention(intentions, {
			"type": "CHECK_PRICES"
		})

	return intentions

func _should_skip_market_buy(agent: Agent, item: String, tuning: Dictionary) -> bool:
	if not _has_save_for_item_goal(agent):
		return false
	var emergency_threshold: float = float(tuning.get("emergency_hunger_threshold", 15.0))
	if item in ["Berries", "CookedMeal"] and agent.get_hunger() < emergency_threshold:
		return false
	return true

func _has_save_for_item_goal(agent: Agent) -> bool:
	for goal in agent.long_term_goals:
		if goal.get("type", "") == "SAVE_FOR_ITEM":
			return true
	for goal in agent.goal_stack:
		if goal.get("type", "") == "SAVE_FOR_ITEM":
			return true
	return false

func _normalize_intentions(intentions: Array[Dictionary], current_tick: int, tuning: Dictionary) -> Array[Dictionary]:
	var ttl: int = int(tuning.get("market_intention_ttl_ticks", 120))
	var max_attempts: int = int(tuning.get("market_intention_max_attempts", 3))
	var normalized: Array[Dictionary] = []
	for intention in intentions:
		if not (intention is Dictionary):
			continue
		var updated := intention.duplicate(true)
		if not updated.has("attempts"):
			updated["attempts"] = 0
		if not updated.has("expires_tick"):
			updated["expires_tick"] = current_tick + ttl if current_tick > 0 else -1
		var expires_tick: int = int(updated.get("expires_tick", -1))
		var attempts: int = int(updated.get("attempts", 0))
		if expires_tick > 0 and current_tick > 0 and current_tick >= expires_tick:
			continue
		if attempts >= max_attempts:
			continue
		normalized.append(updated)
	return normalized

func _add_intention(intentions: Array[Dictionary], candidate: Dictionary) -> void:
	var candidate_type: String = candidate.get("type", "")
	var candidate_item: String = candidate.get("item", "")
	for existing in intentions:
		if existing.get("type", "") != candidate_type:
			continue
		if candidate_item == "" or existing.get("item", "") == candidate_item:
			return
	intentions.append(candidate)
