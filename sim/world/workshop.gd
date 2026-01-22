## Workshop - crafting station for agents
class_name Workshop
extends RefCounted

## Unique identifier
var id: int = 0

## Position in world
var pos_x: int = 0
var pos_y: int = 0

## Agent who built this workshop
var built_by: int = 0

## Ownership and permissions
var owner_id: int = 0  # 0 = public, agent_id = personal, faction_id+offset = faction
var access_policy: String = "public"  # "public", "faction", "private"
var usage_fee: int = 0  # coins per use

## Workshop type and specialization
var workshop_type: String = "general"  # "general", "carpenter", "kiln", "smithy", "kitchen"
var efficiency_bonus: float = 1.0  # Multiplier for crafting speed

## Construction state
var build_start_tick: int = 0
var build_ticks_remaining: int = 0
var is_built: bool = false
var build_progress: int = 0  # Used by crafting.gd
var build_total: int = 0     # Used by crafting.gd

## Crafting queue
var queue: Array = []
var max_queue: int = 3

## Used for generating unique IDs - REMOVED for determinism
# static var _next_id: int = 1

func _init() -> void:
	pass


## Check if workshop is ready for use
func is_ready() -> bool:
	return is_built or build_ticks_remaining <= 0

## Get crafting time multiplier based on workshop type
func get_crafting_speed_bonus(recipe_type: String) -> float:
	match workshop_type:
		"carpenter":
			if "wood" in recipe_type.to_lower():
				return 0.8  # 20% faster for wood recipes
		"smithy":
			if "metal" in recipe_type.to_lower() or "tool" in recipe_type.to_lower():
				return 0.8  # 20% faster for metal/tool recipes
		"kitchen":
			if "food" in recipe_type.to_lower():
				return 0.7  # 30% faster for food recipes
	return 1.0

## Check if agent can use this workshop
func can_agent_use(agent: Agent, state: SimState) -> Dictionary:
	if not is_ready():
		return {"allowed": false, "reason": "Workshop not built"}
		
	# Public workshops can be used by anyone
	if access_policy == "public":
		return {"allowed": true, "reason": "Public access"}
	
	# Faction workshops can be used by faction members
	if access_policy == "faction":
		var workshop_faction_id = 0
		if World.is_faction_owner(owner_id):
			workshop_faction_id = World.faction_id_from_owner(owner_id)
		if agent.faction_id == workshop_faction_id:
			return {"allowed": true, "reason": "Faction member"}
		else:
			return {"allowed": false, "reason": "Faction only"}
	
	# Private workshops can only be used by owner
	if access_policy == "private":
		if agent.id == owner_id:
			return {"allowed": true, "reason": "Owner access"}
		else:
			return {"allowed": false, "reason": "Private workshop"}
	
	return {"allowed": false, "reason": "Unknown access policy"}

## Check if agent can afford usage fee
func can_afford_usage(agent: Agent) -> bool:
	return agent.get_available_money() >= usage_fee

## Check if queue has space
func has_queue_space() -> bool:
	return queue.size() < max_queue

## Add a crafting job to the queue
func add_job(job: Dictionary) -> void:
	if has_queue_space():
		queue.append(job)

## Get the next job without removing it
func peek_job() -> Dictionary:
	if queue.is_empty():
		return {}
	return queue[0]

## Get and remove the next job
func get_next_job() -> Dictionary:
	if queue.is_empty():
		return {}
	return queue.pop_front()

## Pop the first job (alias for get_next_job)
func pop_job() -> Dictionary:
	return get_next_job()

## Get current job without removing it (alias for peek_job)
func get_current_job() -> Dictionary:
	return peek_job()

## Process one tick of build time
func tick_build() -> void:
	if build_ticks_remaining > 0:
		build_ticks_remaining -= 1

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"id": id,
		"pos_x": pos_x,
		"pos_y": pos_y,
		"built_by": built_by,
		"build_start_tick": build_start_tick,
		"build_ticks_remaining": build_ticks_remaining,
		"is_built": is_built,
		"build_progress": build_progress,
		"build_total": build_total,
		"queue": queue.duplicate(true),
		"max_queue": max_queue,
		"owner_id": owner_id,
		"access_policy": access_policy,
		"usage_fee": usage_fee,
		"workshop_type": workshop_type,
		"efficiency_bonus": snappedf(efficiency_bonus, 0.00000001)
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Workshop:
	var workshop := Workshop.new()
	workshop.id = int(d.get("id", 0))
	workshop.pos_x = int(d.get("pos_x", 0))
	workshop.pos_y = int(d.get("pos_y", 0))
	workshop.built_by = int(d.get("built_by", 0))
	workshop.build_start_tick = int(d.get("build_start_tick", 0))
	workshop.build_ticks_remaining = int(d.get("build_ticks_remaining", 0))
	workshop.is_built = d.get("is_built", false)
	workshop.build_progress = int(d.get("build_progress", 0))
	workshop.build_total = int(d.get("build_total", 0))
	workshop.queue = d.get("queue", []).duplicate(true)
	workshop.max_queue = int(d.get("max_queue", 3))
	workshop.owner_id = int(d.get("owner_id", 0))
	workshop.access_policy = d.get("access_policy", "public")
	workshop.usage_fee = int(d.get("usage_fee", 0))
	workshop.workshop_type = d.get("workshop_type", "general")
	workshop.efficiency_bonus = float(d.get("efficiency_bonus", 1.0))
	return workshop
