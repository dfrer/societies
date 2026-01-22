class_name SimRunnerNode
extends Node

## Node that owns and drives a Sim instance for the visualizer.
## Does not modify sim behavior - only calls Sim.step() and exposes state.

const Sim := preload("res://sim/sim.gd")
const Serializers := preload("res://sim/serializers.gd")

signal state_changed(snapshot: Dictionary)
signal run_started(seed_value: int)
signal run_loaded(path: String)
signal error_occurred(message: String)

## The Sim instance (headless simulation kernel)
var sim: RefCounted = null

## Current speed multiplier: 0 = paused, 1 = 1x, 5 = 5x, 20 = 20x
var speed: int = 0

## Ticks per day (cached from tuning after init)
var ticks_per_day: int = 24

## How often to emit state_changed signal (in ticks)
@export var ui_update_interval_ticks: int = 1

## How often to compute checksum for UI snapshots (in ticks)
@export var checksum_interval_ticks: int = 24

## Maximum ticks to process per frame to avoid freezing
@export var max_ticks_per_frame: int = 100

## Time budget per frame for stepping (in milliseconds)
@export var frame_budget_ms: int = 10

## Internal state
var _tick_accumulator: float = 0.0
var _ticks_since_ui_update: int = 0
var _last_emitted_tick: int = -1
var _last_checksum_tick: int = -1

## Base tick rate (ticks per second at 1x speed)
@export var base_ticks_per_second: float = 24.0

var current_seed: int = 0


func _ready() -> void:
	set_process(false)


func _process(delta: float) -> void:
	if sim == null or speed == 0:
		return

	# Accumulate time and convert to ticks
	_tick_accumulator += delta * base_ticks_per_second * speed

	# Calculate how many ticks to run this frame
	var ticks_to_run: int = int(_tick_accumulator)
	if ticks_to_run <= 0:
		return

	# Clamp to avoid freezing on high speeds
	ticks_to_run = mini(ticks_to_run, max_ticks_per_frame)

	var start_ms: int = Time.get_ticks_msec()
	var ticks_run: int = 0
	while ticks_run < ticks_to_run:
		if Time.get_ticks_msec() - start_ms >= frame_budget_ms:
			break
		sim.step(1)
		ticks_run += 1

	if ticks_run == 0:
		# Cap accumulator to prevent runaway accumulation
		_tick_accumulator = minf(_tick_accumulator, float(max_ticks_per_frame))
		return

	_tick_accumulator -= ticks_run

	# Cap accumulator to prevent runaway accumulation
	_tick_accumulator = minf(_tick_accumulator, float(max_ticks_per_frame))

	# Track ticks processed this frame
	_ticks_since_ui_update += ticks_run

	# Emit state update if enough ticks have passed
	if _ticks_since_ui_update >= ui_update_interval_ticks:
		_emit_state_changed()
		_ticks_since_ui_update = 0


## Start a new simulation run with the given seed
func new_run(seed_value: int) -> void:
	current_seed = seed_value
	sim = Sim.new()
	sim.init_new(seed_value)
	_cache_tuning()
	_reset_accumulators()
	set_process(true)
	run_started.emit(seed_value)
	_emit_state_changed()


## Load simulation state from a JSON file
func load_state_json(path: String) -> bool:
	var new_sim = Sim.new()
	if not new_sim.load_json(path):
		error_occurred.emit("Failed to load state from: " + path)
		return false

	sim = new_sim
	_cache_tuning()
	_reset_accumulators()
	set_process(true)
	run_loaded.emit(path)
	_emit_state_changed()
	return true


## Save current simulation state to a JSON file
func save_state_json(path: String) -> bool:
	if sim == null:
		error_occurred.emit("No simulation to save")
		return false

	if not sim.save_json(path):
		error_occurred.emit("Failed to save state to: " + path)
		return false

	return true


## Step the simulation by a specific number of ticks (manual step)
func step_ticks(n: int) -> void:
	if sim == null:
		return

	sim.step(n)
	_emit_state_changed()


## Step the simulation by one full day
func step_day() -> void:
	if sim == null:
		return

	var current_tick: int = sim.get_tick()
	var current_day: int = current_tick / ticks_per_day
	var next_day_tick: int = (current_day + 1) * ticks_per_day
	var ticks_to_step: int = next_day_tick - current_tick

	sim.step(ticks_to_step)
	_emit_state_changed()


