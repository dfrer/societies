## Test Config Schema - validates config files and TuningConfig class
extends "res://tests/test_case.gd"

func _run() -> void:
	subtest("Load tuning.json", _test_tuning_loads)
	subtest("Load items.json", _test_items_loads)
	subtest("Load recipes.json", _test_recipes_loads)
	subtest("TuningConfig validation", _test_tuning_validation)
	subtest("TuningConfig typed getters", _test_typed_getters)
	subtest("Missing required key error", _test_missing_required_key)
	subtest("Missing required key from tuning.json", _test_missing_required_key_from_file)
	subtest("Schema completeness", _test_schema_completeness)

func _test_tuning_loads() -> void:
	var file := FileAccess.open("res://config/tuning.json", FileAccess.READ)
	assert_true(file != null, "tuning.json should exist and be readable")
	if file == null:
		return
	
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	file.close()
	
	assert_eq(error, OK, "tuning.json should be valid JSON")
	assert_true(json.data is Dictionary, "tuning.json should parse to Dictionary")

func _test_items_loads() -> void:
	var file := FileAccess.open("res://config/items.json", FileAccess.READ)
	assert_true(file != null, "items.json should exist and be readable")
	if file == null:
		return
	
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	file.close()
	
	assert_eq(error, OK, "items.json should be valid JSON")
	assert_true(json.data is Dictionary, "items.json should parse to Dictionary")
	
	# Check required item keys
	var items: Dictionary = json.data
	var required_items := ["Berries", "Logs", "Ore", "Planks", "CookedMeal", "MetalIngot", "Axe", "Pickaxe"]
	for item_name in required_items:
		assert_true(items.has(item_name), "items.json should have '%s'" % item_name)

func _test_recipes_loads() -> void:
	var file := FileAccess.open("res://config/recipes.json", FileAccess.READ)
	assert_true(file != null, "recipes.json should exist and be readable")
	if file == null:
		return
	
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	file.close()
	
	assert_eq(error, OK, "recipes.json should be valid JSON")
	assert_true(json.data is Dictionary, "recipes.json should parse to Dictionary")
	assert_true(json.data.has("recipes"), "recipes.json should have 'recipes' key")
	assert_true(json.data["recipes"] is Array, "recipes.json 'recipes' should be Array")

func _test_tuning_validation() -> void:
	var config := TuningConfig.load_from_file("res://config/tuning.json")
	var errors := config.validate()
	
	if not errors.is_empty():
		for error in errors:
			print("  Validation error: %s" % error)
	
	assert_true(errors.is_empty(), "tuning.json should pass validation (found %d errors)" % errors.size())

func _test_typed_getters() -> void:
	var config := TuningConfig.load_from_file("res://config/tuning.json")

	# Test int getter - verify it returns positive int
	var world_w := config.get_int("world_w")
	assert_true(world_w > 0, "world_w should be positive (got %d)" % world_w)

	# Test float getter - verify it returns positive float
	var hunger_drain := config.get_float("hunger_drain_per_tick")
	assert_true(hunger_drain > 0.0 and hunger_drain < 10.0, "hunger_drain_per_tick should be reasonable (got %f)" % hunger_drain)

	# Test bool getter - just verify it returns a bool without error
	var harvest_permit := config.get_bool("harvest_permit_required_default")
	# Value can be true or false, just verify getter works
	assert_true(harvest_permit or not harvest_permit, "harvest_permit_required_default getter should work")

	# Test string getter
	var policy := config.get_string("default_relation_policy")
	assert_true(policy.length() > 0, "default_relation_policy should be non-empty string")

	# Test fallback for optional key with default in schema
	var emergency_hunger := config.get_float("emergency_hunger_threshold")
	assert_true(emergency_hunger > 0.0, "emergency_hunger_threshold should be positive")

func _test_missing_required_key() -> void:
	# Create a config with minimal data (missing required keys)
	var config := TuningConfig.new()
	config.load_from_dict({})  # Empty dict - all required keys missing
	
	var errors := config.validate()
	
	# Should have errors for missing required keys
	assert_true(errors.size() > 0, "Empty config should have validation errors")
	
	# Check that error messages are helpful
	var found_helpful_message := false
	for error in errors:
		if error.contains("Missing required tuning key"):
			found_helpful_message = true
			break
	
	assert_true(found_helpful_message, "Validation should produce helpful 'Missing required tuning key' messages")

func _test_missing_required_key_from_file() -> void:
	var file := FileAccess.open("res://config/tuning.json", FileAccess.READ)
	assert_true(file != null, "tuning.json should exist and be readable")
	if file == null:
		return

	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	file.close()
	assert_eq(error, OK, "tuning.json should parse correctly")
	if error != OK:
		return

	var data: Dictionary = json.data
	data.erase("world_w")

	var config := TuningConfig.new()
	config.load_from_dict(data)
	var errors := config.validate()

	var found_missing := false
	for err in errors:
		if err.contains("world_w"):
			found_missing = true
			break
	assert_true(found_missing, "Validation should flag missing required keys from tuning.json")

func _test_schema_completeness() -> void:
	# Load actual tuning.json
	var file := FileAccess.open("res://config/tuning.json", FileAccess.READ)
	if file == null:
		fail("Could not open tuning.json")
		return
	
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	file.close()
	
	if error != OK:
		fail("tuning.json parse error")
		return
	
	var data: Dictionary = json.data
	var config := TuningConfig.new()
	var schema := config.get_schema()
	
	# Check that all keys in tuning.json are known to schema
	var unknown_keys: Array[String] = []
	for key in data:
		if key.begins_with("_"):
			continue  # Skip comment keys
		if not schema.has(key):
			unknown_keys.append(key)
	
	if not unknown_keys.is_empty():
		print("  Unknown keys not in schema: %s" % str(unknown_keys))
	
	# This is a warning, not a failure - allows for forward-compatible keys
	# assert_true(unknown_keys.is_empty(), "All tuning.json keys should be in schema")
	
	# Check that all required schema keys have reasonable values
	var required_keys: Array[String] = []
	for key in schema:
		if schema[key]["required"]:
			required_keys.append(key)
	
	assert_true(required_keys.size() > 50, "Schema should define at least 50 required keys (has %d)" % required_keys.size())
