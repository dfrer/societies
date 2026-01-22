class_name TestTimelineLogging
extends SceneTree

var _sim: Sim = null
var _output_dir := "timelinelog"

func _init():
	pass

func _ready():
	print("Starting Timeline Logging Test...")
	_cleanup_logs()
	_run_test()
	
	print("\nTest Complete")
	quit()

func _run_test():
	# Initialize Sim
	_sim = Sim.new()
	_sim.init_new(12345)
	
	# Run for a few ticks to generate events
	print("Running sim for 100 ticks...")
	_sim.step(100)
	
	# Check if log file exists
	var dir = DirAccess.open("res://")
	var day = 0 # 100 ticks is day 0 (24 ticks per day usually, so day 4?)
	# Ticks per day default is 24.
	# 100 / 24 = 4 days.
	# So we expect logs for day 0, 1, 2, 3, 4?
	# TimelineLogger logs daily.
	
	# Let's check for any log file in that dir
	if not dir.dir_exists(_output_dir):
		printerr("FAIL: Log directory not created")
		return

	var found_logs = false
	dir.open(_output_dir)
	dir.list_dir_begin()
	var file_name = dir.get_next()
	while file_name != "":
		if not dir.current_is_dir() and file_name.ends_with(".jsonl"):
			print("Found log file: ", file_name)
			_verify_log_content(_output_dir + "/" + file_name)
			found_logs = true
		file_name = dir.get_next()
	
	if not found_logs:
		printerr("FAIL: No log files found")
	else:
		print("SUCCESS: Log files found and verified")

func _verify_log_content(path: String):
	var file = FileAccess.open("res://" + path, FileAccess.READ)
	if not file:
		printerr("FAIL: Could not open log file: ", path)
		return
		
	var line_count = 0
	while file.get_position() < file.get_length():
		var line = file.get_line()
		if line.strip_edges() == "": continue
		
		var json = JSON.new()
		var error = json.parse(line)
		if error != OK:
			printerr("FAIL: JSON parse error in log file: ", json.get_error_message())
			continue
			
		var data = json.data
		if not data.has("tick") or not data.has("type"):
			printerr("FAIL: Invalid event format: ", line)
		
		line_count += 1
		
	print("Verified %d events in %s" % [line_count, path])
	if line_count == 0:
		printerr("WARNING: Log file is empty")

func _cleanup_logs():
	var dir = DirAccess.open("res://")
	if dir.dir_exists(_output_dir):
		dir.open(_output_dir)
		dir.list_dir_begin()
		var file_name = dir.get_next()
		while file_name != "":
			if not dir.current_is_dir():
				dir.remove(file_name)
			file_name = dir.get_next()
		dir.remove(_output_dir) # Remove dir if empty?
