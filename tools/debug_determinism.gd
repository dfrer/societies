extends SceneTree

const Serializers = preload("res://sim/serializers.gd")

var output_lines := []

func _log(msg: String) -> void:
	print(msg)
	output_lines.append(msg)

func _init() -> void:
	var seed_val = 123
	var ticks = 10
	
	_log("--- Debugging Determinism ---")
	_log("Seed: %d, Ticks: %d" % [seed_val, ticks])
	
	var sim1 = Sim.new()
	sim1.init_new(seed_val)
	
	var sim2 = Sim.new()
	sim2.init_new(seed_val)
	
	for t in range(1, ticks + 1):
		sim1.step(1)
		sim2.step(1)
		
		var trace1 = sim1.state.decision_traces
		var trace2 = sim2.state.decision_traces
		
		_log("Tick %d: Sim1 traces: %d, Sim2 traces: %d" % [t, trace1.size(), trace2.size()])
		
		var diverged = false
		var max_i = maxi(trace1.size(), trace2.size())
		for i in range(max_i):
			var a1 = trace1[i] if i < trace1.size() else "MISSING"
			var a2 = trace2[i] if i < trace2.size() else "MISSING"
			
			if JSON.stringify(a1) != JSON.stringify(a2):
				_log("Divergence at tick %d, action index %d" % [t, i])
				_log("Sim1: %s" % JSON.stringify(a1))
				_log("Sim2: %s" % JSON.stringify(a2))
				diverged = true
				break
		
		if diverged:
			break
			
		if sim1.checksum() != sim2.checksum():
			_log("Divergence at tick %d: Checksums differ, but traces match!" % t)
			_compare_states(sim1.state.to_dict(), sim2.state.to_dict())
			break
			
		_log("Tick %d matches." % t)
	
	var f = FileAccess.open("divergence.txt", FileAccess.WRITE)
	for line in output_lines:
		f.store_line(line)
	f.close()
	
	quit(0)

func _compare_states(s1: Dictionary, s2: Dictionary, path: String = "") -> void:
	var keys1 := s1.keys()
	keys1.sort()
	var keys2 := s2.keys()
	keys2.sort()
	
	for k in keys1:
		if not s2.has(k):
			_log("Sim2 missing key: %s%s" % [path, k])
			continue
			
		var v1 = s1[k]
		var v2 = s2[k]
		
		if typeof(v1) != typeof(v2):
			_log("Type mismatch at %s%s: %d vs %d" % [path, k, typeof(v1), typeof(v2)])
			continue
			
		if v1 is Dictionary:
			_compare_states(v1, v2, path + k + ".")
		elif v1 is Array:
			if v1.size() != v2.size():
				_log("Array size mismatch at %s%s: %d vs %d" % [path, k, v1.size(), v2.size()])
			else:
				for i in range(v1.size()):
					if v1[i] is Dictionary:
						_compare_states(v1[i], v2[i], path + k + "[" + str(i) + "].")
					elif JSON.stringify(v1[i]) != JSON.stringify(v2[i]):
						_log("Array element mismatch at %s%s[%d]: %s vs %s" % [path, k, i, JSON.stringify(v1[i]), JSON.stringify(v2[i])])
		else:
			if JSON.stringify(v1) != JSON.stringify(v2):
				_log("Value mismatch at %s%s: %s vs %s" % [path, k, JSON.stringify(v1), JSON.stringify(v2)])
	
	for k in keys2:
		if not s1.has(k):
			_log("Sim1 missing key: %s%s" % [path, k])

func _compare_traces(t1: Array, t2: Array) -> void:
	var size = mini(t1.size(), t2.size())
	for i in range(size):
		if JSON.stringify(t1[i]) != JSON.stringify(t2[i]):
			print("Action %d differs:" % i)
			print("  Sim1: %s" % JSON.stringify(t1[i]))
			print("  Sim2: %s" % JSON.stringify(t2[i]))
			return
	
	if t1.size() > t2.size():
		print("Sim1 has extra action: %s" % JSON.stringify(t1[size]))
	else:
		print("Sim2 has extra action: %s" % JSON.stringify(t2[size]))
