extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")
const CareerPlanner = preload("res://sim/brains/planners/career_planner.gd")
const PlannerContext = preload("res://sim/brains/planner_context.gd")

func _run() -> void:
	subtest("Logger without axe seeks tool", func():
		var sim = SimFixture.make_sim(301)
		var agent = sim.state.agents[0]
		agent.career_type = "logger"
		agent.role = "logger"
		agent.inventory.erase("Axe")

		var planner = CareerPlanner.new()
		var ctx = PlannerContext.create(sim.state.world, sim.state.market, sim.contracts_system, sim.tuning, sim.recipes, sim.state)

		var added = planner.maybe_add_goal(agent, ctx)

		assert_true(added, "Should add tool acquisition goal")
		if not agent.goal_stack.is_empty():
			var goal = agent.goal_stack.back()
			assert_eq(goal.get("type", ""), "ACQUIRE_TOOL", "Goal should acquire tool")
			assert_eq(goal.get("item", ""), "Axe", "Goal should request axe")
	)
