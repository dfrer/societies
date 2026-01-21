## Serializers - helper functions for deterministic JSON serialization
class_name Serializers
extends RefCounted

## Convert dictionary to JSON with sorted keys for determinism
static func to_sorted_json(data: Variant) -> String:
	var sorted: Variant = _sort_recursive(data)
	return JSON.stringify(sorted, "", false)

## Recursively sort dictionary keys
static func _sort_recursive(data: Variant) -> Variant:
	if data is Dictionary:
		var sorted_dict := {}
		var dict_data: Dictionary = data
		var keys: Array = dict_data.keys()
		keys.sort()
		for key in keys:
			sorted_dict[key] = _sort_recursive(dict_data[key])
		return sorted_dict
	elif data is Array:
		var sorted_array := []
		for item in data:
			sorted_array.append(_sort_recursive(item))
		return sorted_array
	else:
		return data

## Parse JSON string to variant
static func from_json(json_str: String) -> Variant:
	var json := JSON.new()
	var error := json.parse(json_str)
	if error == OK:
		return json.data
	else:
		push_error("JSON parse error: " + json.get_error_message())
		return null

## Compute SHA-256 hash of a string
static func sha256_hash(data: String) -> String:
	var ctx := HashingContext.new()
	ctx.start(HashingContext.HASH_SHA256)
	ctx.update(data.to_utf8_buffer())
	var hash_bytes := ctx.finish()
	return hash_bytes.hex_encode()

## Save data to JSON file
static func save_json_file(path: String, data: Variant) -> bool:
	var json_str := to_sorted_json(data)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("Failed to open file for writing: " + path)
		return false
	file.store_string(json_str)
	file.close()
	return true

## Load data from JSON file
static func load_json_file(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		push_error("File not found: " + path)
		return null
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_error("Failed to open file for reading: " + path)
		return null
	var json_str := file.get_as_text()
	file.close()
	return from_json(json_str)
