extends Control

## Main visualizer scene that provides a UI layer to drive the headless simulation.
## Does not modify sim behavior - only calls SimRunnerNode methods and displays state.

@onready var sim_runner: SimRunnerNode = $SimRunnerNode
@onready var top_bar: TopBar = $VBoxContainer/TopBar
@onready var time_controls: TimeControls = $VBoxContainer/MainContent/LeftPanel/TimeControls
@onready var overlay_toolbar: OverlayToolbar = $VBoxContainer/MainContent/LeftPanel/DataTabs/Overlays
@onready var map_view: MapView = $VBoxContainer/MainContent/CenterPanel/VBoxContainer/MapViewContainer/MapView
@onready var coord_label: Label = $VBoxContainer/MainContent/CenterPanel/VBoxContainer/BottomBar/LeftSection/CoordLabel
@onready var selection_label: Label = $VBoxContainer/MainContent/CenterPanel/VBoxContainer/BottomBar/LeftSection/SelectionLabel
@onready var zoom_label: Label = $VBoxContainer/MainContent/CenterPanel/VBoxContainer/BottomBar/RightSection/ZoomLabel
@onready var metrics_button: Button = $VBoxContainer/MainContent/CenterPanel/VBoxContainer/BottomBar/RightSection/MetricsButton
@onready var inspector_panel: InspectorPanel = $VBoxContainer/MainContent/RightPanel/InspectorPanel
@onready var metrics_panel: PanelContainer = $VBoxContainer/MainContent/RightPanel/MetricsPanel
@onready var right_panel: VBoxContainer = $VBoxContainer/MainContent/RightPanel
@onready var data_tabs: TabContainer = $VBoxContainer/MainContent/LeftPanel/DataTabs
@onready var split_container: HSplitContainer = $VBoxContainer/MainContent
@onready var market_panel: MarketPanel = $VBoxContainer/MainContent/LeftPanel/DataTabs/Market
@onready var factions_panel: FactionsPanel = $VBoxContainer/MainContent/LeftPanel/DataTabs/Factions
@onready var contracts_panel: PanelContainer = $VBoxContainer/MainContent/LeftPanel/DataTabs/Contracts
# @onready var timeline_panel: Control = $VBoxContainer/MainContent/LeftPanel/DataTabs/Timeline  # Temporarily disabled
@onready var file_dialog: FileDialog = $FileDialog
@onready var save_dialog: FileDialog = $SaveDialog
@onready var error_label: Label = $VBoxContainer/MainContent/CenterPanel/ErrorLabel

## Default seed for new runs
@export var default_seed: int = 42

## Whether to auto-start a new run on load
@export var auto_start: bool = true

var _is_file_dialog_for_load: bool = true
var _dialog_mode: String = "state" # "state" or "replay"

## Update throttler for performance optimization
var _update_throttler: UpdateThrottler

## UI Controllers for separation of concerns
var _state_controller: StateController
var _map_controller: MapViewController
var _panel_controller: PanelController

## Toast manager for notifications
var _toast_manager: ToastManager

@onready var compare_view: CompareView = $CompareView



func _ready() -> void:
	_log_startup_diagnostics()
	if not _validate_required_nodes():
		return
	# Initialize update throttler
	_update_throttler = UpdateThrottler.new()
	
	# Initialize toast manager (with container optional for now)
	_toast_manager = ToastManager.new(null)
	
	# Connect signals first to catch any early events
	_connect_signals()
	_setup_file_dialog()
	_setup_save_dialog()

	# Disable time controls until a run is started
	if time_controls:
		time_controls.set_enabled(false)
	
	# Connect metrics button if available
	if metrics_button:
		metrics_button.toggled.connect(_on_metrics_button_toggled)
	
	# Connect right panel visibility if available
	if right_panel:
		right_panel.visibility_changed.connect(_on_metrics_panel_visibility_changed)

	# Setup timeline panel if available (temporarily disabled)
	# if timeline_panel and sim_runner:
	# 	timeline_panel.setup(sim_runner)
	# 	timeline_panel.request_jump_to_tick.connect(_on_jump_to_tick_requested)

	# Initialize controllers (after basic setup)
	_initialize_controllers()

	# Auto-start with default seed if enabled
	if auto_start:
		# Delay auto-start slightly to ensure everything is ready
		call_deferred("_start_new_run", default_seed)


