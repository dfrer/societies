## Update Throttler - Controls UI update frequency to improve performance
## Limits updates to maximum frequency instead of updating every single frame

class_name UpdateThrottler
extends RefCounted

## Maximum updates per second for UI components
const MAX_UPDATES_PER_SECOND: int = 10

## Time between updates in seconds
const UPDATE_INTERVAL: float = 1.0 / MAX_UPDATES_PER_SECOND

## Pending updates that need to be processed
var _pending_updates: Array[String] = []

## Last time each update type was processed
var _last_update_times: Dictionary = {}

## Current accumulated time
var _accumulated_time: float = 0.0


## Add an update request to the queue
func request_update(update_type: String) -> void:
	if update_type not in _pending_updates:
		_pending_updates.append(update_type)


## Process pending updates based on elapsed time
func process_updates(delta: float) -> Array[String]:
	var updates_to_process: Array[String] = []
	_accumulated_time += delta
	
	# Check if enough time has passed for any updates
	var current_time = Time.get_ticks_msec() / 1000.0
	
	for update_type in _pending_updates.duplicate():
		var last_update = _last_update_times.get(update_type, 0.0)
		
		if current_time - last_update >= UPDATE_INTERVAL:
			updates_to_process.append(update_type)
			_pending_updates.erase(update_type)
			_last_update_times[update_type] = current_time
	
	return updates_to_process


## Force process all pending updates immediately (for manual refresh)
func force_process_all() -> Array[String]:
	var updates = _pending_updates.duplicate()
	_pending_updates.clear()
	
	var current_time = Time.get_ticks_msec() / 1000.0
	for update_type in updates:
		_last_update_times[update_type] = current_time
	
	return updates


## Check if any updates are pending
func has_pending_updates() -> bool:
	return not _pending_updates.is_empty()


## Clear all pending updates
func clear_pending() -> void:
	_pending_updates.clear()