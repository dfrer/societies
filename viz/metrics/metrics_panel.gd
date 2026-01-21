class_name MetricsPanel
extends PanelContainer

## Metrics panel - displays a single combined chart with toggleable series.
## Supports CSV export.

var _sim_state: SimState = null

# Chart
var _chart: SimpleLineChart

# Toggles container
var _toggles_grid: GridContainer

# Defined metrics config
# { key: "metric_id", title: "Name", color: Color }
var _metric_configs = [
	{ "key": "pollution", "title": "Pollution", "color": Color.MEDIUM_PURPLE },
	{ "key": "berry_stock_total", "title": "Berry Stock", "color": Color.FOREST_GREEN },
	{ "key": "tree_stock_total", "title": "Tree Stock", "color": Color.SADDLE_BROWN },
	{ "key": "ore_stock_total", "title": "Ore Stock", "color": Color.SLATE_GRAY },
	{ "key": "avg_hunger", "title": "Avg Hunger", "color": Color.ORANGE_RED },
	{ "key": "starving_agents", "title": "Starving", "color": Color.RED },
	{ "key": "ref_price_food", "title": "Food Price", "color": Color.GOLD },
	{ "key": "trades_today_delta", "title": "Trades/Day", "color": Color.CYAN },
	{ "key": "factions_count", "title": "Factions", "color": Color.CORNFLOWER_BLUE }
]

func _ready() -> void:
	_setup_ui()

func _setup_ui() -> void:
	# Make it look like a popup/overlay panel
	custom_minimum_size = Vector2(400, 300)
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL
	
	# Style: dark background
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = Color(0.15, 0.15, 0.15, 0.95)
	style_box.border_width_left = 2
	style_box.border_width_top = 2
	style_box.border_width_right = 2
	style_box.border_width_bottom = 2
	style_box.border_color = Color(0.3, 0.3, 0.3)
	style_box.corner_radius_top_left = 4
	style_box.corner_radius_top_right = 4
	style_box.corner_radius_bottom_right = 4
	style_box.corner_radius_bottom_left = 4
	add_theme_stylebox_override("panel", style_box)
	
	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 8)
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 10)
	margin.add_child(main_vbox)
	add_child(margin)
	
	# Header Row
	var header_row = HBoxContainer.new()
	main_vbox.add_child(header_row)
	
	var title = Label.new()
	title.text = "Metrics History"
	title.add_theme_font_size_override("font_size", 16)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header_row.add_child(title)
	
	var export_btn = Button.new()
	export_btn.text = "Export CSV"
	export_btn.pressed.connect(_on_export_csv_pressed)
	header_row.add_child(export_btn)
	
	var close_btn = Button.new()
	close_btn.text = " X "
	close_btn.pressed.connect(_on_close_pressed)
	header_row.add_child(close_btn)
	
	main_vbox.add_child(HSeparator.new())
	
	# Chart Area
	_chart = SimpleLineChart.new()
	_chart.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_chart.show_points = false # Performance for large datasets
	main_vbox.add_child(_chart)
	
	main_vbox.add_child(HSeparator.new())
	
	# Toggles Area (Legend)
	var scroll = ScrollContainer.new()
	scroll.custom_minimum_size.y = 80
	scroll.size_flags_vertical = Control.SIZE_SHRINK_END
	main_vbox.add_child(scroll)
	
	_toggles_grid = GridContainer.new()
	_toggles_grid.columns = 3 # 3 columns of checkboxes
	_toggles_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_toggles_grid)
	
	_create_toggles()

func _create_toggles() -> void:
	for config in _metric_configs:
		var cb = CheckBox.new()
		cb.text = config["title"]
		
		# Set checkbox icon color if possible, or just text color
		cb.add_theme_color_override("font_color", config["color"])
		cb.add_theme_color_override("font_pressed_color", config["color"])
		cb.add_theme_color_override("font_hover_color", config["color"])
		
		# Default checked? Maybe just some.
		# Let's say all unchecked by default to reduce clutter, or specific ones.
		# Let's default Pollution and Avg Hunger
		if config["key"] in ["pollution", "avg_hunger"]:
			cb.button_pressed = true
			
		cb.toggled.connect(func(pressed): _on_metric_toggled(config["key"], pressed))
		_toggles_grid.add_child(cb)