func _log_startup_diagnostics() -> void:
	var version := Engine.get_version_info()
	var version_str := "%s.%s.%s" % [version.get("major", 0), version.get("minor", 0), version.get("patch", 0)]
	print("Visualizer: Startup diagnostics")
	print("  Godot version: %s" % version_str)
	print("  OS: %s" % OS.get_name())
	print("  Auto-start: %s" % str(auto_start))
	print("  Default seed: %d" % default_seed)


func _validate_required_nodes() -> bool:
	var missing := []
	if sim_runner == null:
		missing.append("SimRunnerNode")
	if top_bar == null:
		missing.append("TopBar")
	if time_controls == null:
		missing.append("TimeControls")
	if map_view == null:
		missing.append("MapView")
	if error_label == null:
		missing.append("ErrorLabel")

	if missing.is_empty():
		return true

	var message := "Visualizer startup missing nodes: %s" % ", ".join(missing)
	push_error(message)
	_show_startup_error(message)
	return false


func _connect_signals() -> void:
	# SimRunnerNode signals
	sim_runner.state_changed.connect(_on_state_changed)
	sim_runner.run_started.connect(_on_run_started)
	sim_runner.run_loaded.connect(_on_run_loaded)
	sim_runner.error_occurred.connect(_on_error_occurred)

	# TopBar signals
	top_bar.new_run_requested.connect(_on_new_run_requested)
	top_bar.load_state_requested.connect(_on_load_state_requested)
	top_bar.save_state_requested.connect(_on_save_state_requested)
	top_bar.load_replay_requested.connect(_on_load_replay_requested)
	top_bar.save_replay_requested.connect(_on_save_replay_requested)
	top_bar.compare_requested.connect(_on_compare_requested)

	# TimeControls signals
	time_controls.pause_pressed.connect(_on_pause_pressed)
	time_controls.play_pressed.connect(_on_play_pressed)
	time_controls.step_tick_pressed.connect(_on_step_tick_pressed)
	time_controls.step_day_pressed.connect(_on_step_day_pressed)
	time_controls.jump_to_day_requested.connect(_on_jump_to_day_requested)

	# OverlayToolbar signals
	overlay_toolbar.overlay_changed.connect(_on_overlay_changed)

	# Share overlay settings between toolbar and map view
	overlay_toolbar.set_settings(map_view.overlay_settings)

	# MapView signals
	map_view.tile_hovered.connect(_on_tile_hovered)
	map_view.tile_clicked.connect(_on_tile_clicked)
	map_view.agent_clicked.connect(_on_agent_clicked)


func _setup_file_dialog() -> void:
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.filters = PackedStringArray(["*.json ; JSON Save Files"])
	file_dialog.title = "Load Simulation State"
	file_dialog.file_selected.connect(_on_file_selected)


func _setup_save_dialog() -> void:
	save_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	save_dialog.access = FileDialog.ACCESS_FILESYSTEM
	save_dialog.filters = PackedStringArray(["*.json ; JSON Save Files"])
	save_dialog.title = "Save Simulation State"
	save_dialog.file_selected.connect(_on_save_file_selected)


## Initialize controllers
func _initialize_controllers() -> void:
	# Create controllers with null checks
	if map_view and sim_runner:
		_map_controller = MapViewController.new(map_view, sim_runner)
	
	if data_tabs and market_panel and factions_panel and contracts_panel and metrics_panel and sim_runner:
		_panel_controller = PanelController.new(data_tabs, market_panel, factions_panel, contracts_panel, null, metrics_panel, sim_runner)
	
	if sim_runner and top_bar and time_controls and _map_controller and _panel_controller and inspector_panel and _update_throttler:
		_state_controller = StateController.new(sim_runner, top_bar, time_controls, _map_controller, _panel_controller, inspector_panel, _update_throttler)
		
		# Connect controller signals
		_state_controller.speed_changed.connect(_on_speed_changed)
		_state_controller.day_changed.connect(_on_day_changed)


func _start_new_run(seed_value: int) -> void:
	_clear_error()
	top_bar.set_seed(seed_value)
	# Start the new run through state controller
	_state_controller.start_new_run(seed_value)


func _clear_error() -> void:
	error_label.text = ""
	error_label.visible = false


func _show_error(message: String) -> void:
	error_label.text = message
	error_label.visible = true
	top_bar.set_status_error(message)


