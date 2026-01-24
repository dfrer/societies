extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")
const CommitmentPlanner = preload("res://sim/brains/planners/commitment_planner.gd")
const PlannerContext = preload("res://sim/brains/planner_context.gd")
const Contract = preload("res://sim/contract.gd")

func _run() -> void:
	subtest("Active contract becomes commitment goal", func():
		var sim = SimFixture.make_sim(401)
		var agent = sim.state.agents[0]

		var contract = Contract.new()
		contract.id = 1
		contract.status = Contract.STATUS_ACCEPTED
		contract.worker_id = agent.id
		contract.item = "Berries"
		contract.qty = 1
		sim.contracts_system.contracts.append(contract)

		agent.active_contract_id = contract.id

		var planner = CommitmentPlanner.new()
		var ctx = PlannerContext.create(sim.state.world, sim.state.market, sim.contracts_system, sim.tuning, sim.recipes, sim.state)

		var added = planner.maybe_add_goal(agent, ctx)

		assert_true(added, "Should add commitment goal for active contract")
		if not agent.goal_stack.is_empty():
			var goal = agent.goal_stack.back()
			assert_eq(goal.get("type", ""), "FULFILL_CONTRACT", "Goal should fulfill contract")
	)
