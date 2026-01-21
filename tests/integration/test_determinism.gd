## Integration tests for simulation determinism
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Same Seed Same Result", _test_same_seed_same_result)
	subtest("Different Seeds Different Results", _test_different_seeds_different_results)
	subtest("RNG State Serialization", _test_rng_state_serialization)
	subtest("Extended Determinism (100 Ticks)", _test_extended_determinism)
	subtest("Checksum Stability", _test_checksum_stability)

func _test_same_seed_same_result() -> void:
	# Run two simulations with same seed
	var sim1 := Fixtures.make_sim(12345)
	var sim2 := Fixtures.make_sim(12345)
	
	# Step both forward
	sim1.step(50)
	sim2.step(50)
	
	# Checksums must match
	var c1 := sim1.checksum()
	var c2 := sim2.checksum()
	
	assert_eq(c1, c2, "Same seed should produce identical checksum after 50 ticks")

func _test_different_seeds_different_results() -> void:
	# Run two simulations with different seeds
	var sim1 := Fixtures.make_sim(11111)
	var sim2 := Fixtures.make_sim(22222)
	
	# Step both forward
	sim1.step(30)
	sim2.step(30)
	
	# Checksums should differ
	var c1 := sim1.checksum()
	var c2 := sim2.checksum()
	
	assert_ne(c1, c2, "Different seeds should produce different checksums")

func _test_rng_state_serialization() -> void:
	# Create RNG and advance it
	var rng := Fixtures.make_rng(55555)
	for i in range(100):
		rng.randf()
	
	# Serialize RNG state
	var state_dict := rng.to_dict()
	
	# Generate some more numbers
	var expected_values := []
	for i in range(10):
		expected_values.append(rng.randf())
	
	# Create new RNG from serialized state
	var restored := RNG.from_dict(state_dict)
	
	# Generate numbers from restored RNG
	var restored_values := []
	for i in range(10):
		restored_values.append(restored.randf())
	
	# Values should match
	for i in range(10):
		assert_approx(expected_values[i], restored_values[i], 0.0001, 
			"RNG value %d should match after restore" % i)

func _test_extended_determinism() -> void:
	# Test over longer simulation
	var sim1 := Fixtures.make_sim(99999)
	var sim2 := Fixtures.make_sim(99999)
	
	# Run for 100 ticks
	sim1.step(100)
	sim2.step(100)
	
	var c1 := sim1.checksum()
	var c2 := sim2.checksum()
	
	assert_eq(c1, c2, "Extended simulation (100 ticks) should be deterministic")

func _test_checksum_stability() -> void:
	# Run simulation and compute checksum multiple times
	var sim := Fixtures.make_sim(42424)
	sim.step(25)
	
	# Checksum should be stable (same value each time called)
	var c1 := sim.checksum()
	var c2 := sim.checksum()
	var c3 := sim.checksum()
	
	assert_eq(c1, c2, "Checksum should be stable across calls")
	assert_eq(c2, c3, "Checksum should be stable across calls")