func _show_startup_error(message: String) -> void:
	if error_label:
		error_label.text = message
		error_label.visible = true
	if top_bar:
		top_bar.set_status_error(message)


# These methods are now handled by controllers


# Signal handlers

func _on_state_changed(snapshot: Dictionary) -> void:
	# Delegate to state controller
	_state_controller.on_state_changed(snapshot)


## Process throttled updates for performance
func _process(delta: float) -> void:
	# Delegate to state controller
	_state_controller.process_updates(delta)
	
	# Update toast notifications
	_toast_manager.update_toasts()


## Update zoom label
func _update_zoom_label() -> void:
	zoom_label.text = "Zoom: %d%%" % int(_map_controller.get_zoom_level() * 100)


func _on_run_started(seed_value: int) -> void:
	_state_controller.on_run_started(seed_value)
	print("Visualizer: New run started with seed %d" % seed_value)


func _on_run_loaded(path: String) -> void:
	_state_controller.on_run_loaded(path)
	print("Visualizer: State loaded from %s" % path)


func _on_error_occurred(message: String) -> void:
	_state_controller.on_error_occurred(message)
	_show_error(message)
	print("Visualizer Error: %s" % message)


func _on_new_run_requested(seed_value: int) -> void:
	_state_controller.start_new_run(seed_value)


func _on_load_state_requested() -> void:
	_dialog_mode = "state"
	file_dialog.filters = PackedStringArray(["*.json ; JSON Save Files"])
	file_dialog.popup_centered_ratio(0.7)


func _on_save_state_requested() -> void:
	_dialog_mode = "state"
	save_dialog.filters = PackedStringArray(["*.json ; JSON Save Files"])
	save_dialog.popup_centered_ratio(0.7)

func _on_load_replay_requested() -> void:
	_dialog_mode = "replay"
	file_dialog.filters = PackedStringArray(["*.rply ; Replay Files", "*.json ; JSON Files"])
	file_dialog.popup_centered_ratio(0.7)

func _on_save_replay_requested() -> void:
	_dialog_mode = "replay"
	save_dialog.filters = PackedStringArray(["*.rply ; Replay Files"])
	save_dialog.popup_centered_ratio(0.7)


func _on_file_selected(path: String) -> void:
	_clear_error()
	
	if _dialog_mode == "replay":
		var data: Dictionary = ReplayManager.load_replay(path)
		if data.is_empty():
			_show_error("Failed to load replay: " + path)
			_toast_manager.show_error("Failed to load replay: " + path)
			return
		
		# Start new run with seed
		var seed_val: int = int(data.get("seed", 42))
		var tuning: Dictionary = data.get("tuning", {})
		
		_state_controller.start_new_run(seed_val)
		
		# Override tuning if needed (SimRunnerNode caches tuning in new_run, so we need to inject it)
		# SimRunnerNode uses Sim.init_new which loads tuning. We should overwrite it.
		if sim_runner.sim != null:
			sim_runner.sim.state.tuning.merge(tuning, true)
			# Refresh visualizer components dependent on tuning
			_map_controller.update_world_size() # In case world size changed
		
		_toast_manager.show_success("Replay loaded from %s" % path)
		print("Visualizer: Replay loaded from %s" % path)
		return
	
	# Default state load
	_state_controller.load_state(path)
	_toast_manager.show_success("State loaded from %s" % path)


func _on_save_file_selected(path: String) -> void:
	if _dialog_mode == "replay":
		if not path.ends_with(".rply"):
			path += ".rply"
		
		if sim_runner.sim == null:
			_show_error("No simulation to save")
			_toast_manager.show_error("No simulation to save")
			return
			
		var seed_val: int = sim_runner.current_seed
		var tuning: Dictionary = sim_runner.sim.state.tuning
		
		if ReplayManager.save_replay(path, seed_val, tuning):
			_toast_manager.show_success("Replay saved to %s" % path)
			print("Visualizer: Replay package saved to %s" % path)
		else:
			_show_error("Failed to save replay: " + path)
			_toast_manager.show_error("Failed to save replay: " + path)
		return

	if not path.ends_with(".json"):
		path += ".json"

	_state_controller.save_state(path)
	_toast_manager.show_success("State saved to %s" % path)


func _on_pause_pressed() -> void:
	_state_controller.set_speed(0)


