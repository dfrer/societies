extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")
const HomesteadPlanner = preload("res://sim/brains/planners/homestead_planner.gd")
const PlannerContext = preload("res://sim/brains/planner_context.gd")

func _run() -> void:
	subtest("Establish homestead goal", func():
		var sim = SimFixture.make_sim(101)
		var agent = sim.state.agents[0]
		agent.home_pos = Vector2i(-1, -1)
		var planner = HomesteadPlanner.new()
		var ctx = PlannerContext.create(sim.state.world, sim.state.market, sim.contracts_system, sim.tuning, sim.recipes, sim.state)

		var added = planner.maybe_add_goal(agent, ctx)

		assert_true(added, "Should add establish homestead goal")
		if not agent.goal_stack.is_empty():
			var goal = agent.goal_stack.back()
			assert_eq(goal.get("type", ""), "ESTABLISH_HOMESTEAD", "Goal should establish homestead")
	)

	subtest("Build personal stockpile goal", func():
		var sim = SimFixture.make_sim(102)
		var agent = sim.state.agents[0]
		agent.home_pos = Vector2i(agent.pos_x, agent.pos_y)
		agent.personal_stockpile_id = -1
		var planks_needed: int = int(sim.tuning.get("personal_stockpile_planks", 12))
		var stone_needed: int = int(sim.tuning.get("personal_stockpile_stone", 6))
		agent.add_item("Planks", planks_needed)
		agent.add_item("Stone", stone_needed)

		var planner = HomesteadPlanner.new()
		var ctx = PlannerContext.create(sim.state.world, sim.state.market, sim.contracts_system, sim.tuning, sim.recipes, sim.state)

		var added = planner.maybe_add_goal(agent, ctx)

		assert_true(added, "Should add personal stockpile goal")
		if not agent.goal_stack.is_empty():
			var goal = agent.goal_stack.back()
			assert_eq(goal.get("type", ""), "BUILD_PERSONAL_STOCKPILE", "Goal should build personal stockpile")
	)
