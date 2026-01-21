## Export Metrics CSV - reads a simulation state JSON and exports metrics_history to CSV
extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var state_path := ""
	var out_path := "metrics.csv"
	
	for arg in args:
		if arg.begins_with("--state="):
			state_path = arg.split("=")[1]
		if arg.begins_with("--out="):
			out_path = arg.split("=")[1]
	
	if state_path == "":
		print("Usage: godot -s tools/export_metrics_csv.gd -- --state=path/to/state.json [--out=path.csv]")
		quit(1)
		return
	
	var data = _load_json_file(state_path)
	if data == null:
		print("ERROR: Could not load state file: %s" % state_path)
		quit(1)
		return
	
	var history: Array = data.get("metrics_history", [])
	if history.is_empty():
		print("No metrics found in state file.")
		quit(0)
		return
	
	if _export_csv(history, out_path):
		print("Exported %d days of metrics to %s" % [history.size(), out_path])
	else:
		print("Export failed.")
	
	quit(0)

func _load_json_file(path: String):
	if not FileAccess.file_exists(path):
		return null
	var file := FileAccess.open(path, FileAccess.READ)
	var content := file.get_as_text()
	file.close()
	return JSON.parse_string(content)

func _export_csv(history: Array, path: String) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if not file: return false
	
	var keys = history[0].keys()
	var header := ""
	for i in range(keys.size()):
		header += keys[i] + ("," if i < keys.size() - 1 else "")
	file.store_line(header)
	
	for snapshot in history:
		var line := ""
		for i in range(keys.size()):
			line += str(snapshot[keys[i]]) + ("," if i < keys.size() - 1 else "")
		file.store_line(line)
	
	file.close()
	return true