func _on_play_pressed(speed: int) -> void:
	_state_controller.set_speed(speed)


func _on_step_tick_pressed() -> void:
	_state_controller.step_ticks(1)


func _on_step_day_pressed() -> void:
	_state_controller.step_day()


func _on_jump_to_day_requested(day: int) -> void:
	_state_controller.jump_to_day(day)


func _on_tile_hovered(tile: Vector2i) -> void:
	if _map_controller.get_selection().is_valid_tile(tile):
		coord_label.text = "Tile: (%d, %d)" % [tile.x, tile.y]
	else:
		coord_label.text = "Tile: --"

	# Update zoom label on any interaction
	_update_zoom_label()


func _on_tile_clicked(tile: Vector2i) -> void:
	selection_label.text = "Selected: (%d, %d)" % [tile.x, tile.y]
	
	# Handle tile selection through state controller
	_state_controller.on_tile_selected(tile)


func _on_agent_clicked(agent_id: int) -> void:
	selection_label.text = "Selected: Agent #%d" % agent_id
	
	# Handle agent selection through state controller
	_state_controller.on_agent_selected(agent_id)


func _on_overlay_changed() -> void:
	# Handle overlay changes through map controller
	_map_controller.on_overlay_changed()


# This method is now handled by PanelController

func _on_metrics_button_toggled(pressed: bool) -> void:
	_panel_controller.set_panel_visible("metrics", pressed)
	if pressed:
		_panel_controller.update_panel("metrics") # Force update when opening

func _on_metrics_panel_visibility_changed() -> void:
	# Sync button state if panel is closed via its own close button
	if metrics_button.button_pressed != right_panel.visible:
		metrics_button.set_pressed_no_signal(right_panel.visible)

func _on_jump_to_tick_requested(tick: int) -> void:
	_state_controller.jump_to_tick(tick)

func _on_compare_requested() -> void:
	compare_view.visible = not compare_view.visible

## Input handling for keyboard shortcuts
func _input(event: InputEvent) -> void:
	# Handle keyboard shortcuts
	if not event.pressed:
		return
		
	# Space: pause/play toggle
	if event.keycode == KEY_SPACE:
		if _state_controller.is_simulation_running():
			_state_controller.set_speed(0)
			_toast_manager.show_info("Simulation paused")
		else:
			_state_controller.set_speed(1)
			_toast_manager.show_info("Simulation resumed")
		get_viewport().set_input_as_handled()
	
	# S: step tick
	elif event.keycode == KEY_S:
		_state_controller.step_ticks(1)
		_toast_manager.show_info("Stepped 1 tick")
		get_viewport().set_input_as_handled()
	
	# D: step day
	elif event.keycode == KEY_D:
		_state_controller.step_day()
		_toast_manager.show_info("Stepped 1 day")
		get_viewport().set_input_as_handled()
	
	# M: toggle metrics panel
	elif event.keycode == KEY_M:
		var metrics_visible = right_panel.visible
		_panel_controller.set_panel_visible("metrics", not metrics_visible)
		_toast_manager.show_info("Metrics panel " + ("shown" if not metrics_visible else "hidden"))
		get_viewport().set_input_as_handled()
	
	# 1-4: switch tabs
	elif event.keycode >= KEY_1 and event.keycode <= KEY_4:
		var tab_index = event.keycode - KEY_1
		_panel_controller.set_current_tab(tab_index)
		var tab_names = ["Market", "Factions", "Contracts", "Timeline"]
		if tab_index < tab_names.size():
			_toast_manager.show_info("Switched to " + tab_names[tab_index] + " tab")
		get_viewport().set_input_as_handled()
	
	# Escape: clear selection
	elif event.keycode == KEY_ESCAPE:
		_clear_selection()
		_toast_manager.show_info("Selection cleared")
		get_viewport().set_input_as_handled()


## Clear current selection
func _clear_selection() -> void:
	var selection = _map_controller.get_selection()
	selection.clear()
	inspector_panel.agent_inspector.clear()
	inspector_panel.tile_inspector.clear()
	_map_controller.clear_agent_target()
	selection_label.text = "Selected: --"
	coord_label.text = "Tile: --"


## Additional signal handlers for controller events
func _on_speed_changed(speed: int) -> void:
	# Speed changes are handled by state controller
	pass

func _on_day_changed(day: int) -> void:
	# Day changes are handled by state controller  
	pass
