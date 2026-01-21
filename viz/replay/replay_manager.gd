class_name ReplayManager
extends RefCounted

## Save a replay package containing seed and tuning
static func save_replay(path: String, seed_val: int, tuning: Dictionary) -> bool:
	var data := {
		"version": 1,
		"seed": seed_val,
		"tuning": tuning,
		"timestamp": Time.get_datetime_string_from_system()
	}
	
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("Failed to open file for writing: " + path)
		return false
		
	file.store_string(JSON.stringify(data, "\t"))
	return true

## Load a replay package
static func load_replay(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		push_error("File does not exist: " + path)
		return {}
		
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_error("Failed to open file for reading: " + path)
		return {}
		
	var text := file.get_as_text()
	var json := JSON.new()
	var error := json.parse(text)
	
	if error != OK:
		push_error("JSON Parse Error: " + json.get_error_message())
		return {}
		
	var data = json.get_data()
	if not data is Dictionary:
		push_error("Invalid replay data format")
		return {}
		
	return data
