## Integration tests for save/load functionality
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

const TEST_SAVE_PATH := "user://test_save_temp.json"

func _run() -> void:
	subtest("Full State Roundtrip", _test_full_state_roundtrip)
	subtest("Resume From Saved State", _test_resume_from_saved)
	subtest("Save Load Preserves Agents", _test_preserves_agents)
	subtest("Save Load Preserves Market", _test_preserves_market)
	subtest("Save Load Preserves Contracts", _test_preserves_contracts)
	subtest("Save Load Preserves Factions", _test_preserves_factions)
	_cleanup()

func _cleanup() -> void:
	# Clean up test file
	if FileAccess.file_exists(TEST_SAVE_PATH):
		DirAccess.remove_absolute(TEST_SAVE_PATH)

func _test_full_state_roundtrip() -> void:
	# Create and run simulation
	var sim := Fixtures.make_sim(77777)
	sim.step(30)
	
	var checksum_before := sim.checksum()
	
	# Save
	var save_success := sim.save_json(TEST_SAVE_PATH)
	assert_true(save_success, "Save should succeed")
	
	# Load into new sim
	var sim2 := Sim.new()
	var load_success := sim2.load_json(TEST_SAVE_PATH)
	assert_true(load_success, "Load should succeed")
	
	var checksum_after := sim2.checksum()
	
	# Checksums should match
	assert_eq(checksum_before, checksum_after, "Checksum should match after save/load roundtrip")

func _test_resume_from_saved() -> void:
	# Run simulation, save at tick 50, continue to 100
	var sim1 := Fixtures.make_sim(88888)
	sim1.step(50)
	
	sim1.save_json(TEST_SAVE_PATH)
	
	# Continue sim1 for 50 more ticks
	sim1.step(50)
	var expected_checksum := sim1.checksum()
	
	# Load saved state and continue
	var sim2 := Sim.new()
	sim2.load_json(TEST_SAVE_PATH)
	sim2.step(50)
	var resumed_checksum := sim2.checksum()
	
	# Should match
	assert_eq(expected_checksum, resumed_checksum, 
		"Simulation resumed from save should match continuous run")

func _test_preserves_agents() -> void:
	var sim := Fixtures.make_sim(33333)
	sim.step(20)
	
	# Record agent data
	var agent_count_before := sim.state.agents.size()
	var first_agent_money: int = sim.state.agents[0].money if agent_count_before > 0 else 0
	var first_agent_hunger: float = sim.state.agents[0].get_hunger() if agent_count_before > 0 else 0.0
	
	# Save and load
	sim.save_json(TEST_SAVE_PATH)
	var sim2 := Sim.new()
	sim2.load_json(TEST_SAVE_PATH)
	
	# Verify agents
	assert_eq(sim2.state.agents.size(), agent_count_before, "Agent count should match")
	if sim2.state.agents.size() > 0:
		assert_eq(sim2.state.agents[0].money, first_agent_money, "First agent money should match")
		assert_approx(sim2.state.agents[0].get_hunger(), first_agent_hunger, 0.01, 
			"First agent hunger should match")

func _test_preserves_market() -> void:
	var sim := Fixtures.make_sim(44444)
	sim.step(30)
	
	# Record market data
	var total_trades_before := sim.state.market.total_trades
	var berries_ref_before := sim.state.market.get_ref_price("Berries")
	
	# Save and load
	sim.save_json(TEST_SAVE_PATH)
	var sim2 := Sim.new()
	sim2.load_json(TEST_SAVE_PATH)
	
	# Verify market
	assert_eq(sim2.state.market.total_trades, total_trades_before, "Total trades should match")
	assert_approx(sim2.state.market.get_ref_price("Berries"), berries_ref_before, 0.01,
		"Berries ref price should match")

func _test_preserves_contracts() -> void:
	var sim := Fixtures.make_sim(55555)
	sim.step(40)  # Enough time for contracts to generate
	
	# Record contracts data
	var contract_count_before := sim.state.contracts_system.contracts.size()
	var stats_before := sim.state.contracts_system.stats.duplicate()
	
	# Save and load
	sim.save_json(TEST_SAVE_PATH)
	var sim2 := Sim.new()
	sim2.load_json(TEST_SAVE_PATH)
	
	# Verify contracts
	assert_eq(sim2.state.contracts_system.contracts.size(), contract_count_before, 
		"Contract count should match")
	assert_eq(sim2.state.contracts_system.stats["posted"], stats_before["posted"],
		"Posted stat should match")

func _test_preserves_factions() -> void:
	var sim := Fixtures.make_sim(66666)
	sim.step(100)  # Enough time for factions to potentially form
	
	# Record factions data
	var faction_count_before := sim.state.factions.size()
	var faction_names := []
	for f in sim.state.factions:
		faction_names.append(f.name)
	
	# Save and load
	sim.save_json(TEST_SAVE_PATH)
	var sim2 := Sim.new()
	sim2.load_json(TEST_SAVE_PATH)
	
	# Verify factions
	assert_eq(sim2.state.factions.size(), faction_count_before, "Faction count should match")
	for i in range(mini(faction_count_before, sim2.state.factions.size())):
		assert_eq(sim2.state.factions[i].name, faction_names[i], 
			"Faction %d name should match" % i)
