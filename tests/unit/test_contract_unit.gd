## Unit tests for the Contract class
extends TestCase

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Status Transitions", _test_status_transitions)
	subtest("Is Available Checks", _test_is_available)
	subtest("Is Active Checks", _test_is_active)
	subtest("Is Past Deadline Check", _test_is_past_deadline)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)

func _test_status_transitions() -> void:
	var contract := Fixtures.make_contract(1, "Berries", 10, 50)
	
	# Starts as posted
	assert_eq(contract.status, Contract.STATUS_POSTED, "Should start as POSTED")
	
	# Accept
	contract.accept(2, 10)  # worker_id=2, accept_tick=10
	assert_eq(contract.status, Contract.STATUS_ACCEPTED, "Should be ACCEPTED after accept()")
	assert_eq(contract.worker_id, 2, "Worker ID should be set")
	
	# Complete
	contract.complete()
	assert_eq(contract.status, Contract.STATUS_COMPLETED, "Should be COMPLETED after complete()")

func _test_is_available() -> void:
	var contract := Fixtures.make_contract(1, "Logs", 5, 30, 100)
	
	# Available before accepting
	assert_true(contract.is_available(), "Should be available when new")
	
	# Not available after accepting
	contract.accept(2, 10)
	assert_false(contract.is_available(), "Should not be available after accepting")
	
	# Create another to test posted state
	var contract2 := Fixtures.make_contract(1, "Logs", 5, 30, 50)
	assert_true(contract2.is_available(), "Newly created should be available")

func _test_is_active() -> void:
	var contract := Fixtures.make_contract(1, "Ore", 3, 45)
	
	# Posted is active
	assert_true(contract.is_active(), "POSTED should be active")
	
	# Accepted is active
	contract.accept(2, 10)
	assert_true(contract.is_active(), "ACCEPTED should be active")
	
	# Completed is not active
	contract.complete()
	assert_false(contract.is_active(), "COMPLETED should not be active")
	
	# Expired is not active
	var contract2 := Fixtures.make_contract(1, "Ore", 3, 45)
	contract2.fail_or_expire(true)  # expire
	assert_false(contract2.is_active(), "EXPIRED should not be active")
	
	# Failed is not active
	var contract3 := Fixtures.make_contract(1, "Ore", 3, 45)
	contract3.accept(2, 10)
	contract3.fail_or_expire(false)  # fail
	assert_false(contract3.is_active(), "FAILED should not be active")

func _test_is_past_deadline() -> void:
	var contract := Fixtures.make_contract(1, "Planks", 2, 40, 100)  # deadline = tick 100
	
	# Not past deadline before deadline
	assert_false(contract.is_past_deadline(50), "Should not be past deadline at tick 50")
	assert_false(contract.is_past_deadline(99), "Should not be past deadline at tick 99")
	assert_false(contract.is_past_deadline(100), "Should not be past deadline at tick 100")
	
	# Past deadline after deadline
	assert_true(contract.is_past_deadline(101), "Should be past deadline at tick 101")
	assert_true(contract.is_past_deadline(150), "Should be past deadline at tick 150")

func _test_serialization_roundtrip() -> void:
	var contract := Fixtures.make_contract(5, "CookedMeal", 8, 120, 200, {
		"id": 42,
		"delivery_pos_x": 30,
		"delivery_pos_y": 40,
		"created_tick": 50
	})
	contract.accept(7, 60)  # Accept at tick 60
	
	# Serialize
	var dict := contract.to_dict()
	
	# Deserialize
	var restored := Contract.from_dict(dict)
	
	# Verify all fields
	assert_eq(restored.id, 42, "ID should match")
	assert_eq(restored.issuer_id, 5, "issuer_id should match")
	assert_eq(restored.worker_id, 7, "worker_id should match")
	assert_eq(restored.item, "CookedMeal", "item should match")
	assert_eq(restored.qty, 8, "qty should match")
	assert_eq(restored.payout, 120, "payout should match")
	assert_eq(restored.deadline_tick, 200, "deadline_tick should match")
	assert_eq(restored.delivery_pos_x, 30, "delivery_pos_x should match")
	assert_eq(restored.delivery_pos_y, 40, "delivery_pos_y should match")
	assert_eq(restored.status, Contract.STATUS_ACCEPTED, "status should match")
	assert_eq(restored.created_tick, 50, "created_tick should match")
