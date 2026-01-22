extends TestCase

const SimFixture = preload("res://tests/sim_fixture.gd")

func _run() -> void:
	_test_rng_determinism()

func _test_rng_determinism() -> void:
	var sim1 := SimFixture.make_sim(5678)
	sim1.step(10)
	var sim2 := SimFixture.make_sim(5678)
	sim2.step(10)
	assert_eq(sim1.checksum(), sim2.checksum(), "Determinism match")