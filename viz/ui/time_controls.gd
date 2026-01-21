class_name TimeControls
extends PanelContainer

## UI component for simulation time controls.
## Provides pause, play, speed, step, and jump-to-day functionality.

signal pause_pressed()
signal play_pressed(speed: int)
signal step_tick_pressed()
signal step_day_pressed()
signal jump_to_day_requested(day: int)

@onready var pause_btn: Button = $MarginContainer/VBoxContainer/ButtonRow/PauseBtn
@onready var play_1x_btn: Button = $MarginContainer/VBoxContainer/ButtonRow/Play1xBtn
@onready var play_5x_btn: Button = $MarginContainer/VBoxContainer/ButtonRow/Play5xBtn
@onready var play_20x_btn: Button = $MarginContainer/VBoxContainer/ButtonRow/Play20xBtn
@onready var step_tick_btn: Button = $MarginContainer/VBoxContainer/StepRow/StepTickBtn
@onready var step_day_btn: Button = $MarginContainer/VBoxContainer/StepRow/StepDayBtn
@onready var jump_day_spin: SpinBox = $MarginContainer/VBoxContainer/JumpRow/JumpDaySpin
@onready var jump_btn: Button = $MarginContainer/VBoxContainer/JumpRow/JumpBtn

var _current_speed: int = 0


func _ready() -> void:
	pause_btn.pressed.connect(_on_pause_pressed)
	play_1x_btn.pressed.connect(_on_play_1x_pressed)
	play_5x_btn.pressed.connect(_on_play_5x_pressed)
	play_20x_btn.pressed.connect(_on_play_20x_pressed)
	step_tick_btn.pressed.connect(_on_step_tick_pressed)
	step_day_btn.pressed.connect(_on_step_day_pressed)
	jump_btn.pressed.connect(_on_jump_pressed)

	_update_button_states()


func set_current_speed(spd: int) -> void:
	_current_speed = spd
	_update_button_states()


func set_current_day(day: int) -> void:
	# Update spinbox minimum to be current day + 1
	jump_day_spin.min_value = day + 1
	if jump_day_spin.value < jump_day_spin.min_value:
		jump_day_spin.value = jump_day_spin.min_value


func set_enabled(enabled: bool) -> void:
	pause_btn.disabled = not enabled
	play_1x_btn.disabled = not enabled
	play_5x_btn.disabled = not enabled
	play_20x_btn.disabled = not enabled
	step_tick_btn.disabled = not enabled
	step_day_btn.disabled = not enabled
	jump_day_spin.editable = enabled
	jump_btn.disabled = not enabled


func _update_button_states() -> void:
	# Visual feedback for current speed
	pause_btn.button_pressed = (_current_speed == 0)
	play_1x_btn.button_pressed = (_current_speed == 1)
	play_5x_btn.button_pressed = (_current_speed == 5)
	play_20x_btn.button_pressed = (_current_speed == 20)


func _on_pause_pressed() -> void:
	pause_pressed.emit()


func _on_play_1x_pressed() -> void:
	play_pressed.emit(1)


func _on_play_5x_pressed() -> void:
	play_pressed.emit(5)


func _on_play_20x_pressed() -> void:
	play_pressed.emit(20)


func _on_step_tick_pressed() -> void:
	step_tick_pressed.emit()


func _on_step_day_pressed() -> void:
	step_day_pressed.emit()


func _on_jump_pressed() -> void:
	var target_day: int = int(jump_day_spin.value)
	jump_to_day_requested.emit(target_day)
