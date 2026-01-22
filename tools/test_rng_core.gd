extends SceneTree

const RNGClass = preload("res://sim/rng.gd")

func _init():
	var rng1 = RNGClass.new()
	rng1.init_seed(1234)
	var val1 = []
	for i in range(10):
		val1.append(rng1.randf())
	
	var rng2 = RNGClass.new()
	rng2.init_seed(1234)
	var val2 = []
	for i in range(10):
		val2.append(rng2.randf())
		
	print("RNG 1: ", val1)
	print("RNG 2: ", val2)
	
	if val1 == val2:
		print("SUCCESS: RNG is deterministic.")
	else:
		print("FAILURE: RNG is NON-DETERMINISTIC!")
		for i in range(10):
			if val1[i] != val2[i]:
				print("Mismatch at index ", i, ": ", val1[i], " vs ", val2[i])
	
	quit()
