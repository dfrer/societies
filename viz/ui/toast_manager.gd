## ToastManager - Displays non-intrusive notifications to the user
## Better UX than error labels for temporary messages

class_name ToastManager
extends Object

## Toast notification data
class ToastNotification:
	var message: String
	var type: String  # "info", "success", "warning", "error"
	var duration: float
	var timestamp: float
	
	func _init(msg: String, msg_type: String = "info", dur: float = 3.0):
		message = msg
		type = msg_type
		duration = dur
		timestamp = Time.get_ticks_msec() / 1000.0


## Active toasts
var _active_toasts: Array[ToastNotification] = []

## UI Container for toasts
var _toast_container: Control

## Maximum concurrent toasts
const MAX_TOASTS: int = 5

## Toast colors by type
const TOAST_COLORS = {
	"info": Color(0.2, 0.6, 1.0),
	"success": Color(0.2, 0.8, 0.4),
	"warning": Color(1.0, 0.8, 0.2),
	"error": Color(1.0, 0.3, 0.3)
}


func _init(container: Control = null):
	_toast_container = container


## Set the container for displaying toasts
func set_container(container: Control) -> void:
	_toast_container = container


## Show a toast notification
func show_toast(message: String, type: String = "info", duration: float = 3.0) -> void:
	var toast = ToastNotification.new(message, type, duration)
	_active_toasts.append(toast)
	
	# Create UI if we have a container
	if _toast_container:
		_create_toast_ui(toast)
	
	# Limit number of active toasts
	if _active_toasts.size() > MAX_TOASTS:
		_active_toasts.pop_front()


## Update toasts (call from _process)
func update_toasts() -> void:
	var current_time = Time.get_ticks_msec() / 1000.0
	var to_remove: Array[int] = []
	
	for i in range(_active_toasts.size()):
		var toast = _active_toasts[i]
		if current_time - toast.timestamp > toast.duration:
			to_remove.append(i)
	
	# Remove expired toasts
	for i in range(to_remove.size() - 1, -1, -1):
		_active_toasts.remove_at(to_remove[i])
	
	# Update UI container
	if _toast_container:
		_update_toast_ui()


## Clear all active toasts
func clear_toasts() -> void:
	_active_toasts.clear()
	if _toast_container:
		_update_toast_ui()


## Get active toast count
func get_active_count() -> int:
	return _active_toasts.size()


## Create UI for a toast
func _create_toast_ui(toast: ToastNotification) -> void:
	if not _toast_container:
		return
		
	var toast_panel = PanelContainer.new()
	toast_panel.name = "Toast_%d" % toast.timestamp
	
	# Style
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = TOAST_COLORS.get(toast.type, Color.GRAY)
	style_box.bg_color.a = 0.9
	style_box.corner_radius_top_left = 6
	style_box.corner_radius_top_right = 6
	style_box.corner_radius_bottom_left = 6
	style_box.corner_radius_bottom_right = 6
	style_box.border_width_left = 2
	style_box.border_width_right = 2
	style_box.border_width_top = 2
	style_box.border_width_bottom = 2
	style_box.border_color = TOAST_COLORS.get(toast.type, Color.GRAY).lightened(0.3)
	toast_panel.add_theme_stylebox_override("panel", style_box)
	
	# Content
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 12)
	margin.add_theme_constant_override("margin_right", 12)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	toast_panel.add_child(margin)
	
	var label = Label.new()
	label.text = toast.message
	label.add_theme_font_size_override("font_size", 14)
	label.add_theme_color_override("font_color", Color.WHITE)
	margin.add_child(label)
	
	# Add to container
	_toast_container.add_child(toast_panel)
	_update_toast_ui()


## Update toast UI positioning
func _update_toast_ui() -> void:
	if not _toast_container:
		return
		
	# Clear existing UI children (except the container itself)
	for child in _toast_container.get_children():
		child.queue_free()
	
	# Recreate UI for active toasts
	var offset = 0.0
	for toast in _active_toasts:
		var toast_panel = PanelContainer.new()
		
		# Style
		var style_box = StyleBoxFlat.new()
		style_box.bg_color = TOAST_COLORS.get(toast.type, Color.GRAY)
		style_box.bg_color.a = 0.9
		style_box.corner_radius_top_left = 6
		style_box.corner_radius_top_right = 6
		style_box.corner_radius_bottom_left = 6
		style_box.corner_radius_bottom_right = 6
		style_box.border_width_left = 2
		style_box.border_width_right = 2
		style_box.border_width_top = 2
		style_box.border_width_bottom = 2
		style_box.border_color = TOAST_COLORS.get(toast.type, Color.GRAY).lightened(0.3)
		toast_panel.add_theme_stylebox_override("panel", style_box)
		
		# Position
		toast_panel.position.y = offset
		toast_panel.size.x = _toast_container.size.x
		toast_panel.size.y = 40
		
		# Content
		var margin = MarginContainer.new()
		margin.add_theme_constant_override("margin_left", 12)
		margin.add_theme_constant_override("margin_right", 12)
		margin.add_theme_constant_override("margin_top", 8)
		margin.add_theme_constant_override("margin_bottom", 8)
		toast_panel.add_child(margin)
		
		var label = Label.new()
		label.text = toast.message
		label.add_theme_font_size_override("font_size", 14)
		label.add_theme_color_override("font_color", Color.WHITE)
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		margin.add_child(label)
		
		_toast_container.add_child(toast_panel)
		offset += 50.0


## Show specific toast types
func show_info(message: String) -> void:
	show_toast(message, "info")


func show_success(message: String) -> void:
	show_toast(message, "success")


func show_warning(message: String) -> void:
	show_toast(message, "warning")


func show_error(message: String) -> void:
	show_toast(message, "error", 5.0)  # Errors stay longer