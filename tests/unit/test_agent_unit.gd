## Unit tests for the Agent class
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Inventory Add and Remove", _test_inventory_add_remove)
	subtest("Inventory Locking", _test_inventory_locking)
	subtest("Inventory Release Locked", _test_inventory_release_locked)
	subtest("Inventory Consume Locked", _test_inventory_consume_locked)
	subtest("Money Locking", _test_money_locking)
	subtest("Money Release", _test_money_release)
	subtest("Get Available Money", _test_get_available_money)
	subtest("Hunger Clamping", _test_hunger_clamping)
	subtest("Is Alive", _test_is_alive)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)
	subtest("Position Helpers", _test_position_helpers)
	subtest("Skill System", _test_skill_system)

func _test_inventory_add_remove() -> void:
	var agent := Fixtures.make_agent()
	
	# Add items
	agent.add_item("Berries", 10)
	assert_eq(agent.get_item_count("Berries"), 10, "Should have 10 Berries after adding")
	
	# Add more
	agent.add_item("Berries", 5)
	assert_eq(agent.get_item_count("Berries"), 15, "Should have 15 Berries after adding more")
	
	# Remove items
	var removed := agent.remove_item("Berries", 7)
	assert_true(removed, "Should return true when remove succeeds")
	assert_eq(agent.get_item_count("Berries"), 8, "Should have 8 Berries after removing 7")
	
	# Remove more than available (should return false and not modify)
	var removed_too_many := agent.remove_item("Berries", 20)
	assert_false(removed_too_many, "Should return false when not enough available")
	assert_eq(agent.get_item_count("Berries"), 8, "Should still have 8 Berries (unchanged)")
	
	# Remove all that remain
	agent.remove_item("Berries", 8)
	assert_eq(agent.get_item_count("Berries"), 0, "Should have 0 Berries after removing exact amount")
	
	# Get count for non-existent item
	assert_eq(agent.get_item_count("NonexistentItem"), 0, "Non-existent item should return 0")

func _test_inventory_locking() -> void:
	var agent := Fixtures.make_agent()
	agent.add_item("Logs", 10)
	
	# Lock some items
	agent.lock_item("Logs", 3)
	assert_eq(agent.get_available_item("Logs"), 7, "Available should be total minus locked")
	assert_eq(agent.locked_inventory.get("Logs", 0), 3, "Locked should be 3")
	
	# Lock more
	agent.lock_item("Logs", 4)
	assert_eq(agent.get_available_item("Logs"), 3, "Available should be 3 after locking 7 total")
	assert_eq(agent.locked_inventory.get("Logs", 0), 7, "Locked should be 7")
	
	# Try to lock more than available (should clamp)
	agent.lock_item("Logs", 10)  # More than available
	# Available should not go negative
	assert_true(agent.get_available_item("Logs") >= 0, "Available should never be negative")

func _test_inventory_release_locked() -> void:
	var agent := Fixtures.make_agent()
	agent.add_item("Ore", 10)
	agent.lock_item("Ore", 5)
	
	# Release locked items (should not destroy inventory)
	agent.release_locked_item("Ore", 3)
	assert_eq(agent.locked_inventory.get("Ore", 0), 2, "Locked should be 2 after releasing 3")
	assert_eq(agent.get_item_count("Ore"), 10, "Total inventory should still be 10")
	assert_eq(agent.get_available_item("Ore"), 8, "Available should now be 8")

func _test_inventory_consume_locked() -> void:
	var agent := Fixtures.make_agent()
	agent.add_item("Planks", 10)
	agent.lock_item("Planks", 5)
	
	# Consume locked items (releases AND removes from inventory)
	agent.consume_locked_item("Planks", 3)
	assert_eq(agent.locked_inventory.get("Planks", 0), 2, "Locked should be 2 after consuming 3")
	assert_eq(agent.get_item_count("Planks"), 7, "Total inventory should be 7 after consuming")
	assert_eq(agent.get_available_item("Planks"), 5, "Available should be 5")

func _test_money_locking() -> void:
	var agent := Fixtures.make_agent({"money": 100})
	
	# Lock money
	agent.lock_money(30)
	assert_eq(agent.locked_money, 30, "Locked money should be 30")
	assert_eq(agent.get_available_money(), 70, "Available money should be 70")
	
	# Lock more
	agent.lock_money(20)
	assert_eq(agent.locked_money, 50, "Locked money should be 50")
	assert_eq(agent.get_available_money(), 50, "Available money should be 50")

