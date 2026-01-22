## Structures - registry for world structures (stockpiles, etc.)
class_name Structures
extends RefCounted

var next_structure_id: int = 1
var structures: Array = [] # Array[StructureState]

func add_stockpile(pos_x: int, pos_y: int, owner_id: int, capacity: int) -> StructureState:
	var state := StructureState.new()
	state.id = next_structure_id
	next_structure_id += 1
	state.structure_type = StructureState.TYPE_STOCKPILE
	state.pos_x = pos_x
	state.pos_y = pos_y
	state.owner_id = owner_id
	state.capacity = capacity
	structures.append(state)
	return state

func add_shelter(pos_x: int, pos_y: int, owner_id: int, capacity: int) -> StructureState:
	var state := StructureState.new()
	state.id = next_structure_id
	next_structure_id += 1
	state.structure_type = StructureState.TYPE_SHELTER
	state.pos_x = pos_x
	state.pos_y = pos_y
	state.owner_id = owner_id
	state.capacity = capacity
	structures.append(state)
	return state

func get_structure(structure_id: int) -> StructureState:
	for structure in structures:
		if structure.id == structure_id:
			return structure
	return null

func get_stockpiles_sorted() -> Array:
	var result := []
	for structure in structures:
		if structure.structure_type == StructureState.TYPE_STOCKPILE:
			result.append(structure)
	result.sort_custom(func(a, b): return a.id < b.id)
	return result

func get_stockpiles_for_owner(owner_id: int) -> Array:
	var result := []
	for structure in structures:
		if structure.structure_type != StructureState.TYPE_STOCKPILE:
			continue
		if owner_id > 0 and structure.owner_id != owner_id:
			continue
		result.append(structure)
	result.sort_custom(func(a, b): return a.id < b.id)
	return result

func find_stockpile_with_item(item: String, qty: int = 1) -> StructureState:
	var stockpiles := get_stockpiles_sorted()
	for structure in stockpiles:
		if structure.get_available_item(item) >= qty:
			return structure
	return null

func to_dict() -> Dictionary:
	var data := []
	for structure in structures:
		data.append(structure.to_dict())
	return {
		"next_structure_id": next_structure_id,
		"structures": data
	}

static func from_dict(d: Dictionary) -> Structures:
	var system := Structures.new()
	system.next_structure_id = int(d.get("next_structure_id", 1))
	system.structures = []
	for structure_data in d.get("structures", []):
		system.structures.append(StructureState.from_dict(structure_data))
	return system
