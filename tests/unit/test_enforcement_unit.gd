## Unit tests for the Enforcement class
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Is Detected", _test_is_detected)
	subtest("Record Violation", _test_record_violation)
	subtest("Get Recent Violation Count", _test_get_recent_count)
	subtest("Apply Fine Routing to Agent", _test_apply_fine_to_agent)
	subtest("Apply Fine Routing to Faction", _test_apply_fine_to_faction)
	subtest("Apply Fine Respects Available Money", _test_apply_fine_respects_available)
	subtest("Market Ban Application", _test_market_ban)
	subtest("Process Illegal Harvest", _test_process_illegal_harvest)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)

func _test_is_detected() -> void:
	var enforcement := Fixtures.make_enforcement()
	var rng := Fixtures.make_rng(12345)
	
	# With 100% detect chance, should always detect
	var detected_count := 0
	for i in range(10):
		if enforcement.is_detected(rng, 1.0):
			detected_count += 1
	assert_eq(detected_count, 10, "Should always detect with 100% chance")
	
	# With 0% detect chance, should never detect
	detected_count = 0
	for i in range(10):
		if enforcement.is_detected(rng, 0.0):
			detected_count += 1
	assert_eq(detected_count, 0, "Should never detect with 0% chance")
	
	# With 50% chance, should detect some but not all (probabilistic)
	# Just verify it doesn't crash
	enforcement.is_detected(rng, 0.5)

func _test_record_violation() -> void:
	var enforcement := Fixtures.make_enforcement()
	
	# Record a violation
	enforcement.record_violation(1, Enforcement.VIOLATION_ILLEGAL_HARVEST, 100)
	
	assert_eq(enforcement.violations_log.size(), 1, "Should have 1 violation")
	assert_eq(enforcement.violations_log[0]["agent_id"], 1, "Agent ID should match")
	assert_eq(enforcement.violations_log[0]["type"], Enforcement.VIOLATION_ILLEGAL_HARVEST, "Type should match")
	assert_eq(enforcement.violations_log[0]["tick"], 100, "Tick should match")
	assert_eq(enforcement.violations_detected, 1, "violations_detected should increment")

func _test_get_recent_count() -> void:
	var enforcement := Fixtures.make_enforcement()
	
	# Record violations at different ticks
	enforcement.record_violation(1, Enforcement.VIOLATION_ILLEGAL_HARVEST, 50)
	enforcement.record_violation(1, Enforcement.VIOLATION_ILLEGAL_BUILD, 75)
	enforcement.record_violation(1, Enforcement.VIOLATION_ILLEGAL_HARVEST, 90)
	enforcement.record_violation(2, Enforcement.VIOLATION_ILLEGAL_HARVEST, 80)  # Different agent
	
	# Count for agent 1 within window (tick 60 to 100 = 40 ticks window)
	var count := enforcement.get_recent_violation_count(1, 100, 40)
	assert_eq(count, 2, "Should have 2 recent violations for agent 1")
	
	# Count for agent 2
	var count2 := enforcement.get_recent_violation_count(2, 100, 40)
	assert_eq(count2, 1, "Should have 1 recent violation for agent 2")
	
	# Count with smaller window (only tick 85-100)
	var count3 := enforcement.get_recent_violation_count(1, 100, 15)
	assert_eq(count3, 1, "Should have 1 violation in narrow window")

func _test_apply_fine_to_agent() -> void:
	var enforcement := Fixtures.make_enforcement()
	var state := Fixtures.make_sim_state()
	
	# Create violator and tile owner
	var violator := Fixtures.make_agent({"id": 1, "money": 100})
	var owner := Fixtures.make_agent({"id": 2, "money": 0})
	state.agents = [violator, owner]
	state.add_agent(violator)
	state.add_agent(owner)
	
	# Apply fine routed to agent owner
	var paid := enforcement.apply_fine_with_routing(violator, 30, {}, 2, state)
	
	assert_eq(paid, 30, "Should pay full fine")
	assert_eq(violator.money, 70, "Violator should have 70 left")
	assert_eq(owner.money, 30, "Owner should receive fine")
	assert_eq(enforcement.fines_collected, 30, "fines_collected should be 30")

