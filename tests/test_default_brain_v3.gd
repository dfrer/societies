extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")
const DefaultBrain = preload("res://sim/brains/default_brain.gd")
const PlannerContext = preload("res://sim/brains/planner_context.gd")
const Contract = preload("res://sim/contract.gd")

func _run() -> void:
	subtest("Commitment goals outrank lower priorities", func():
		var sim = SimFixture.make_sim(501)
		var agent = sim.state.agents[0]
		agent.goal_stack.clear()
		agent.claim_tokens = 0
		agent.goal_data["failed_item_request"] = {
			"item": "MysteryItem",
			"qty": 1
		}

		var contract = Contract.new()
		contract.id = 1
		contract.status = Contract.STATUS_ACCEPTED
		contract.worker_id = agent.id
		sim.contracts_system.contracts.append(contract)
		agent.active_contract_id = contract.id

		var brain = DefaultBrain.new()
		var ctx = PlannerContext.create(sim.state.world, sim.state.market, sim.contracts_system, sim.tuning, sim.recipes, sim.state)
		brain._generate_high_level_goals(agent, ctx)

		assert_false(agent.goal_stack.is_empty(), "Should have at least one goal")
		if not agent.goal_stack.is_empty():
			var goal = agent.goal_stack.back()
			assert_eq(goal.get("type", ""), "FULFILL_CONTRACT", "Commitments should be prioritized")
	)

	subtest("New goal completion logic", func():
		var sim = SimFixture.make_sim(502)
		var agent = sim.state.agents[0]
		agent.add_item("Berries", 5)

		var brain = DefaultBrain.new()
		var goal = {
			"type": "MAINTAIN_FOOD_BUFFER",
			"target_qty": 5
		}
		var complete = brain._is_goal_complete(agent, goal, sim.state.world, sim.contracts_system)
		assert_true(complete, "Food buffer goal should complete when target reached")
	)