## Jump to a specific day (steps until that day is reached)
func jump_to_day(target_day: int) -> void:
	if sim == null:
		return

	var current_tick: int = sim.get_tick()
	var target_tick: int = target_day * ticks_per_day

	if target_tick <= current_tick:
		error_occurred.emit("Cannot jump backwards in time")
		return

	var ticks_to_step: int = target_tick - current_tick

	# Step in chunks to avoid freezing and allow UI updates
	var chunk_size: int = ticks_per_day
	while ticks_to_step > 0:
		var this_chunk: int = mini(ticks_to_step, chunk_size)
		sim.step(this_chunk)
		ticks_to_step -= this_chunk
		_emit_state_changed()
		# Allow frame to process (non-blocking jump)
		await get_tree().process_frame

	_emit_state_changed()


## Jump to a specific tick (steps or restarts until that tick is reached)
func jump_to_tick(target_tick: int) -> void:
	if sim == null:
		return
	
	var current_tick: int = sim.get_tick()
	
	if target_tick < current_tick:
		# Restart
		new_run(current_seed)
		# new_run emits state changed, so we are at tick 0
		current_tick = 0
		
	var ticks_to_step: int = target_tick - current_tick
	
	if ticks_to_step <= 0:
		return
	
	# Step in chunks to avoid freezing and allow UI updates
	var chunk_size: int = ticks_per_day * 5 # Faster chunks for raw tick jump
	
	# Disable processing while fast forwarding
	set_process(false)
	
	while ticks_to_step > 0:
		var this_chunk: int = mini(ticks_to_step, chunk_size)
		sim.step(this_chunk)
		ticks_to_step -= this_chunk
		
		# Only emit/yield occasionally to keep UI responsive but fast
		if ticks_to_step % (chunk_size * 2) == 0:
			_emit_state_changed()
			await get_tree().process_frame
	
	# Re-enable processing if speed > 0 (handled by set_speed usually, but we are in manual mode)
	if speed > 0:
		set_process(true)
		
	_emit_state_changed()


## Set the simulation speed multiplier
## 0 = paused, 1 = 1x, 5 = 5x, 20 = 20x
func set_speed(mult: int) -> void:
	speed = clampi(mult, 0, 20)
	if speed == 0:
		_tick_accumulator = 0.0


## Get current tick
func get_tick() -> int:
	if sim == null:
		return 0
	return sim.get_tick()


## Get current day
func get_day() -> int:
	if sim == null:
		return 0
	return sim.get_tick() / ticks_per_day


## Get current checksum
func get_checksum() -> String:
	if sim == null:
		return ""
	return sim.checksum()


## Get average hunger across all agents
func get_average_hunger() -> float:
	if sim == null:
		return 0.0
	return sim.get_average_hunger()


## Get alive agent count
func get_alive_agent_count() -> int:
	if sim == null:
		return 0
	return sim.get_alive_agent_count()


## Get total agent count
func get_agent_count() -> int:
	if sim == null:
		return 0
	return sim.get_agent_count()


## Check if simulation is running (has been initialized)
func is_running() -> bool:
	return sim != null


## Check if simulation is paused
func is_paused() -> bool:
	return speed == 0


## Build and emit a state snapshot
func _emit_state_changed() -> void:
	if sim == null:
		return

	var current_tick: int = sim.get_tick()
	if current_tick == _last_emitted_tick:
		return

	_last_emitted_tick = current_tick

	var checksum_value := ""
	if self.checksum_interval_ticks > 0:
		if _last_checksum_tick < 0 or (current_tick - _last_checksum_tick) >= self.checksum_interval_ticks:
			checksum_value = sim.checksum()
			_last_checksum_tick = current_tick

	var snapshot := {
		"tick": current_tick,
		"day": current_tick / ticks_per_day,
		"checksum": checksum_value,
		"speed": speed,
		"alive_agents": sim.get_alive_agent_count(),
		"total_agents": sim.get_agent_count(),
		"avg_hunger": sim.get_average_hunger(),
		"avg_pollution": sim.get_average_pollution(),
		"total_trades": sim.get_total_trades(),
	}

	state_changed.emit(snapshot)


## Cache tuning values after init/load
func _cache_tuning() -> void:
	if sim == null:
		return
	ticks_per_day = sim.state.tuning.get("ticks_per_day", 24)


## Reset accumulators for a new run
func _reset_accumulators() -> void:
	_tick_accumulator = 0.0
	_ticks_since_ui_update = 0
	_last_emitted_tick = -1
	_last_checksum_tick = -1
