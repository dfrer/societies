## StateController - Manages simulation state and UI coordination
## Handles high-level state management and coordination between components

class_name StateController
extends RefCounted

## Signals for state changes
signal simulation_started(seed: int)
signal simulation_loaded(path: String)
signal simulation_state_updated(snapshot: Dictionary)
signal simulation_error(message: String)
signal speed_changed(speed: int)
signal day_changed(day: int)

## Component references
var _sim_runner: SimRunnerNode
var _top_bar: TopBar
var _time_controls: TimeControls
var _map_controller: MapViewController
var _panel_controller: PanelController
var _inspector_panel: InspectorPanel

## Update management
var _update_throttler: UpdateThrottler


func _init(
	sim_runner: SimRunnerNode,
	top_bar: TopBar,
	time_controls: TimeControls,
	map_controller: MapViewController,
	panel_controller: PanelController,
	inspector_panel: InspectorPanel,
	update_throttler: UpdateThrottler
) -> void:
	_sim_runner = sim_runner
	_top_bar = top_bar
	_time_controls = time_controls
	_map_controller = map_controller
	_panel_controller = panel_controller
	_inspector_panel = inspector_panel
	_update_throttler = update_throttler


## Start a new simulation run
func start_new_run(seed_value: int) -> void:
	_sim_runner.new_run(seed_value)
	_sim_runner.set_speed(0)  # Start paused
	
	# Enable UI controls
	_time_controls.set_enabled(true)
	_time_controls.set_current_speed(0)
	_top_bar.set_save_enabled(true)
	
	# Initialize map and panels
	_map_controller.force_update_all()
	_panel_controller.force_update_all()
	
	simulation_started.emit(seed_value)


## Load simulation state from file
func load_state(path: String) -> void:
	if _sim_runner.load_state_json(path):
		_sim_runner.set_speed(0)  # Start paused after load
		_time_controls.set_enabled(true)
		_time_controls.set_current_speed(0)
		_top_bar.set_save_enabled(true)
		
		# Update components
		_map_controller.force_update_all()
		_panel_controller.force_update_all()
		
		simulation_loaded.emit(path)
	else:
		var error_msg = "Failed to load: %s" % path
		simulation_error.emit(error_msg)


## Save current simulation state
func save_state(path: String) -> void:
	if _sim_runner.save_state_json(path):
		# Success - could add notification here
		pass
	else:
		var error_msg = "Failed to save: %s" % path
		simulation_error.emit(error_msg)


## Handle simulation state changes (called by throttled updates)
func on_state_changed(snapshot: Dictionary) -> void:
	# Update UI components that need immediate updates
	_top_bar.update_state(snapshot)
	_time_controls.set_current_day(snapshot.get("day", 0))
	_time_controls.set_current_speed(snapshot.get("speed", 0))
	
	# Request throttled updates for expensive operations
	_update_throttler.request_update("map_data")
	_update_throttler.request_update("panels")
	_update_throttler.request_update("inspector")
	
	# Emit state change signal
	simulation_state_updated.emit(snapshot)
	
	# Emit specific change signals
	var current_speed = snapshot.get("speed", 0)
	if current_speed != _time_controls.get_current_speed():
		speed_changed.emit(current_speed)
	
	var current_day = snapshot.get("day", 0)
	if current_day != _time_controls.get_current_day():
		day_changed.emit(current_day)


## Process throttled updates
func process_updates(delta: float) -> void:
	if _update_throttler.has_pending_updates():
		var updates = _update_throttler.process_updates(delta)
		
		for update_type in updates:
			match update_type:
				"map_data":
					_map_controller.update_map_data()
					_map_controller.update_agents()
					_map_controller.update_snapshot()
				"panels":
					_panel_controller.update_panels()
				"inspector":
					_update_inspector_if_needed()


## Handle run started event
func on_run_started(seed_value: int) -> void:
	_top_bar.set_status_paused()
	_map_controller.update_world_size()
	_map_controller.force_update_all()
	_panel_controller.force_update_all()
	
	simulation_started.emit(seed_value)


## Handle run loaded event
func on_run_loaded(path: String) -> void:
	_top_bar.set_status_paused()
	_map_controller.update_world_size()
	_map_controller.force_update_all()
	_panel_controller.force_update_all()
	
	simulation_loaded.emit(path)


## Handle simulation errors
func on_error_occurred(message: String) -> void:
	simulation_error.emit(message)


## Set simulation speed
func set_speed(speed: int) -> void:
	_sim_runner.set_speed(speed)
	_time_controls.set_current_speed(speed)
	
	if speed == 0:
		_top_bar.set_status_paused()
	else:
		_top_bar.set_status_running()
	
	speed_changed.emit(speed)


## Step simulation by specified ticks
func step_ticks(ticks: int) -> void:
	_sim_runner.set_speed(0)
	_time_controls.set_current_speed(0)
	_sim_runner.step_ticks(ticks)


## Step simulation by one day
func step_day() -> void:
	_sim_runner.set_speed(0)
	_time_controls.set_current_speed(0)
	_sim_runner.step_day()


## Jump to specific day
func jump_to_day(day: int) -> void:
	_sim_runner.set_speed(0)
	_time_controls.set_current_speed(0)
	_sim_runner.jump_to_day(day)


## Jump to specific tick
func jump_to_tick(tick: int) -> void:
	_sim_runner.jump_to_tick(tick)
	_time_controls.set_current_speed(0)
	_top_bar.set_status_paused()


## Update inspector if agent is selected
func _update_inspector_if_needed() -> void:
	var selection = _map_controller.get_selection()
	if selection.has_agent_selection() and _sim_runner.sim != null:
		var agent_id: int = selection.selected_agent_id
		_inspector_panel.update_agent_selection(agent_id, _sim_runner.sim.state)
		
		var target_pos: Vector2 = _inspector_panel.get_agent_target_position()
		_map_controller.set_agent_target(target_pos)


## Handle tile selection
func on_tile_selected(tile: Vector2i) -> void:
	if _sim_runner.sim != null:
		var world: RefCounted = _sim_runner.sim.state.world
		var owner_id: int = world.get_claim_owner(tile.x, tile.y)
		
		# Update the inspector with full tile information
		_inspector_panel.update_selection(tile, _sim_runner.sim.state)
		
		# If no agent selection exists, clear the agent inspector
		var selection = _map_controller.get_selection()
		if not selection.has_agent_selection():
			_inspector_panel.agent_inspector.clear()
			_map_controller.clear_agent_target()


## Handle agent selection
func on_agent_selected(agent_id: int) -> void:
	if _sim_runner.sim != null:
		# Update agent inspector
		_inspector_panel.update_agent_selection(agent_id, _sim_runner.sim.state)
		
		# Update target line
		var target_pos: Vector2 = _inspector_panel.get_agent_target_position()
		_map_controller.set_agent_target(target_pos)


## Get current simulation state
func get_sim_state() -> SimState:
	return _sim_runner.sim.state if _sim_runner.sim else null


## Check if simulation is running
func is_simulation_running() -> bool:
	return _sim_runner.sim != null and _sim_runner.current_speed > 0


## Get simulation statistics
func get_simulation_stats() -> Dictionary:
	if _sim_runner.sim == null:
		return {}
	
	return {
		"seed": _sim_runner.current_seed,
		"tick": _sim_runner.sim.state.tick,
		"day": _sim_runner.sim.state.day,
		"speed": _sim_runner.current_speed,
		"agent_count": _sim_runner.sim.state.agents.size()
	}
