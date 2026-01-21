## Unit tests for resource node spawning
extends "res://tests/test_case.gd"

func _run() -> void:
	subtest("Resource node counts and caps", _test_resource_node_counts)

func _test_resource_node_counts() -> void:
	var sim := Sim.new()
	sim.init_new(12345)
	var tuning: Dictionary = sim.state.tuning

	var counts := {
		"berry": 0,
		"tree": 0,
		"ore": 0,
		"stone": 0
	}

	for node in sim.state.world.resource_nodes:
		counts[node.type] += 1
		var prefix := node.type
		var max_stock_key := "%s_max_stock" % prefix
		var start_min_key := "%s_start_min" % prefix
		var start_max_key := "%s_start_max" % prefix
		var max_stock: int = int(tuning.get(max_stock_key, node.max_stock))
		var start_min: int = int(tuning.get(start_min_key, max_stock))
		var start_max: int = int(tuning.get(start_max_key, max_stock))
		if start_min > start_max:
			var temp := start_min
			start_min = start_max
			start_max = temp

		assert_eq(node.max_stock, max_stock, "Node max_stock should match tuning for %s" % node.type)
		assert_between(node.stock, start_min, start_max, "Node stock should be within tuning range for %s" % node.type)

	assert_eq(counts["berry"], int(tuning.get("berry_nodes_count", 0)), "Berry node count should match tuning")
	assert_eq(counts["tree"], int(tuning.get("tree_nodes_count", 0)), "Tree node count should match tuning")
	assert_eq(counts["ore"], int(tuning.get("ore_nodes_count", 0)), "Ore node count should match tuning")
	assert_eq(counts["stone"], int(tuning.get("stone_nodes_count", 0)), "Stone node count should match tuning")