func _test_money_release() -> void:
	var agent := Fixtures.make_agent({"money": 100})
	agent.lock_money(50)
	
	# Release some
	agent.release_locked_money(20)
	assert_eq(agent.locked_money, 30, "Locked money should be 30 after releasing 20")
	assert_eq(agent.get_available_money(), 70, "Available should be 70")

func _test_get_available_money() -> void:
	var agent := Fixtures.make_agent({"money": 100})
	
	# All available by default
	assert_eq(agent.get_available_money(), 100, "All money available initially")
	
	# Lock all
	agent.lock_money(100)
	assert_eq(agent.get_available_money(), 0, "No money available when all locked")
	
	# Lock more than total should not make available negative
	agent.lock_money(50)  # 150 locked, but only 100 total
	assert_true(agent.get_available_money() >= 0, "Available money should never be negative")

func _test_hunger_clamping() -> void:
	var agent := Fixtures.make_agent()
	
	# Set hunger normally
	agent.set_hunger(75.0)
	assert_eq(agent.get_hunger(), 75.0, "Hunger should be 75")
	
	# Clamp to max
	agent.set_hunger(150.0)
	assert_eq(agent.get_hunger(), 100.0, "Hunger should clamp to 100")
	
	# Clamp to min
	agent.set_hunger(-50.0)
	assert_eq(agent.get_hunger(), 0.0, "Hunger should clamp to 0")

func _test_is_alive() -> void:
	var agent := Fixtures.make_agent()
	
	# Alive when hunger > 0
	agent.set_hunger(50.0)
	assert_true(agent.is_alive(), "Should be alive with hunger > 0")
	
	# Dead when hunger = 0
	agent.set_hunger(0.0)
	assert_false(agent.is_alive(), "Should be dead with hunger = 0")

func _test_serialization_roundtrip() -> void:
	var agent := Fixtures.make_agent({
		"id": 42,
		"pos_x": 10,
		"pos_y": 20,
		"money": 500,
		"faction_id": 3,
		"inventory": {"Berries": 15, "Logs": 8}
	})
	agent.set_hunger(65.0)
	agent.lock_money(100)
	agent.lock_item("Berries", 5)
	agent.personality["greed"] = 0.8
	
	# Serialize
	var dict := agent.to_dict()
	
	# Deserialize
	var restored := Agent.from_dict(dict)
	
	# Verify core fields
	assert_eq(restored.id, 42, "ID should match")
	assert_eq(restored.pos_x, 10, "pos_x should match")
	assert_eq(restored.pos_y, 20, "pos_y should match")
	assert_eq(restored.money, 500, "money should match")
	assert_eq(restored.faction_id, 3, "faction_id should match")
	
	# Verify inventory
	assert_eq(restored.get_item_count("Berries"), 15, "Berries count should match")
	assert_eq(restored.get_item_count("Logs"), 8, "Logs count should match")
	
	# Verify locked state
	assert_eq(restored.locked_money, 100, "locked_money should match")
	assert_eq(restored.locked_inventory.get("Berries", 0), 5, "locked Berries should match")
	
	# Verify hunger
	assert_approx(restored.get_hunger(), 65.0, 0.01, "hunger should match")
	
	# Verify personality
	assert_approx(restored.personality["greed"], 0.8, 0.0001, "personality greed should match")

func _test_position_helpers() -> void:
	var agent := Fixtures.make_agent({"pos_x": 10, "pos_y": 20})
	
	assert_true(agent.is_at(10, 20), "Should be at (10, 20)")
	assert_false(agent.is_at(10, 21), "Should not be at (10, 21)")
	assert_false(agent.is_at(11, 20), "Should not be at (11, 20)")

func _test_skill_system() -> void:
	var agent := Fixtures.make_agent()
	
	# Initial skill level
	var initial_gather := agent.get_skill_level("gather")
	assert_true(initial_gather >= 0.0, "Skill level should be non-negative")
	
	# Add experience
	agent.add_skill_xp("gather", 10.0, {})
	var after_xp := agent.get_skill_level("gather")
	assert_true(after_xp >= initial_gather, "Skill should not decrease after XP gain")
