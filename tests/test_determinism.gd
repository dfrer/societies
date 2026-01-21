extends "res://tests/test_case.gd"
const SimFixture = preload("res://tests/sim_fixture.gd")

func _run() -> void:
	# Simplified determinism test - only 2 sims, minimal ticks
	subtest("Same seed identical", func():
		var sim1 := SimFixture.make_sim(123)
		sim1.step(5)
		var c1 := sim1.checksum()
		var sim2 := SimFixture.make_sim(123)
		sim2.step(5)
		var c2 := sim2.checksum()
		assert_eq(c1, c2, "Checksums should match for same seed")
	)
