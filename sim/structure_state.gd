## StructureState - generic structure data container
class_name StructureState
extends RefCounted

const TYPE_STOCKPILE := "stockpile"
const TYPE_SHELTER := "shelter"

var id: int = 0
var structure_type: String = TYPE_STOCKPILE
var pos_x: int = 0
var pos_y: int = 0
var owner_id: int = 0
var access_policy: String = "organization"
var capacity: int = 0
var items: Dictionary = {}
var reserved_items: Dictionary = {}

func _init() -> void:
	items = {}
	reserved_items = {}

func get_available_item(item: String) -> int:
	var total: int = int(items.get(item, 0))
	var reserved: int = int(reserved_items.get(item, 0))
	return maxi(0, total - reserved)

func add_item(item: String, qty: int) -> void:
	if qty <= 0:
		return
	items[item] = int(items.get(item, 0)) + qty

func remove_item(item: String, qty: int) -> int:
	if qty <= 0:
		return 0
	var available: int = int(items.get(item, 0))
	var to_remove := mini(available, qty)
	if to_remove <= 0:
		return 0
	items[item] = available - to_remove
	if items[item] <= 0:
		items.erase(item)
	return to_remove

func reserve_item(item: String, qty: int) -> int:
	if qty <= 0:
		return 0
	var available: int = get_available_item(item)
	var to_reserve := mini(available, qty)
	if to_reserve <= 0:
		return 0
	reserved_items[item] = int(reserved_items.get(item, 0)) + to_reserve
	return to_reserve

func release_reserved_item(item: String, qty: int) -> int:
	if qty <= 0:
		return 0
	var reserved: int = int(reserved_items.get(item, 0))
	var to_release := mini(reserved, qty)
	if to_release <= 0:
		return 0
	reserved_items[item] = reserved - to_release
	if reserved_items[item] <= 0:
		reserved_items.erase(item)
	return to_release

func get_total_item_count() -> int:
	var total := 0
	for key in items:
		total += int(items[key])
	return total

func get_free_capacity() -> int:
	if capacity <= 0:
		return 999999
	return maxi(0, capacity - get_total_item_count())

func to_dict() -> Dictionary:
	return {
		"id": id,
		"structure_type": structure_type,
		"pos_x": pos_x,
		"pos_y": pos_y,
		"owner_id": owner_id,
		"access_policy": access_policy,
		"capacity": capacity,
		"items": _sanitize_item_dict(items),
		"reserved_items": _sanitize_item_dict(reserved_items)
	}

static func from_dict(d: Dictionary) -> StructureState:
	var state := StructureState.new()
	state.id = int(d.get("id", 0))
	state.structure_type = d.get("structure_type", TYPE_STOCKPILE)
	state.pos_x = int(d.get("pos_x", 0))
	state.pos_y = int(d.get("pos_y", 0))
	state.owner_id = int(d.get("owner_id", 0))
	if d.has("access_policy"):
		state.access_policy = str(d.get("access_policy", "organization"))
	else:
		state.access_policy = state.owner_id == 0 ? "public" : "organization"
	state.capacity = int(d.get("capacity", 0))
	state.items = _sanitize_item_dict(d.get("items", {}))
	state.reserved_items = _sanitize_item_dict(d.get("reserved_items", {}))
	return state

static func _sanitize_item_dict(d: Dictionary) -> Dictionary:
	var result := {}
	for key in d:
		result[key] = int(d[key])
	return result
