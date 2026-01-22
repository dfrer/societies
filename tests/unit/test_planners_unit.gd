## Unit tests for planner modules
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Survival Planner - Food Goal", _test_survival_planner_food)
	subtest("Economy Planner - Contract Goal", _test_economy_planner_contract)
	subtest("Governance Planner - Expansion Goal", _test_governance_planner_expansion)

func _test_survival_planner_food() -> void:
	var agent := Fixtures.make_agent({
		"id": 1,
		"hunger": 30.0,
		"inventory": {"Berries": 2}
	})
	var planner := SurvivalPlanner.new()
	var tuning := {
		"eat_threshold": 50.0
	}
	var added := planner.maybe_add_goal(agent, tuning)
	assert_true(added, "Survival planner should add a food goal")
	assert_eq(agent.goal_stack.size(), 1, "Goal stack should contain one goal")
	assert_eq(agent.goal_stack[0]["type"], "EAT_FOOD", "Goal should be EAT_FOOD")

func _test_economy_planner_contract() -> void:
	var state := Fixtures.make_sim_state(12345)
	var agent := Fixtures.make_agent({
		"id": 1,
		"hunger": 90.0
	})
	state.add_agent(agent)

	var contracts := ContractsSystem.new()
	var contract := Fixtures.make_contract(2, "Planks", 2, 200, 100, {"id": 5})
	contract.status = Contract.STATUS_POSTED
	contracts.contracts.append(contract)

	var planner := EconomyPlanner.new()
	var market := Fixtures.make_market()
	var added := planner.add_primary_goal(agent, state.world, market, contracts, state.tuning, {}, state)
	assert_true(added, "Economy planner should add a contract goal")
	assert_eq(agent.goal_stack.back()["type"], "ACCEPT_CONTRACT", "Goal should be ACCEPT_CONTRACT")

func _test_governance_planner_expansion() -> void:
	var state := Fixtures.make_sim_state(12345)
	var agent := Fixtures.make_agent({
		"id": 1,
		"hunger": 90.0,
		"faction_id": 1
	})
	state.add_agent(agent)

	var faction := Fixtures.make_faction(1, {"treasury": 100})
	faction.founder_agent_id = agent.id
	state.factions = [faction]

	var planner := GovernancePlanner.new()
	var added := planner.maybe_add_goal(agent, state.world, state.tuning, state)
	assert_true(added, "Governance planner should add an expansion goal")
	assert_eq(agent.goal_stack.back()["type"], "EXPAND_FACTION", "Goal should be EXPAND_FACTION")
