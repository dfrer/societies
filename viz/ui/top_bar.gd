class_name TopBar
extends PanelContainer

## Top bar UI component displaying simulation info and controls.
## Shows seed, tick, day, checksum, and provides New Run / Load State buttons.

signal new_run_requested(seed_value: int)
signal load_state_requested()
signal save_state_requested()
signal load_replay_requested()
signal save_replay_requested()
signal compare_requested()

@onready var seed_spin: SpinBox = $MarginContainer/HBoxContainer/LeftSection/SeedSpin
@onready var new_run_btn: Button = $MarginContainer/HBoxContainer/LeftSection/NewRunBtn
@onready var load_btn: Button = $MarginContainer/HBoxContainer/LeftSection/LoadBtn
@onready var save_btn: Button = $MarginContainer/HBoxContainer/LeftSection/SaveBtn
@onready var load_replay_btn: Button = $MarginContainer/HBoxContainer/LeftSection/LoadReplayBtn
@onready var save_replay_btn: Button = $MarginContainer/HBoxContainer/LeftSection/SaveReplayBtn
@onready var compare_btn: Button = $MarginContainer/HBoxContainer/LeftSection/CompareBtn

@onready var tick_label: Label = $MarginContainer/HBoxContainer/CenterSection/TickLabel
@onready var day_label: Label = $MarginContainer/HBoxContainer/CenterSection/DayLabel
@onready var speed_label: Label = $MarginContainer/HBoxContainer/CenterSection/SpeedLabel

@onready var checksum_label: Label = $MarginContainer/HBoxContainer/RightSection/ChecksumLabel
@onready var status_label: Label = $MarginContainer/HBoxContainer/RightSection/StatusLabel
@onready var hunger_label: Label = $MarginContainer/HBoxContainer/RightSection/HungerLabel

var _current_seed: int = 42


func _ready() -> void:
	new_run_btn.pressed.connect(_on_new_run_pressed)
	load_btn.pressed.connect(_on_load_pressed)
	save_btn.pressed.connect(_on_save_pressed)
	load_replay_btn.pressed.connect(func(): load_replay_requested.emit())
	save_replay_btn.pressed.connect(func(): save_replay_requested.emit())
	compare_btn.pressed.connect(func(): compare_requested.emit())
	seed_spin.value_changed.connect(_on_seed_changed)

	# Initialize with default seed
	seed_spin.value = _current_seed
	_update_display_idle()


func set_seed(seed_value: int) -> void:
	_current_seed = seed_value
	seed_spin.value = seed_value


func get_seed() -> int:
	return _current_seed


func update_state(snapshot: Dictionary) -> void:
	var tick: int = snapshot.get("tick", 0)
	var day: int = snapshot.get("day", 0)
	var checksum: String = snapshot.get("checksum", "")
	var speed: int = snapshot.get("speed", 0)
	var alive_agents: int = snapshot.get("alive_agents", 0)
	var total_agents: int = snapshot.get("total_agents", 0)
	var avg_hunger: float = snapshot.get("avg_hunger", 0.0)

	tick_label.text = "Tick: %d" % tick
	day_label.text = "Day: %d" % day

	# Show shortened checksum (first 12 chars)
	if checksum.length() > 12:
		checksum_label.text = "Hash: %s..." % checksum.substr(0, 12)
	else:
		checksum_label.text = "Hash: %s" % checksum

	# Speed display
	if speed == 0:
		speed_label.text = "Speed: Paused"
	else:
		speed_label.text = "Speed: %dx" % speed

	# Status
	status_label.text = "Agents: %d/%d" % [alive_agents, total_agents]

	# Hunger
	hunger_label.text = "Avg Hunger: %.1f" % avg_hunger


func set_status_running() -> void:
	status_label.add_theme_color_override("font_color", Color.GREEN)


func set_status_paused() -> void:
	status_label.add_theme_color_override("font_color", Color.YELLOW)


func set_status_error(message: String) -> void:
	status_label.text = "Error: %s" % message
	status_label.add_theme_color_override("font_color", Color.RED)


func set_save_enabled(enabled: bool) -> void:
	save_btn.disabled = not enabled
	save_replay_btn.disabled = not enabled


func _update_display_idle() -> void:
	tick_label.text = "Tick: --"
	day_label.text = "Day: --"
	checksum_label.text = "Hash: --"
	speed_label.text = "Speed: --"
	status_label.text = "Not Running"
	hunger_label.text = "Avg Hunger: --"


func _on_new_run_pressed() -> void:
	new_run_requested.emit(_current_seed)


func _on_load_pressed() -> void:
	load_state_requested.emit()


func _on_save_pressed() -> void:
	save_state_requested.emit()


func _on_seed_changed(value: float) -> void:
	_current_seed = int(value)
