extends Control
class_name CompareView

@onready var chart: SimpleLineChart = $VBoxContainer/HSplitContainer/ChartContainer/SimpleLineChart
@onready var metric_selector: OptionButton = $VBoxContainer/HBoxContainer/MetricSelector
@onready var stats_tree: Tree = $VBoxContainer/HSplitContainer/StatsContainer/StatsTree
@onready var file_dialog: FileDialog = $FileDialog
@onready var label_a: Label = $VBoxContainer/HBoxContainer/LabelA
@onready var label_b: Label = $VBoxContainer/HBoxContainer/LabelB

var state_a: Dictionary = {}
var state_b: Dictionary = {}
var loading_slot: String = "A" # "A" or "B"

# Metrics available to plot
const METRICS = [
	"alive_agents",
	"avg_hunger",
	"avg_pollution",
	"total_trades",
	"fines_collected",
	"taxes_collected"
]

func _ready() -> void:
	# Setup Metric Selector
	for m in METRICS:
		metric_selector.add_item(m.capitalize().replace("_", " "))
	
	metric_selector.item_selected.connect(_on_metric_selected)
	
	# Setup File Dialog
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.filters = PackedStringArray(["*.json ; JSON State Files"])
	file_dialog.file_selected.connect(_on_file_selected)
	
	# Initial UI state
	_update_stats_table()

func _on_load_a_pressed() -> void:
	loading_slot = "A"
	file_dialog.popup_centered_ratio(0.7)

func _on_load_b_pressed() -> void:
	loading_slot = "B"
	file_dialog.popup_centered_ratio(0.7)

func _on_file_selected(path: String) -> void:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_error("Failed to open " + path)
		return
		
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	if error != OK:
		push_error("JSON Parse Error")
		return
		
	var data = json.get_data()
	if not data is Dictionary:
		push_error("Invalid JSON data")
		return
		
	if loading_slot == "A":
		state_a = data
		label_a.text = "Run A: " + path.get_file()
	else:
		state_b = data
		label_b.text = "Run B: " + path.get_file()
		
	_update_chart()
	_update_stats_table()

func _on_metric_selected(_index: int) -> void:
	_update_chart()

func _update_chart() -> void:
	chart.clear_all()
	
	var metric_idx = metric_selector.selected
	if metric_idx < 0: return
	var metric_key = METRICS[metric_idx]
	
	chart.title = metric_selector.get_item_text(metric_idx)
	
	if not state_a.is_empty():
		var data_a = _extract_metric_history(state_a, metric_key)
		chart.add_series("Run A", data_a, Color.DEEP_SKY_BLUE)
		
	if not state_b.is_empty():
		var data_b = _extract_metric_history(state_b, metric_key)
		chart.add_series("Run B", data_b, Color.ORANGE_RED)

func _extract_metric_history(state: Dictionary, key: String) -> Array:
	var history: Array = state.get("metrics_history", [])
	var data: Array = []
	for entry in history:
		if entry.has(key):
			data.append(float(entry[key]))
		else:
			data.append(0.0)
	return data

func _update_stats_table() -> void:
	stats_tree.clear()
	stats_tree.columns = 3
	var root = stats_tree.create_item()
	stats_tree.set_column_title(0, "Metric")
	stats_tree.set_column_title(1, "Run A")
	stats_tree.set_column_title(2, "Run B")
	stats_tree.set_column_titles_visible(true)
	
	# Helper to get scalar
	var get_val = func(d: Dictionary, k: String):
		if d.has(k): return d[k]
		return "-"
	
	# General Stats
	_add_stat_row(root, "Seed/Run", 
		str(state_a.get("rng", {}).get("seed", "?")) if not state_a.is_empty() else "-", 
		str(state_b.get("rng", {}).get("seed", "?")) if not state_b.is_empty() else "-")
		
	_add_stat_row(root, "Tick", 
		str(state_a.get("tick", "-")), 
		str(state_b.get("tick", "-")))
		
	for m in METRICS:
		# Get final value from metrics history OR current state props if available
		var val_a = "-"
		var val_b = "-"
		
		if not state_a.is_empty():
			val_a = str(_get_final_metric(state_a, m))
			
		if not state_b.is_empty():
			val_b = str(_get_final_metric(state_b, m))
			
		_add_stat_row(root, m.capitalize().replace("_", " "), val_a, val_b)

func _get_final_metric(state: Dictionary, key: String) -> Variant:
	# Try top level first
	if state.has(key): return state[key]
	# Try last history entry
	var hist: Array = state.get("metrics_history", [])
	if not hist.is_empty():
		var last = hist.back()
		if last.has(key): return last[key]
	return "-"

func _add_stat_row(parent: TreeItem, title: String, val_a: String, val_b: String) -> void:
	var item = stats_tree.create_item(parent)
	item.set_text(0, title)
	item.set_text(1, val_a)
	item.set_text(2, val_b)
	
	# Colorize deltas?
	# Simple logic: if numeric, and A > B, maybe color?
	# Leave simple for now.

func _on_close_pressed() -> void:
	visible = false
