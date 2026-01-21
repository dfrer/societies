extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")

func _run() -> void:
	# Simplified test - just one determinism check to reduce memory usage
	subtest("Brain Determinism Only", func():
		var sim1 := SimFixture.make_sim(999)
		sim1.step(5)  # Very minimal
		var sim2 := SimFixture.make_sim(999)
		sim2.step(5)
		assert_eq(sim1.checksum(), sim2.checksum(), "Same seed should produce same checksum with brain")
	)
