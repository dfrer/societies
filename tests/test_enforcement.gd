extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")

func _run() -> void:
	# Simplified test - just run a few ticks and check determinism
	subtest("Enforcement Determinism Only", func():
		var sim1 := SimFixture.make_sim(321)
		sim1.step(10)  # Very minimal
		var sim2 := SimFixture.make_sim(321)
		sim2.step(10)
		assert_eq(sim1.checksum(), sim2.checksum(), "Determinism match")
	)
