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
const DEFAULT_JSON_OUTPUT_PATH := "user://test_results.json"
const DEFAULT_TEST_TIMEOUT_MS := 120000
const REQUIRED_CONFIG_FILES := [
	"res://config/tuning.json",
	"res://config/items.json",
	"res://config/recipes.json"
]

func _init() -> void:
	var output_path := _get_output_path()
	var filter := _get_filter()
	var json_output_path := _get_json_output_path()
	var timeout_ms := _get_timeout_ms()
	var output_file := FileAccess.open(output_path, FileAccess.WRITE)
	if not output_file:
		print("Failed to open output file")
		quit(1)
		return

	var log_msg := func(msg: String):
		print(msg)
		output_file.store_line(msg)
		output_file.flush()
	
	log_msg.call("=== Societies Simulation Test Suite ===")
	log_msg.call("")
	log_msg.call("Output file: %s" % output_path)
	log_msg.call("Per-test timeout (ms): %d" % timeout_ms)
	if not filter.is_empty():
		log_msg.call("Filter: %s" % filter)
	if not json_output_path.is_empty():
		log_msg.call("JSON output: %s" % json_output_path)
	log_msg.call("")

	if not _validate_required_files(log_msg):
		output_file.close()
		quit(1)
		return
	
	var passed_count := 0
	var failed_count := 0
	var total_count := 0
	var results: Array[Dictionary] = []
	var failures_by_file: Dictionary = {}
	var aborted_due_to_timeout := false
	
	var test_files: Array[String] = []
	
	# Discover tests in all root directories
	for root_dir in TEST_ROOTS:
		var discovered := _discover_tests_recursive(root_dir)
		test_files.append_array(discovered)
	
	# Sort for deterministic ordering
	test_files.sort()
	if not filter.is_empty():
		test_files = _filter_tests(test_files, filter)
	
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
		var start_ms := Time.get_ticks_msec()
		var status := "unknown"
		var failures: Array[String] = []
		
		log_msg.call("  Loading script: %s" % path)
		var script = load(path)
		if not script:
			log_msg.call("  FAILED to load script")
			failed_count += 1
			total_count += 1
			status = "error"
			_failures_add(failures_by_file, path)
			results.append(_result_entry(path, status, failures, start_ms))
			continue
			
		var instance = script.new()
		if not instance:
			log_msg.call("  FAILED to instantiate script")
			failed_count += 1
			total_count += 1
			status = "error"
			_failures_add(failures_by_file, path)
			results.append(_result_entry(path, status, failures, start_ms))
			continue
			
		if not instance.has_method("run"):
			log_msg.call("  SKIPPED (no run() method)")
			instance = null  # Explicitly release
			status = "skipped"
			results.append(_result_entry(path, status, failures, start_ms))
			continue
			
		total_count += 1
		
		var success := false
		# Check if it's a newer TestCase version by method existence
		var run_result := _run_with_timeout(instance, timeout_ms)
		if run_result["timed_out"]:
			log_msg.call("  Result: TIMED OUT after %d ms" % timeout_ms)
			status = "timeout"
			failures.append("Test timed out after %d ms" % timeout_ms)
			failed_count += 1
			_failures_add(failures_by_file, path)
			instance = null
			script = null
			results.append(_result_entry(path, status, failures, start_ms))
			aborted_due_to_timeout = true
			break
		success = run_result["success"]
		if instance.has_method("get_failures"):
			if success:
				log_msg.call("  Result: PASSED")
				passed_count += 1
				status = "passed"
			else:
				log_msg.call("  Result: FAILED")
				failed_count += 1
				status = "failed"
				var fails = instance.get_failures()
				for fail_msg in fails:
					log_msg.call("    - %s" % fail_msg)
					failures.append(fail_msg)
				_failures_add(failures_by_file, path)
		else:
			# Legacy/Simple bool returner
			if success:
				log_msg.call("  Result: PASSED")
				passed_count += 1
				status = "passed"
			else:
				log_msg.call("  Result: FAILED")
				status = "failed"
				if "error_message" in instance:
					var error_message: String = instance.error_message
					log_msg.call("    - %s" % error_message)
					failures.append(error_message)
				failed_count += 1
				_failures_add(failures_by_file, path)
		
		# CRITICAL: Explicit cleanup to prevent memory accumulation
		instance = null
		script = null
		
		results.append(_result_entry(path, status, failures, start_ms))
		log_msg.call("")

	log_msg.call("=== Test Summary ===")
	log_msg.call("Tests run: %d | Passed: %d | Failed: %d" % [total_count, passed_count, failed_count])
	log_msg.call("")
	if aborted_due_to_timeout:
		log_msg.call("ABORTED: Test run stopped due to timeout.")
		log_msg.call("")
	if not failures_by_file.is_empty():
		log_msg.call("=== Failure Summary ===")
		for failed_path in failures_by_file.keys():
			log_msg.call("  - %s" % failed_path.replace("res://tests/", ""))
		log_msg.call("")
	
	if not json_output_path.is_empty():
		_write_json_results(log_msg, json_output_path, results)
	
	output_file.close()
	
	if failed_count == 0 and total_count > 0 and not aborted_due_to_timeout:
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

