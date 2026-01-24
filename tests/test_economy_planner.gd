extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")
const EconomyPlanner = preload("res://sim/brains/planners/economy_planner.gd")
const PlannerContext = preload("res://sim/brains/planner_context.gd")

func _run() -> void:
	subtest("Post contract for failed request", func():
		var sim = SimFixture.make_sim(201)
		var agent = sim.state.agents[0]
		agent.claim_tokens = 0
		agent.goal_data["failed_item_request"] = {
			"item": "MysteryItem",
			"qty": 2
		}

		var planner = EconomyPlanner.new()
		var ctx = PlannerContext.create(sim.state.world, sim.state.market, sim.contracts_system, sim.tuning, sim.recipes, sim.state)

		var added = planner.maybe_add_goal(agent, ctx)

		assert_true(added, "Should add a contract posting goal")
		if not agent.goal_stack.is_empty():
			var goal = agent.goal_stack.back()
			assert_eq(goal.get("type", ""), "POST_CONTRACT_FOR_NEED", "Goal should post contract for need")
	)

	subtest("Suppress market purchases while saving", func():
		var sim = SimFixture.make_sim(202)
		var agent = sim.state.agents[0]
		agent.long_term_goals.append({
			"type": "SAVE_FOR_ITEM",
			"item": "Axe",
			"target_money": 120,
			"progress": 0.0
		})
		var planner = EconomyPlanner.new()
		var suppress = planner.should_suppress_market_purchase(agent, "Planks", sim.tuning)
		assert_true(suppress, "Should suppress non-essential market purchases while saving")
	)