func update_sim_state(sim_state: SimState) -> void:
	if sim_state == null:
		return
	
	# Simple check to avoid excessive updates
	# Assuming ticks increase always
	if _sim_state == sim_state and _chart.series_dict.size() > 0:
		# Check if data length changed significantly or just assume update needed for new data point
		var history_len = sim_state.metrics_history.size()
		# Optimization: only update every N ticks or day?
		# For visually smooth updates, every update is fine if data processing is fast.
		# But recreating arrays every frame is bad.
		# Let's check against cached data size if we had it, but simpler: just update.
		pass
		
	_sim_state = sim_state
	_refresh_chart_data()

func _refresh_chart_data() -> void:
	if _sim_state == null: return
	
	var history = _sim_state.metrics_history
	if history.is_empty():
		return
		
	# Calculate derived series if needed (e.g. deltas)
	# But mostly we extract series.
	
	# To avoid parsing everything every frame, we could cache. 
	# But strictly recalculating for "simplicity" first.
	
	# Prepare calculated series
	var trades_delta = _calculate_deltas(history, "trades_today")
	
	for config in _metric_configs:
		var key = config["key"]
		var color = config["color"]
		var data = []
		
		if key == "trades_today_delta":
			data = trades_delta
		else:
			data = _extract_series(history, key)
			
		_chart.add_series(key, data, color)
		
		# Sync visibility with toggles
		# Find the checkbox for this key? 
		# Actually, we rely on the toggle event to set visibility. 
		# But on first add, we need to respect current toggle state.
		var is_visible = _get_toggle_state(key)
		_chart.set_series_visible(key, is_visible)

func _get_toggle_state(key: String) -> bool:
	for i in range(_metric_configs.size()):
		if _metric_configs[i]["key"] == key:
			var cb = _toggles_grid.get_child(i) as CheckBox
			return cb.button_pressed
	return false

func _on_metric_toggled(key: String, pressed: bool) -> void:
	_chart.set_series_visible(key, pressed)

func _on_close_pressed() -> void:
	hide()

func _extract_series(history: Array, key: String) -> Array:
	var series = []
	for snapshot in history:
		series.append(float(snapshot.get(key, 0.0)))
	return series

func _calculate_deltas(history: Array, key: String) -> Array:
	var series = []
	var prev_val = 0.0
	for i in range(history.size()):
		var snapshot = history[i]
		var curr_val = float(snapshot.get(key, 0))
		if i == 0:
			series.append(curr_val)
		else:
			series.append(curr_val - prev_val)
		prev_val = curr_val
	return series

func _on_export_csv_pressed() -> void:
	if _sim_state == null or _sim_state.metrics_history.is_empty():
		OS.alert("No metrics data to export.", "Export Failed")
		return
		
	var path = "user://metrics_export_%d.csv" % Time.get_unix_time_from_system()
	var file = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		OS.alert("Failed to create file: " + path, "Export Error")
		return
		
	# CSV Header
	var keys = ["day", "tick"]
	for c in _metric_configs:
		keys.append(c["key"])
	
	# Note: "trades_today_delta" is calculated, so we need to handle it.
	
	file.store_line(",".join(keys))
	
	var prev_trades = 0
	
	for i in range(_sim_state.metrics_history.size()):
		var s = _sim_state.metrics_history[i]
		var row = []
		
		row.append(str(s.get("day", 0)))
		row.append(str(s.get("tick", 0)))
		
		var curr_trades = int(s.get("trades_today", 0))
		var trade_delta = curr_trades - prev_trades if i > 0 else curr_trades
		prev_trades = curr_trades
		
		for c in _metric_configs:
			var k = c["key"]
			if k == "trades_today_delta":
				row.append(str(trade_delta))
			else:
				row.append(str(s.get(k, 0)))
		
		file.store_line(",".join(row))
		
	file.close()
	OS.alert("Exported metrics to: " + ProjectSettings.globalize_path(path), "Export Successful")
