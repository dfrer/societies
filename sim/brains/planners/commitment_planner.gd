## Surfaces active job board activities and contracts as explicit goals
## Priority: highest — commitments must be honored before new goals
class_name CommitmentPlanner
extends IAgentPlanner

func get_priority() -> int:
	return 100 # Highest priority

func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	# Check for active contract
	if agent.active_contract_id >= 0:
		var contract = context.contracts_system.get_contract(agent.active_contract_id)
		if contract != null and contract.status == Contract.STATUS_ACCEPTED:
			agent.goal_stack.push_back({
				type = "FULFILL_CONTRACT",
				contract_id = agent.active_contract_id,
				is_goal = true
			})
			return true

	# Check for claimed activity
	if agent.current_activity_id >= 0 and context.state != null:
		var activity = context.state.job_board.get_activity(agent.current_activity_id)
		if activity != null:
			agent.goal_stack.push_back({
				type = "COMPLETE_ACTIVITY",
				activity_id = agent.current_activity_id,
				activity_type = activity.get("type", ""),
				is_goal = true
			})
			return true

	return false
