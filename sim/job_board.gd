## JobBoard - deterministic registry of activities for agents
class_name JobBoard
extends RefCounted

const STATUS_AVAILABLE := "available"
const STATUS_CLAIMED := "claimed"
const STATUS_COMPLETED := "completed"
const STATUS_CANCELLED := "cancelled"

const ACTIVITY_GATHER_NODE := "GATHER_NODE"
const ACTIVITY_ACCEPT_CONTRACT := "ACCEPT_CONTRACT"

var next_activity_id: int = 1
var activities: Array = [] # Array[Dictionary]

## Activity schema:
## {
##   "activity_id": int,
##   "type": String,
##   "status": String,
##   "owner_id": int,
##   "worker_id": int,
##   "created_tick": int,
##   "updated_tick": int,
##   "data": Dictionary
## }

func post_activity(type: String, owner_id: int, data: Dictionary, created_tick: int) -> Dictionary:
	var activity := {
		"activity_id": next_activity_id,
		"type": type,
		"status": STATUS_AVAILABLE,
		"owner_id": owner_id,
		"worker_id": -1,
		"created_tick": created_tick,
		"updated_tick": created_tick,
		"data": data.duplicate(true)
	}
	next_activity_id += 1
	activities.append(activity)
	return activity

func post_gather_node(node_id: int, node_type: String, created_tick: int) -> Dictionary:
	return post_activity(ACTIVITY_GATHER_NODE, 0, {"node_id": node_id, "node_type": node_type}, created_tick)

func post_accept_contract(contract_id: int, created_tick: int) -> Dictionary:
	return post_activity(ACTIVITY_ACCEPT_CONTRACT, 0, {"contract_id": contract_id}, created_tick)

func get_activity(activity_id: int) -> Dictionary:
	for activity in activities:
		if activity.get("activity_id", -1) == activity_id:
			return activity
	return {}

func get_available_activities(type: String = "") -> Array:
	var result := []
	for activity in activities:
		if activity.get("status", STATUS_AVAILABLE) != STATUS_AVAILABLE:
			continue
		if type != "" and activity.get("type", "") != type:
			continue
		result.append(activity)
	result.sort_custom(func(a, b): return a.get("activity_id", 0) < b.get("activity_id", 0))
	return result

func claim_activity(activity_id: int, agent_id: int, current_tick: int) -> bool:
	for activity in activities:
		if activity.get("activity_id", -1) == activity_id:
			if activity.get("status", STATUS_AVAILABLE) != STATUS_AVAILABLE:
				return false
			activity["status"] = STATUS_CLAIMED
			activity["worker_id"] = agent_id
			activity["updated_tick"] = current_tick
			return true
	return false

func release_activity(activity_id: int, current_tick: int) -> void:
	for activity in activities:
		if activity.get("activity_id", -1) == activity_id:
			if activity.get("status", STATUS_CLAIMED) == STATUS_CLAIMED:
				activity["status"] = STATUS_AVAILABLE
				activity["worker_id"] = -1
				activity["updated_tick"] = current_tick
			return

func complete_activity(activity_id: int, current_tick: int) -> void:
	for activity in activities:
		if activity.get("activity_id", -1) == activity_id:
			activity["status"] = STATUS_COMPLETED
			activity["updated_tick"] = current_tick
			return

func cancel_activity(activity_id: int, current_tick: int) -> void:
	for activity in activities:
		if activity.get("activity_id", -1) == activity_id:
			activity["status"] = STATUS_CANCELLED
			activity["updated_tick"] = current_tick
			return

func has_activity_for_node(node_id: int) -> bool:
	for activity in activities:
		if activity.get("type", "") != ACTIVITY_GATHER_NODE:
			continue
		var data: Dictionary = activity.get("data", {})
		if int(data.get("node_id", -1)) == node_id and activity.get("status", "") != STATUS_CANCELLED:
			return true
	return false

func has_activity_for_contract(contract_id: int) -> bool:
	for activity in activities:
		if activity.get("type", "") != ACTIVITY_ACCEPT_CONTRACT:
			continue
		var data: Dictionary = activity.get("data", {})
		if int(data.get("contract_id", -1)) == contract_id and activity.get("status", "") != STATUS_CANCELLED:
			return true
	return false

func prune_inactive(max_inactive: int) -> void:
	if max_inactive <= 0:
		return
	var inactive := []
	for activity in activities:
		var status: String = activity.get("status", STATUS_AVAILABLE)
		if status == STATUS_COMPLETED or status == STATUS_CANCELLED:
			inactive.append(activity)
	inactive.sort_custom(func(a, b): return a.get("activity_id", 0) < b.get("activity_id", 0))
	if inactive.size() <= max_inactive:
		return
	var to_remove: Dictionary = {}
	var remove_count := inactive.size() - max_inactive
	for i in range(remove_count):
		to_remove[inactive[i].get("activity_id", 0)] = true
	var pruned := []
	for activity in activities:
		if to_remove.has(activity.get("activity_id", 0)):
			continue
		pruned.append(activity)
	activities = pruned

## Serialize to dictionary
func to_dict() -> Dictionary:
	var activities_data := []
	for activity in activities:
		activities_data.append(_sanitize_activity(activity))
	return {
		"next_activity_id": next_activity_id,
		"activities": activities_data
	}

func _sanitize_activity(activity: Dictionary) -> Dictionary:
	return {
		"activity_id": int(activity.get("activity_id", 0)),
		"type": activity.get("type", ""),
		"status": activity.get("status", STATUS_AVAILABLE),
		"owner_id": int(activity.get("owner_id", 0)),
		"worker_id": int(activity.get("worker_id", -1)),
		"created_tick": int(activity.get("created_tick", 0)),
		"updated_tick": int(activity.get("updated_tick", 0)),
		"data": _sanitize_activity_data(activity.get("data", {}))
	}

func _sanitize_activity_data(data: Dictionary) -> Dictionary:
	var fixed := {}
	for key in data:
		var val = data[key]
		if val is int or val is float:
			fixed[key] = int(val)
		elif val is bool:
			fixed[key] = bool(val)
		else:
			fixed[key] = val
	return fixed

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> JobBoard:
	var board := JobBoard.new()
	board.next_activity_id = int(d.get("next_activity_id", 1))
	board.activities = []
	for activity in d.get("activities", []):
		board.activities.append({
			"activity_id": int(activity.get("activity_id", 0)),
			"type": activity.get("type", ""),
			"status": activity.get("status", STATUS_AVAILABLE),
			"owner_id": int(activity.get("owner_id", 0)),
			"worker_id": int(activity.get("worker_id", -1)),
			"created_tick": int(activity.get("created_tick", 0)),
			"updated_tick": int(activity.get("updated_tick", 0)),
			"data": board._sanitize_activity_data(activity.get("data", {}))
		})
	return board
