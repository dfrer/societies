class_name TimelineLogger
extends ISimSystem

const LOG_DIR := "timelinelog"

var _log_file: FileAccess = null
var _current_log_path: String = ""

func _init() -> void:
	pass

func tick(sim: RefCounted, state: SimState) -> void:
	# On first tick, initialize the log file
	if _log_file == null:
		_init_log_file()
	
	_flush_new_events(state)

func _init_log_file() -> void:
	# Ensure directory exists
	var dir = DirAccess.open("res://")
	if not dir.dir_exists(LOG_DIR):
		dir.make_dir(LOG_DIR)
	
	# Create unique filename timestamp
	var timestamp = Time.get_datetime_string_from_system().replace(":", "-")
	_current_log_path = "res://%s/timeline_%s.jsonl" % [LOG_DIR, timestamp]
	
	_log_file = FileAccess.open(_current_log_path, FileAccess.WRITE)
		
	if not _log_file:
		push_error("Failed to open timeline log file: %s" % _current_log_path)
	else:
		print("Timeline logging to: %s" % _current_log_path)

func _flush_new_events(state: SimState) -> void:
	if not _log_file:
		return
		
	var events := state.events
	var current_tick = state.tick
	var events_to_log = []
	
	# Iterate backwards to find events for this tick
	# Assumes TimelineLogger is the LAST system to run in the tick.
	for i in range(events.size() - 1, -1, -1):
		var event = events[i]
		if event.tick < current_tick:
			break
		if event.tick == current_tick:
			events_to_log.append(event)
	
	# events_to_log is now in reverse order (newest first). Reverse it back.
	events_to_log.reverse()
	
	for event in events_to_log:
		_log_file.store_line(JSON.stringify(event))
