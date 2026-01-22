## CommunalProject - represents a faction cooperative construction project
## Faction members can contribute resources to complete shared infrastructure
class_name CommunalProject
extends RefCounted

const STATUS_COLLECTING := "collecting"  # Waiting for resources
const STATUS_BUILDING := "building"      # Construction in progress
const STATUS_COMPLETE := "complete"      # Done
const STATUS_ABANDONED := "abandoned"    # Project was abandoned

var id: int = 0
var project_type: String = "workshop"  # "workshop", "road", "farm", "wall"
var faction_id: int = 0
var pos_x: int = 0
var pos_y: int = 0
var required_resources: Dictionary = {}  # {"Planks": 20, "MetalIngot": 5}
var contributed: Dictionary = {}         # {"Planks": 10, "MetalIngot": 2}
var contributors: Array = []             # Agent IDs who contributed, sorted
var reserved_resources: Dictionary = {}
var status: String = STATUS_COLLECTING
var started_tick: int = 0
var completion_tick: int = 0
var initiator_id: int = 0  # Agent who started the project
var build_progress: int = 0
var build_required: int = 0
var assigned_workers: Array = []

func _init() -> void:
	required_resources = {}
	contributed = {}
	contributors = []
	reserved_resources = {}
	assigned_workers = []

## Initialize a new project
func init_project(p_id: int, p_type: String, p_faction_id: int, 
				  p_pos_x: int, p_pos_y: int, p_initiator_id: int,
				  p_requirements: Dictionary, p_tick: int) -> void:
	id = p_id
	project_type = p_type
	faction_id = p_faction_id
	pos_x = p_pos_x
	pos_y = p_pos_y
	initiator_id = p_initiator_id
	required_resources = p_requirements.duplicate()
	started_tick = p_tick
	status = STATUS_COLLECTING
	
	# Initialize contributed to zero for all required resources
	for item in required_resources:
		contributed[item] = 0
		reserved_resources[item] = 0

## Contribute resources to the project
## Returns how many items were actually consumed
func contribute(agent_id: int, item: String, qty: int) -> int:
	if status != STATUS_COLLECTING:
		return 0
	
	if not required_resources.has(item):
		return 0
	
	var needed: int = required_resources[item] - contributed.get(item, 0)
	if needed <= 0:
		return 0
	
	var actual: int = mini(qty, needed)
	contributed[item] = contributed.get(item, 0) + actual
	if reserved_resources.has(item):
		reserved_resources[item] = maxi(0, reserved_resources.get(item, 0) - actual)
	
	# Track contributor
	if agent_id not in contributors:
		contributors.append(agent_id)
		contributors.sort()
	
	# Check if all resources collected
	if is_fully_funded():
		status = STATUS_BUILDING
	
	return actual

## Check if all required resources have been contributed
func is_fully_funded() -> bool:
	for item in required_resources:
		if contributed.get(item, 0) < required_resources[item]:
			return false
	return true

## Get remaining resources needed
func get_remaining_resources() -> Dictionary:
	var remaining := {}
	for item in required_resources:
		var need: int = required_resources[item] - contributed.get(item, 0) - reserved_resources.get(item, 0)
		if need > 0:
			remaining[item] = need
	return remaining

func reserve_resource(item: String, qty: int) -> int:
	if status != STATUS_COLLECTING:
		return 0
	if not required_resources.has(item):
		return 0
	var remaining: int = required_resources[item] - contributed.get(item, 0) - reserved_resources.get(item, 0)
	if remaining <= 0:
		return 0
	var to_reserve := mini(remaining, qty)
	reserved_resources[item] = reserved_resources.get(item, 0) + to_reserve
	return to_reserve

func release_reserved_resource(item: String, qty: int) -> int:
	var reserved: int = reserved_resources.get(item, 0)
	var to_release := mini(reserved, qty)
	if to_release <= 0:
		return 0
	reserved_resources[item] = reserved - to_release
	return to_release

## Mark as complete
func complete(tick: int) -> void:
	status = STATUS_COMPLETE
	completion_tick = tick

## Mark as abandoned
func abandon() -> void:
	status = STATUS_ABANDONED

## Check if project is active (collecting or building)
func is_active() -> bool:
	return status == STATUS_COLLECTING or status == STATUS_BUILDING

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"project_type": project_type,
		"faction_id": faction_id,
		"pos_x": pos_x,
		"pos_y": pos_y,
		"required_resources": required_resources.duplicate(),
		"contributed": contributed.duplicate(),
		"reserved_resources": reserved_resources.duplicate(),
		"contributors": contributors.duplicate(),
		"status": status,
		"started_tick": started_tick,
		"completion_tick": completion_tick,
		"initiator_id": initiator_id,
		"build_progress": build_progress,
		"build_required": build_required,
		"assigned_workers": assigned_workers.duplicate()
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> CommunalProject:
	var project := CommunalProject.new()
	project.id = int(d.get("id", 0))
	project.project_type = d.get("project_type", "workshop")
	project.faction_id = int(d.get("faction_id", 0))
	project.pos_x = int(d.get("pos_x", 0))
	project.pos_y = int(d.get("pos_y", 0))
	
	# Convert resource dicts to int values
	var req: Dictionary = d.get("required_resources", {})
	project.required_resources = {}
	for k in req:
		project.required_resources[k] = int(req[k])
	
	var contrib: Dictionary = d.get("contributed", {})
	project.contributed = {}
	for k in contrib:
		project.contributed[k] = int(contrib[k])

	var reserved: Dictionary = d.get("reserved_resources", {})
	project.reserved_resources = {}
	for k in reserved:
		project.reserved_resources[k] = int(reserved[k])
	
	# Convert contributors to int array
	var contribs: Array = d.get("contributors", [])
	project.contributors = []
	for c in contribs:
		project.contributors.append(int(c))
	
	project.status = d.get("status", STATUS_COLLECTING)
	project.started_tick = int(d.get("started_tick", 0))
	project.completion_tick = int(d.get("completion_tick", 0))
	project.initiator_id = int(d.get("initiator_id", 0))
	project.build_progress = int(d.get("build_progress", 0))
	project.build_required = int(d.get("build_required", 0))
	project.assigned_workers = []
	for worker_id in d.get("assigned_workers", []):
		project.assigned_workers.append(int(worker_id))
	
	return project
