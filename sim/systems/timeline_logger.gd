class_name TimelineLogger
extends ISimSystem

const LOG_DIR := "timelinelog"

var _log_file: FileAccess = null
var _current_log_path: String = ""
# Track the index of the next event to log from the global event list
var _next_event_index: int = 0

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
	
	# Create unique filename timestamp for this simulation RUN
	# We use a single file for the entire run.
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
		
	var all_events := state.events
	var total_events = all_events.size()
	
	# If we have no new events, return
	if _next_event_index >= total_events:
		# Handle case where events might have been pruned (unlikely with just 500 limit if we run every tick, but safe to check)
		# If index is out of bounds due to pruning, reset to 0 or appropriate start
		if _next_event_index > total_events:
			_next_event_index = 0
		return
	
	# Log all events from our last known index to the end
	for i in range(_next_event_index, total_events):
		var event = all_events[i]
		_log_file.store_line(JSON.stringify(event))
	
	# Update index for next time
	_next_event_index = total_events
