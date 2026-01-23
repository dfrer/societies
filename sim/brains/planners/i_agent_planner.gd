## Abstract interface for goal-generating planners
class_name IAgentPlanner
extends RefCounted

## Attempt to add a goal to the agent's goal stack.
## Returns true if a goal was added, false otherwise.
func maybe_add_goal(agent: Agent, context: PlannerContext) -> bool:
	return false

## Returns the priority of this planner (higher = runs first)
func get_priority() -> int:
	return 0
