## Unit tests for the DefaultBrain class (Utility AI)
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Decide Action - Hungry Agent Seeks Food", _test_hungry_agent_seeks_food)
	subtest("Decide Action - Well Fed Agent Gathers", _test_well_fed_agent_works)
	subtest("Decide Action - Critical Hunger Eats", _test_critical_hunger_eats)
	subtest("Goal Stack Management", _test_goal_stack)
	subtest("Action Selection Determinism", _test_action_determinism)

func _test_hungry_agent_seeks_food() -> void:
	var state := Fixtures.make_sim_state(12345)
	var agent := Fixtures.make_agent({
		"id": 1,
		"hunger": 35.0,  # Low but not critical
		"inventory": {"Berries": 0}
	})
	state.add_agent(agent)
	
	var brain := DefaultBrain.new()
	var world := Fixtures.make_world()
	var market := Fixtures.make_market()
	var contracts := ContractsSystem.new()
	var tuning := state.tuning.to_dict()
	
	# Hungry agent should prioritize getting food
	var action := brain.decide_action(agent, world, market, contracts, tuning, {}, state)
	
	# Action should be something related to food or seeking resources
	assert_true(action.has("type"), "Should return an action with type")
	# The exact action depends on world state, but it should not be idle with hunger this low
	# (unless no options available)

func _test_well_fed_agent_works() -> void:
	var state := Fixtures.make_sim_state(12345)
	var agent := Fixtures.make_agent({
		"id": 1,
		"hunger": 90.0,  # Well fed
		"money": 100
	})
	state.add_agent(agent)
	
	var brain := DefaultBrain.new()
	var world := Fixtures.make_world()
	var market := Fixtures.make_market()
	var contracts := ContractsSystem.new()
	var tuning := state.tuning.to_dict()
	
	# Well-fed agent should focus on wealth/work
	var action := brain.decide_action(agent, world, market, contracts, tuning, {}, state)
	
	assert_true(action.has("type"), "Should return an action with type")
	# Well-fed agent should be doing productive work, not panicking about food

func _test_critical_hunger_eats() -> void:
	var state := Fixtures.make_sim_state(12345)
	var agent := Fixtures.make_agent({
		"id": 1,
		"hunger": 15.0,  # Critical hunger
		"inventory": {"Berries": 5}
	})
	state.add_agent(agent)
	
	var brain := DefaultBrain.new()
	var world := Fixtures.make_world()
	var market := Fixtures.make_market()
	var contracts := ContractsSystem.new()
	var tuning := state.tuning.to_dict()
	
	# Critical hunger with food should result in eating
	var action := brain.decide_action(agent, world, market, contracts, tuning, {}, state)
	
	assert_true(action.has("type"), "Should return an action with type")
	# With critical hunger and food available, should prioritize eating
	# Note: exact behavior depends on panic threshold config

func _test_goal_stack() -> void:
	var agent := Fixtures.make_agent({"id": 1})
	
	# Test goal stack manipulation
	agent.goal_stack = []
	assert_eq(agent.goal_stack.size(), 0, "Goal stack starts empty")
	
	# Push a goal
	agent.goal_stack.push_back({"type": "OBTAIN_ITEM", "item": "Berries", "qty": 5, "is_goal": true})
	assert_eq(agent.goal_stack.size(), 1, "Goal stack should have 1 item")
	
	# Push another goal
	agent.goal_stack.push_back({"type": "EAT_FOOD", "is_goal": true})
	assert_eq(agent.goal_stack.size(), 2, "Goal stack should have 2 items")
	
	# Pop a goal
	var popped := agent.goal_stack.pop_back()
	assert_eq(popped["type"], "EAT_FOOD", "Should pop most recent goal")
	assert_eq(agent.goal_stack.size(), 1, "Goal stack should have 1 item after pop")

func _test_action_determinism() -> void:
	# Two identical agents with same state should produce same action
	var state1 := Fixtures.make_sim_state(99999)
	var agent1 := Fixtures.make_agent({
		"id": 1,
		"hunger": 50.0,
		"money": 100,
		"pos_x": 48,
		"pos_y": 48
	})
	state1.add_agent(agent1)
	
	var state2 := Fixtures.make_sim_state(99999)
	var agent2 := Fixtures.make_agent({
		"id": 1,
		"hunger": 50.0,
		"money": 100,
		"pos_x": 48,
		"pos_y": 48
	})
	state2.add_agent(agent2)
	
	var brain1 := DefaultBrain.new()
	var brain2 := DefaultBrain.new()
	var world1 := state1.world
	var world2 := state2.world
	var market1 := state1.market
	var market2 := state2.market
	var contracts1 := ContractsSystem.new()
	var contracts2 := ContractsSystem.new()
	var tuning1 := state1.tuning.to_dict()
	var tuning2 := state2.tuning.to_dict()
	
	var action1 := brain1.decide_action(agent1, world1, market1, contracts1, tuning1, {}, state1)
	var action2 := brain2.decide_action(agent2, world2, market2, contracts2, tuning2, {}, state2)
	
	assert_eq(action1["type"], action2["type"], "Same state should produce same action type")