func _get_json_output_path() -> String:
	var output_path := DEFAULT_JSON_OUTPUT_PATH
	for arg in OS.get_cmdline_args():
		if arg.begins_with("--json_out="):
			output_path = arg.get_slice("=", 1)
	return output_path

func _get_filter() -> String:
	for arg in OS.get_cmdline_args():
		if arg.begins_with("--filter="):
			return arg.get_slice("=", 1)
	return ""

func _get_timeout_ms() -> int:
	var timeout_ms := DEFAULT_TEST_TIMEOUT_MS
	for arg in OS.get_cmdline_args():
		if arg.begins_with("--timeout_ms="):
			timeout_ms = int(arg.get_slice("=", 1))
	return timeout_ms

func _filter_tests(test_files: Array[String], filter: String) -> Array[String]:
	if filter.is_empty():
		return test_files
	var filtered: Array[String] = []
	for path in test_files:
		if path.contains(filter):
			filtered.append(path)
	return filtered

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

func _result_entry(path: String, status: String, failures: Array[String], start_ms: int) -> Dictionary:
	var elapsed_ms := Time.get_ticks_msec() - start_ms
	return {
		"path": path,
		"status": status,
		"duration_ms": elapsed_ms,
		"failures": failures
	}

func _failures_add(failures_by_file: Dictionary, path: String) -> void:
	if failures_by_file.has(path):
		failures_by_file[path] += 1
	else:
		failures_by_file[path] = 1

func _write_json_results(log_msg: Callable, json_path: String, results: Array[Dictionary]) -> void:
	var payload := {
		"results": results
	}
	var json_str := JSON.stringify(payload, "\t", false)
	var file := FileAccess.open(json_path, FileAccess.WRITE)
	if file == null:
		log_msg.call("Failed to open JSON output file: %s" % json_path)
		return
	file.store_string(json_str)
	file.close()
	log_msg.call("Wrote JSON results to %s" % json_path)

func _run_with_timeout(instance: Object, timeout_ms: int) -> Dictionary:
	var thread := Thread.new()
	var callable := Callable(self, "_thread_run_test").bind(instance)
	var start_err := thread.start(callable)
	if start_err != OK:
		return {"success": false, "timed_out": false}
	var start_ms := Time.get_ticks_msec()
	while thread.is_alive():
		if Time.get_ticks_msec() - start_ms > timeout_ms:
			return {"success": false, "timed_out": true}
		OS.delay_msec(25)
	var success: Variant = thread.wait_to_finish()
	return {"success": bool(success), "timed_out": false}

func _thread_run_test(instance: Object) -> bool:
	return instance.run()
