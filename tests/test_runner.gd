extends SceneTree

## To run tests from command line:
## & "D:\Games (SSD)\SteamLibrary\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe" --headless --script tests/test_runner.gd 2>&1

const TestCaseScript = preload("res://tests/test_case.gd")

## Root directories to scan for tests
const TEST_ROOTS := ["res://tests"]

## Files to exclude from test discovery
const EXCLUDE_FILES := [
	"test_runner.gd", 
	"test_case.gd", 
	"test_utils.gd", 
	"test_fixtures.gd",
	"test_hello.gd", 
	"test_baseline_determinism.gd",
	"sim_fixture.gd"
]

const DEFAULT_OUTPUT_PATH := "user://test_output.txt"
const REQUIRED_CONFIG_FILES := [
	"res://config/tuning.json",
	"res://config/items.json",
	"res://config/recipes.json"
]

func _init() -> void:
	var output_path := _get_output_path()
	var output_file := FileAccess.open(output_path, FileAccess.WRITE)
	if not output_file:
		print("Failed to open output file")
		quit(1)
		return

	var log_msg := func(msg: String):
		print(msg)
		output_file.store_line(msg)
	
	log_msg.call("=== Societies Simulation Test Suite ===")
	log_msg.call("")
	log_msg.call("Output file: %s" % output_path)
	log_msg.call("")

	if not _validate_required_files(log_msg):
		output_file.close()
		quit(1)
		return
	
	var passed_count := 0
	var failed_count := 0
	var total_count := 0
	
	var test_files: Array[String] = []
	
	# Discover tests in all root directories
	for root_dir in TEST_ROOTS:
		var discovered := _discover_tests_recursive(root_dir)
		test_files.append_array(discovered)
	
	# Sort for deterministic ordering
	test_files.sort()
	
	log_msg.call("Discovered %d test files" % test_files.size())
	log_msg.call("")
	
	var tests_to_run := test_files.size()
	if tests_to_run == 0:
		log_msg.call("No tests discovered. Check discovery roots and exclusions.")
		output_file.close()
		quit(1)
		return
	
	for i in range(tests_to_run):
		var path: String = test_files[i]
		var display_name := path.replace("res://tests/", "")
		log_msg.call("[TEST %d/%d] %s" % [i + 1, tests_to_run, display_name])
		
		var script = load(path)
		if not script:
			log_msg.call("  FAILED to load script")
			failed_count += 1
			total_count += 1
			continue
			
		var instance = script.new()
		if not instance:
			log_msg.call("  FAILED to instantiate script")
			failed_count += 1
			total_count += 1
			continue
			
		if not instance.has_method("run"):
			log_msg.call("  SKIPPED (no run() method)")
			instance = null  # Explicitly release
			continue
			
		total_count += 1
		
		var success := false
		# Check if it's a newer TestCase version by method existence
		if instance.has_method("get_failures"):
			success = instance.run()
			if success:
				log_msg.call("  Result: PASSED")
				passed_count += 1
			else:
				log_msg.call("  Result: FAILED")
				failed_count += 1
				var fails = instance.get_failures()
				for fail_msg in fails:
					log_msg.call("    - %s" % fail_msg)
		else:
			# Legacy/Simple bool returner
			success = instance.run()
			if success:
				log_msg.call("  Result: PASSED")
				passed_count += 1
			else:
				log_msg.call("  Result: FAILED")
				if "error_message" in instance:
					log_msg.call("    - %s" % instance.error_message)
				failed_count += 1
		
		# CRITICAL: Explicit cleanup to prevent memory accumulation
		instance = null
		script = null
				
		log_msg.call("")

	log_msg.call("=== Test Summary ===")
	log_msg.call("Tests run: %d | Passed: %d | Failed: %d" % [total_count, passed_count, failed_count])
	log_msg.call("")
	
	output_file.close()
	
	if failed_count == 0 and total_count > 0:
		log_msg.call("ALL TESTS PASSED")
		quit(0)
	else:
		log_msg.call("SOME TESTS FAILED")
		quit(1)

## Discover test files in a directory tree
func _discover_tests_recursive(dir_path: String) -> Array[String]:
	var result: Array[String] = []
	
	var dir := DirAccess.open(dir_path)
	if not dir:
		return result
	
	dir.list_dir_begin()
	var file_name := dir.get_next()
	while file_name != "":
		if dir.current_is_dir():
			if file_name != "." and file_name != "..":
				result.append_array(_discover_tests_recursive(dir_path + "/" + file_name))
		else:
			if file_name.begins_with("test_") and file_name.ends_with(".gd"):
				if file_name not in EXCLUDE_FILES:
					result.append(dir_path + "/" + file_name)
		file_name = dir.get_next()
	
	return result

func _get_output_path() -> String:
	var output_path := DEFAULT_OUTPUT_PATH
	for arg in OS.get_cmdline_args():
		if arg.begins_with("--out="):
			output_path = arg.get_slice("=", 1)
	return output_path

func _validate_required_files(log_msg: Callable) -> bool:
	var missing: Array[String] = []
	for path in REQUIRED_CONFIG_FILES:
		if not FileAccess.file_exists(path):
			missing.append(path)
	if missing.is_empty():
		return true
	log_msg.call("Missing required config files:")
	for path in missing:
		log_msg.call("  - %s" % path)
	log_msg.call("Run with the correct project path (e.g., godot --path /path/to/project).")
	return false
