## GovernancePlanner - handles faction expansion and infrastructure goals
class_name GovernancePlanner
extends RefCounted

func maybe_add_goal(agent: Agent, world: World, tuning: Dictionary, state: SimState = null) -> bool:
	if state == null:
		return false
	if agent.faction_id == 0:
		return false

	var my_faction = null
	for faction in state.factions:
		if faction.id == agent.faction_id:
			my_faction = faction
			break

	if my_faction and my_faction.founder_agent_id == agent.id:
		var claim_cost: int = int(tuning.get("faction_claim_cost", 15))
		if my_faction.treasury >= claim_cost * 2:
			agent.goal_stack.push_back({"type": "EXPAND_FACTION", "is_goal": true})
			return true

	if agent.has_tool("Shovel"):
		var market_pos = Vector2i(int(tuning.get("market_pos_x", 48)), int(tuning.get("market_pos_y", 48)))
		if state.rng.randf() < 0.05:
			agent.goal_stack.push_back({"type": "BUILD_ROAD", "target_x": market_pos.x, "target_y": market_pos.y, "is_goal": true})
			return true

	return false
