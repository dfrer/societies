class_name SimpleLineChart
extends Control

@export var title: String = ""
@export var show_points: bool = true
@export var show_legend: bool = false # Not fully implemented in chart itself, managed by parent

# Data structure: { "series_key": { "data": [floats], "color": Color, "visible": bool } }
var series_dict: Dictionary = {}
var y_min: float = 0.0
var y_max: float = 100.0
var auto_scale_y: bool = true

func _ready() -> void:
	custom_minimum_size = Vector2(200, 150)

# Add or update a data series
func add_series(key: String, data: Array, color: Color) -> void:
	series_dict[key] = {
		"data": data,
		"color": color,
		"visible": true
	}
	_update_scales()
	queue_redraw()

# Set visibility of a series
func set_series_visible(key: String, visible: bool) -> void:
	if series_dict.has(key):
		series_dict[key]["visible"] = visible
		_update_scales()
		queue_redraw()

func clear_all() -> void:
	series_dict.clear()
	queue_redraw()

func update_series_data(series_data: Dictionary) -> void:
	var updated := false
	for key in series_data:
		if series_dict.has(key):
			series_dict[key]["data"] = series_data[key]
			updated = true

	if updated:
		_update_scales()
		queue_redraw()

func _update_scales() -> void:
	if not auto_scale_y:
		return
		
	var global_min = 0.0
	var global_max = 1.0 # Default fallback
	var first = true
	
	for key in series_dict:
		var s = series_dict[key]
		if not s["visible"] or s["data"].is_empty():
			continue
			
		for v in s["data"]:
			if first:
				global_min = v
				global_max = v
				first = false
			else:
				if v < global_min: global_min = v
				if v > global_max: global_max = v
	
	if first: # No data found
		y_min = 0.0
		y_max = 1.0
	else:
		var range_val = global_max - global_min
		if range_val == 0:
			range_val = 1.0
			
		y_min = global_min - range_val * 0.05
		y_max = global_max + range_val * 0.05
		
		# Optional: Keep 0 in view if numbers are positive
		if y_min < 0 and global_min >= 0: y_min = 0

func _draw() -> void:
	var rect = get_rect()
	var padding_top = 20.0
	var padding_bottom = 20.0
	var padding_left = 40.0 # Space for labels
	var padding_right = 10.0
	
	if title != "":
		padding_top = 30.0
	
	var graph_rect = Rect2(padding_left, padding_top, 
						   rect.size.x - padding_left - padding_right, 
						   rect.size.y - padding_top - padding_bottom)
	
	# Draw Background
	draw_rect(graph_rect, Color(0.1, 0.1, 0.1, 0.5))
	draw_rect(graph_rect, Color(0.3, 0.3, 0.3), false, 1.0) # Border
	
	# Draw Title
	var font = get_theme_font("font")
	var font_size = get_theme_font_size("font_size")
	if title != "":
		draw_string(font, Vector2(rect.size.x / 2 - font.get_string_size(title).x / 2, 20), title, HORIZONTAL_ALIGNMENT_CENTER, -1, font_size)

	# Y Axis Labels (Min/Max)
	draw_string(font, Vector2(0, graph_rect.end.y), "%.1f" % y_min, HORIZONTAL_ALIGNMENT_RIGHT, padding_left - 5, 10)
	draw_string(font, Vector2(0, graph_rect.position.y + 10), "%.1f" % y_max, HORIZONTAL_ALIGNMENT_RIGHT, padding_left - 5, 10)
	
	# Draw Reference Lines (Grid)
	var grid_color = Color(1, 1, 1, 0.1)
	draw_line(Vector2(graph_rect.position.x, graph_rect.position.y + graph_rect.size.y/2), 
			  Vector2(graph_rect.end.x, graph_rect.position.y + graph_rect.size.y/2), grid_color)

	if series_dict.is_empty():
		return

	var value_range = y_max - y_min
	if value_range == 0: value_range = 1.0
	
	for key in series_dict:
		var s = series_dict[key]
		if not s["visible"] or s["data"].size() < 2:
			continue
			
		var data = s["data"]
		var color = s["color"]
		var count = data.size()
		var points = PackedVector2Array()
		var step_x = graph_rect.size.x / (count - 1)
		
		for i in range(count):
			var val = data[i]
			var n_val = (val - y_min) / value_range
			# Clamp to graph area to avoid drawing outside
			n_val = clampf(n_val, 0.0, 1.0)
			
			var x = graph_rect.position.x + i * step_x
			var y = graph_rect.end.y - n_val * graph_rect.size.y
			points.append(Vector2(x, y))
			
		draw_polyline(points, color, 2.0, true)
		
		if show_points and count < 40:
			for p in points:
				draw_circle(p, 3.0, color)