func _test_apply_fine_to_faction() -> void:
	var enforcement := Fixtures.make_enforcement()
	var state := Fixtures.make_sim_state()
	
	# Create violator and faction
	var violator := Fixtures.make_agent({"id": 1, "money": 100})
	var faction := Fixtures.make_faction(1, {"treasury": 0})
	state.agents = [violator]
	state.add_agent(violator)
	state.factions = [faction]
	
	# Faction owner ID = World.owner_id_for_faction(1) = some negative number
	var faction_owner_id := World.owner_id_for_faction(1)
	
	# Apply fine routed to faction
	var paid := enforcement.apply_fine_with_routing(violator, 25, {}, faction_owner_id, state)
	
	assert_eq(paid, 25, "Should pay full fine")
	assert_eq(faction.treasury, 25, "Faction treasury should receive fine")

func _test_apply_fine_respects_available() -> void:
	var enforcement := Fixtures.make_enforcement()
	var state := Fixtures.make_sim_state()
	
	# Agent with locked money
	var violator := Fixtures.make_agent({"id": 1, "money": 100})
	violator.reserve_money(60)  # Only 40 available
	state.agents = [violator]
	state.add_agent(violator)
	
	# Try to fine 50 (more than available)
	var paid := enforcement.apply_fine_with_routing(violator, 50, {}, 0, state)
	
	# Should only take from available, not locked
	assert_eq(paid, 40, "Should only pay from available money (40)")
	assert_eq(violator.locked_money, 60, "Locked money should be untouched")
	assert_eq(violator.get_available_money(), 0, "Available should be 0")

func _test_market_ban() -> void:
	var enforcement := Fixtures.make_enforcement()
	var agent := Fixtures.make_agent({"id": 1})
	
	# Initially not banned
	assert_false(enforcement.is_market_banned(agent, 100), "Should not be banned initially")
	
	# Apply ban for 50 ticks starting at tick 100
	enforcement.apply_market_ban(agent, 100, 50)
	
	# Check ban status
	assert_true(enforcement.is_market_banned(agent, 100), "Should be banned at tick 100")
	assert_true(enforcement.is_market_banned(agent, 149), "Should be banned at tick 149")
	assert_false(enforcement.is_market_banned(agent, 150), "Should not be banned at tick 150")
	assert_false(enforcement.is_market_banned(agent, 200), "Should not be banned at tick 200")

func _test_process_illegal_harvest() -> void:
	var enforcement := Fixtures.make_enforcement()
	var state := Fixtures.make_sim_state()
	var rng := Fixtures.make_rng(12345)
	
	# Create agent and laws
	var agent := Fixtures.make_agent({"id": 1, "money": 100})
	var laws := Fixtures.make_laws({"harvest_permit_required": true})
	state.agents = [agent]
	state.add_agent(agent)
	
	# Without logging in as having permit
	# Use detect chance of 1.0 to ensure detection
	var tuning := {"detect_chance": 1.0, "fine_base": 20}
	
	var result := enforcement.process_illegal_harvest_with_state(
		agent, 2, laws, rng, tuning, {}, 100, state
	)
	
	# Should not be allowed due to missing permit and detection
	assert_false(result["allowed"], "Should not be allowed without permit when detected")
	assert_eq(result["reason_code"], EnforcementResult.FINED, "Reason should be FINED")

func _test_serialization_roundtrip() -> void:
	var enforcement := Fixtures.make_enforcement()
	
	# Add some state
	enforcement.record_violation(1, Enforcement.VIOLATION_ILLEGAL_HARVEST, 50)
	enforcement.record_violation(2, Enforcement.VIOLATION_ILLEGAL_BUILD, 75)
	enforcement.fines_collected = 100
	
	# Serialize
	var dict := enforcement.to_dict()
	
	# Deserialize
	var restored := Enforcement.from_dict(dict)
	
	# Verify
	assert_eq(restored.violations_log.size(), 2, "Should have 2 violations")
	assert_eq(restored.violations_detected, 2, "violations_detected should match")
	assert_eq(restored.fines_collected, 100, "fines_collected should match")
