## CivicPlanner - handles governance participation and faction membership
class_name CivicPlanner
extends "res://sim/brains/planners/i_agent_planner.gd"

func get_priority() -> int:
	return 40

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	if context.state == null:
		return false

	var tuning_data: Dictionary = context.tuning if context.tuning is Dictionary else {}

	if agent.faction_id == 0:
		var grievance_threshold: float = float(tuning_data.get("civic_join_grievance_threshold", 0.3))
		var social_need_threshold: float = float(tuning_data.get("civic_join_social_need_threshold", 60.0))
		var social_need: float = float(agent.needs.get("social", 100.0))
		if agent.grievance <= grievance_threshold or social_need < social_need_threshold:
			var best_faction: Faction = null
			var best_score := -999.0
			for faction in context.state.factions:
				var score := _score_faction_for_joining(agent, faction, tuning_data)
				if score > best_score:
					best_score = score
					best_faction = faction
			var min_score: float = float(tuning_data.get("join_min_score", 0.3))
			if best_faction != null and best_score >= min_score:
				agent.goal_stack.push_front({
					"type": "JOIN_FACTION",
					"faction_id": best_faction.id,
					"is_goal": true
				})
				return true
		return false

	var my_faction: Faction = context.state.factions_system.get_faction(agent.faction_id, context.state.factions)
	if my_faction == null:
		return false

	var proposals := my_faction.get_active_proposals(context.state.tick)
	for proposal in proposals:
		var votes_for: Array = proposal.get("votes_for", [])
		var votes_against: Array = proposal.get("votes_against", [])
		if agent.id in votes_for or agent.id in votes_against:
			continue
		var faction_owner_id := World.owner_id_for_faction(my_faction.id)
		var laws: Laws = context.state.get_laws(faction_owner_id)
		var changes: Dictionary = proposal.get("changes", {})
		var vote_for := context.state.factions_system._should_vote_for(agent, changes, laws)
		agent.goal_stack.push_front({
			"type": "VOTE_ON_PROPOSAL",
			"proposal_id": proposal.get("proposal_id", -1),
			"vote_for": vote_for,
			"is_goal": true
		})
		return true

	if agent.grievance > 0.5:
		agent.goal_stack.push_front({
			"type": "PROPOSE_LAW_CHANGE",
			"is_goal": true
		})
		return true

	return false

func _score_faction_for_joining(agent: Agent, faction: Faction, tuning: Dictionary) -> float:
	if faction == null:
		return -999.0

	var search_radius: int = int(tuning.get("join_search_radius", 15))
	var distance := absi(faction.home_pos.x - agent.pos_x) + absi(faction.home_pos.y - agent.pos_y)
	if distance > search_radius:
		return -999.0

	var treasury_divisor: float = float(tuning.get("civic_join_treasury_divisor", 100.0))
	var member_weight: float = float(tuning.get("civic_join_member_weight", 0.05))
	var distance_weight: float = float(tuning.get("civic_join_distance_weight", 1.0))

	var treasury_score := float(faction.treasury) / maxf(1.0, treasury_divisor)
	var member_score := float(faction.get_member_count()) * member_weight
	var distance_score := (1.0 / maxf(1.0, float(distance))) * distance_weight

	return treasury_score + member_score + distance_score
