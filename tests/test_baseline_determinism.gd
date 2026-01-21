extends SceneTree

 

# Re-implementing correctly below
func run_test_same_seed() -> bool:
	print("TEST: same_seed_same_result")
	var path1 = "test_run_1.json"
	var path2 = "test_run_2.json"
	
	_run_baseline_file(12345, path1)
	_run_baseline_file(12345, path2)
	
	var data1 = _read_json(path1)
	var data2 = _read_json(path2)
	
	var result = true
	if data1.get("final_checksum") != data2.get("final_checksum"):
		print("  FAIL: Checksums differ: %s vs %s" % [data1.get("final_checksum"), data2.get("final_checksum")])
		result = false
	else:
		print("  PASS: Checksums match.")
		
	# Cleanup
	DirAccess.remove_absolute(path1)
	DirAccess.remove_absolute(path2)
	return result

func run_test_diff_seed() -> bool:
	print("TEST: diff_seed_diff_result")
	var path1 = "test_run_A.json"
	var path2 = "test_run_B.json"
	
	_run_baseline_file(11111, path1)
	_run_baseline_file(22222, path2)
	
	var data1 = _read_json(path1)
	var data2 = _read_json(path2)
	
	var result = true
	if data1.get("final_checksum") == data2.get("final_checksum"):
		print("  FAIL: Checksums matches despite different seeds: %s" % data1.get("final_checksum"))
		result = false
	else:
		print("  PASS: Checksums differ.")
		
	# Cleanup
	DirAccess.remove_absolute(path1)
	DirAccess.remove_absolute(path2)
	return result

func _run_baseline_file(seed_val: int, out_path: String) -> void:
	var godot_exe = "D:/Games (SSD)/SteamLibrary/steamapps/common/Godot Engine/godot.windows.opt.tools.64.exe"
	var args = [
		"--headless",
		"--script", "tools/baseline_runner.gd",
		"--",
		"--seed=%d" % seed_val,
		"--days=5", # Short run for speed
		"--out=%s" % out_path
	]
	var output = []
	var exit_code = OS.execute(godot_exe, args, output, true)
	if exit_code != 0:
		print("  ERROR: Baseline runner failed with code %d" % exit_code)
		print("  Output: %s" % "\n".join(output))

func _read_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		print("  ERROR: Output file not found: %s" % path)
		return {}
	var file = FileAccess.open(path, FileAccess.READ)
	var content = file.get_as_text()
	var json = JSON.parse_string(content)
	if json == null:
		return {}
	return json

func _start():
	var p1 = run_test_same_seed()
	var p2 = run_test_diff_seed()
	
	if p1 and p2:
		print("ALL DETEMINISM TESTS PASSED")
		quit(0)
	else:
		print("SOME TESTS FAILED")
		quit(1)

func _ready():
	_start()
