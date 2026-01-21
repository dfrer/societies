## Unit tests for the Crafting class
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Create Job", _test_create_job)
	subtest("Can Craft", _test_can_craft)
	subtest("Consume Inputs", _test_consume_inputs)
	subtest("Give Outputs", _test_give_outputs)
	subtest("Get Active Job Count", _test_active_job_count)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)

func _test_create_job() -> void:
	var crafting := Crafting.new()
	
	# Create a mock recipe
	var recipe := Recipe.new()
	recipe.id = "planks"
	recipe.ticks = 5
	
	var job := crafting.create_job(recipe, 1, 10)
	
	assert_eq(job["recipe_id"], "planks", "recipe_id should match")
	assert_eq(job["agent_id"], 1, "agent_id should match")
	assert_eq(job["workshop_id"], 10, "workshop_id should match")
	assert_eq(job["progress"], 0, "progress should start at 0")
	assert_eq(job["total_ticks"], 5, "total_ticks should match recipe")
	
	# Job ID should increment
	var job2 := crafting.create_job(recipe, 2, 10)
	assert_true(job2["job_id"] > job["job_id"], "Job IDs should increment")

func _test_can_craft() -> void:
	var agent := Fixtures.make_agent()
	agent.add_item("Logs", 5)
	
	# Recipe requiring 3 logs
	var recipe := Recipe.new()
	recipe.inputs = {"Logs": 3}
	
	# Agent has enough
	assert_true(Crafting.can_craft(agent, recipe), "Should be able to craft with enough materials")
	
	# Recipe requiring too many logs
	var recipe2 := Recipe.new()
	recipe2.inputs = {"Logs": 10}
	
	assert_false(Crafting.can_craft(agent, recipe2), "Should not be able to craft without enough materials")
	
	# Recipe requiring items agent doesn't have
	var recipe3 := Recipe.new()
	recipe3.inputs = {"Ore": 5}
	
	assert_false(Crafting.can_craft(agent, recipe3), "Should not be able to craft with missing materials")

func _test_consume_inputs() -> void:
	var agent := Fixtures.make_agent()
	agent.add_item("Logs", 10)
	
	var recipe := Recipe.new()
	recipe.inputs = {"Logs": 3}
	
	# Consume inputs
	var success := Crafting.consume_inputs(agent, recipe)
	
	assert_true(success, "consume_inputs should succeed")
	assert_eq(agent.get_item_count("Logs"), 7, "Should have 7 logs after consuming 3")
	
	# Try to consume when not enough
	agent.inventory["Logs"] = 2
	var recipe2 := Recipe.new()
	recipe2.inputs = {"Logs": 5}
	
	var success2 := Crafting.consume_inputs(agent, recipe2)
	assert_false(success2, "consume_inputs should fail without enough materials")
	assert_eq(agent.get_item_count("Logs"), 2, "Logs should be unchanged after failed consume")

func _test_give_outputs() -> void:
	var agent := Fixtures.make_agent()
	
	var recipe := Recipe.new()
	recipe.outputs = {"Planks": 4, "Sawdust": 1}
	
	Crafting.give_outputs(agent, recipe)
	
	assert_eq(agent.get_item_count("Planks"), 4, "Should have 4 planks")
	assert_eq(agent.get_item_count("Sawdust"), 1, "Should have 1 sawdust")

func _test_active_job_count() -> void:
	# Create mock workshops
	var workshop1 := Workshop.new()
	workshop1.is_built = true
	
	var workshop2 := Workshop.new()
	workshop2.is_built = true
	
	# Add jobs
	workshop1.add_job({"job_id": 1, "recipe_id": "planks", "agent_id": 1, "progress": 0, "total_ticks": 5})
	workshop1.add_job({"job_id": 2, "recipe_id": "planks", "agent_id": 2, "progress": 0, "total_ticks": 5})
	workshop2.add_job({"job_id": 3, "recipe_id": "ingot", "agent_id": 3, "progress": 0, "total_ticks": 10})
	
	var workshops := [workshop1, workshop2]
	
	var count := Crafting.get_active_job_count(workshops)
	assert_eq(count, 3, "Should have 3 active jobs")

func _test_serialization_roundtrip() -> void:
	var crafting := Crafting.new()
	crafting.next_job_id = 10
	crafting.crafted_counts = {"Planks": 50, "MetalIngot": 20}
	
	# Serialize
	var dict := crafting.to_dict()
	
	# Deserialize
	var restored := Crafting.from_dict(dict)
	
	# Verify
	assert_eq(restored.next_job_id, 10, "next_job_id should match")
	assert_eq(restored.crafted_counts.get("Planks", 0), 50, "Planks count should match")
	assert_eq(restored.crafted_counts.get("MetalIngot", 0), 20, "MetalIngot count should match")
