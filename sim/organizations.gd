## Organization - shared planning entity for settlement-scale decisions
class_name Organization
extends RefCounted

var id: int = 0
var name: String = ""
var members: Array = [] # Array[int]
var stockpile_ids: Array = [] # Array[int]
var treasury: int = 0
var town_center: Vector2i = Vector2i.ZERO

var zoning_radii: Dictionary = {
	"town_center": 0,
	"shelter": 3,
	"stockpile": 5,
	"workshop": 7,
	"residential": 10
}

func _init() -> void:
	members = []
	stockpile_ids = []

func init_organization(org_id: int, org_name: String, center: Vector2i, seed_treasury: int) -> void:
	id = org_id
	name = org_name
	town_center = center
	treasury = seed_treasury

func add_member(agent_id: int) -> void:
	if agent_id in members:
		return
	members.append(agent_id)
	members.sort()

func remove_member(agent_id: int) -> void:
	var idx := members.find(agent_id)
	if idx >= 0:
		members.remove_at(idx)

func has_member(agent_id: int) -> bool:
	return agent_id in members

func add_stockpile_access(structure_id: int) -> void:
	if structure_id in stockpile_ids:
		return
	stockpile_ids.append(structure_id)
	stockpile_ids.sort()

func get_owner_id() -> int:
	return World.organization_owner_id(id)

func get_zone_for_distance(distance: int) -> String:
	if distance <= zoning_radii.get("town_center", 0):
		return "town_center"
	if distance <= zoning_radii.get("shelter", 3):
		return "shelter"
	if distance <= zoning_radii.get("stockpile", 5):
		return "stockpile"
	if distance <= zoning_radii.get("workshop", 7):
		return "workshop"
	return "residential"

func plan_daily(state: SimState) -> void:
	if members.is_empty():
		return
	var members_per_stockpile := state.get_tuning_int("organization_stockpile_members_per", 6)
	var members_per_workshop := state.get_tuning_int("organization_workshop_members_per", 10)
	var members_per_shelter := state.get_tuning_int("organization_shelter_members_per", 4)
	var stockpile_target := maxi(1, int(ceil(float(members.size()) / float(members_per_stockpile))))
	var workshop_target := maxi(1, int(ceil(float(members.size()) / float(members_per_workshop))))
	var shelter_target := maxi(1, int(ceil(float(members.size()) / float(members_per_shelter))))

	var owner_id := get_owner_id()
	var stockpile_count := _count_structures_of_type(state, StructureState.TYPE_STOCKPILE, owner_id)
	var shelter_count := _count_structures_of_type(state, StructureState.TYPE_SHELTER, owner_id)
	var workshop_count := _count_workshops(state, owner_id)

	var stockpile_projects := _count_active_projects(state, "stockpile")
	var workshop_projects := _count_active_projects(state, "workshop")
	var shelter_projects := _count_active_projects(state, "shelter")

	if stockpile_count + stockpile_projects < stockpile_target:
		_spawn_project(state, "stockpile", "stockpile")
	if workshop_count + workshop_projects < workshop_target:
		_spawn_project(state, "workshop", "workshop")
	if shelter_count + shelter_projects < shelter_target:
		_spawn_project(state, "shelter", "shelter")
	_plan_station_projects(state)
	_plan_road_projects(state)

func _count_structures_of_type(state: SimState, structure_type: String, owner_id: int) -> int:
	var count := 0
	for structure in state.structures.structures:
		if structure.structure_type == structure_type and structure.owner_id == owner_id:
			count += 1
	return count

func _count_workshops(state: SimState, owner_id: int) -> int:
	var count := 0
	for workshop in state.world.workshops:
		if workshop.owner_id == owner_id:
			count += 1
	return count

func _count_workshops_by_type(state: SimState, owner_id: int, workshop_type: String) -> int:
	var count := 0
	for workshop in state.world.workshops:
		if workshop.owner_id == owner_id and workshop.workshop_type == workshop_type:
			count += 1
	return count

func _count_active_projects(state: SimState, project_type: String) -> int:
	var count := 0
	for project in state.communal_projects.projects:
		if project.project_type != project_type:
			continue
		if project.faction_id != id:
			continue
		if project.is_active():
			count += 1
	return count

func _spawn_project(state: SimState, project_type: String, zone_tag: String) -> void:
	var site := _find_zoned_site(state, zone_tag)
	if site == Vector2i(-1, -1):
		return
	state.communal_projects.start_project(id, project_type, site.x, site.y, 0, state.tick, state)
	state.log_event("organization_project_planned", {
		"organization_id": id,
		"project_type": project_type,
		"pos": [site.x, site.y]
	})

func _find_zoned_site(state: SimState, zone_tag: String) -> Vector2i:
	var claims: Array = state.world.get_claims_by_owner().get(get_owner_id(), [])
	if claims.is_empty():
		return Vector2i(-1, -1)
	var candidates := []
	for pos in claims:
		if state.world.get_zone_tag(pos.x, pos.y) != zone_tag:
			continue
		if _is_tile_occupied(state, pos.x, pos.y):
			continue
		var dist := absi(pos.x - town_center.x) + absi(pos.y - town_center.y)
		candidates.append({"x": pos.x, "y": pos.y, "dist": dist})
	if candidates.is_empty():
		return Vector2i(-1, -1)
	candidates.sort_custom(func(a, b):
		if a["dist"] == b["dist"]:
			if a["x"] == b["x"]:
				return a["y"] < b["y"]
			return a["x"] < b["x"]
		return a["dist"] < b["dist"]
	)
	var best: Dictionary = candidates[0]
	return Vector2i(int(best["x"]), int(best["y"]))

func _is_tile_occupied(state: SimState, x: int, y: int) -> bool:
	for structure in state.structures.structures:
		if structure.pos_x == x and structure.pos_y == y:
			return true
	for workshop in state.world.workshops:
		if workshop.pos_x == x and workshop.pos_y == y:
			return true
	for project in state.communal_projects.projects:
		if project.is_active() and project.pos_x == x and project.pos_y == y:
			return true
	return false

func _plan_station_projects(state: SimState) -> void:
	var owner_id := get_owner_id()
	var carpenter_count := _count_workshops_by_type(state, owner_id, "carpenter")
	var kiln_count := _count_workshops_by_type(state, owner_id, "kiln")
	var smithy_count := _count_workshops_by_type(state, owner_id, "smithy")
	var carpenter_projects := _count_active_projects(state, "carpenter")
	var kiln_projects := _count_active_projects(state, "kiln")
	var smithy_projects := _count_active_projects(state, "smithy")

	var planks_target := state.get_tuning_int("organization_target_planks", 20)
	var metal_target := state.get_tuning_int("organization_target_metal_ingot", 6)
	var tools_target := state.get_tuning_int("organization_target_tools", 2)
	var planks := _get_stockpile_item_count(state, owner_id, "Planks")
	var ore := _get_stockpile_item_count(state, owner_id, "Ore")
	var metal := _get_stockpile_item_count(state, owner_id, "MetalIngot")
	var tools := _get_stockpile_tool_count(state, owner_id)

	if carpenter_count + carpenter_projects < 1 and planks < planks_target:
		_spawn_project(state, "carpenter", "workshop")
		return
	if carpenter_count == 0 and carpenter_projects == 0:
		return
	if kiln_count + kiln_projects < 1 and (metal < metal_target or ore >= 4):
		_spawn_project(state, "kiln", "workshop")
		return
	if kiln_count == 0 and kiln_projects == 0:
		return
	if smithy_count + smithy_projects < 1 and (metal >= 2 or tools < tools_target):
		_spawn_project(state, "smithy", "workshop")

func _get_stockpile_item_count(state: SimState, owner_id: int, item: String) -> int:
	var total := 0
	var stockpiles := _get_org_stockpiles(state, owner_id)
	for stockpile in stockpiles:
		total += int(stockpile.items.get(item, 0))
	return total

func _get_stockpile_tool_count(state: SimState, owner_id: int) -> int:
	var tools := ["Axe", "Pickaxe", "Mallet", "Shovel"]
	var total := 0
	for tool in tools:
		total += _get_stockpile_item_count(state, owner_id, tool)
	return total

func _get_org_stockpiles(state: SimState, owner_id: int) -> Array:
	var stockpiles := []
	for structure in state.structures.structures:
		if structure.structure_type != StructureState.TYPE_STOCKPILE:
			continue
		if structure.owner_id != owner_id:
			continue
		stockpiles.append(structure)
	stockpiles.sort_custom(func(a, b): return a.id < b.id)
	return stockpiles

func _plan_road_projects(state: SimState) -> void:
	var daily_limit := state.get_tuning_int("organization_road_projects_per_day", 6)
	if daily_limit <= 0:
		return
	var owner_id := get_owner_id()
	var stockpiles := _get_org_stockpiles(state, owner_id)
	if stockpiles.is_empty():
		return
	var remaining := daily_limit
	var road_targets := _get_road_targets(state, stockpiles)
	for target in road_targets:
		if remaining <= 0:
			break
		remaining = _spawn_road_path(state, target["start"], target["end"], remaining)

func _get_road_targets(state: SimState, stockpiles: Array) -> Array:
	var targets := []
	for stockpile in stockpiles:
		targets.append({"start": town_center, "end": Vector2i(stockpile.pos_x, stockpile.pos_y)})
	var resource_types := ["tree", "ore", "stone", "berry"]
	for rtype in resource_types:
		var node := _find_nearest_resource_node(state, rtype, town_center)
		if node == null:
			continue
		var nearest_stockpile := _find_nearest_stockpile(stockpiles, Vector2i(node.pos_x, node.pos_y))
		if nearest_stockpile == null:
			continue
		targets.append({
			"start": Vector2i(nearest_stockpile.pos_x, nearest_stockpile.pos_y),
			"end": Vector2i(node.pos_x, node.pos_y)
		})
	return targets

func _find_nearest_resource_node(state: SimState, node_type: String, origin: Vector2i) -> ResourceNode:
	var best: ResourceNode = null
	var best_dist := 999999
	for node in state.world.resource_nodes:
		if node.type != node_type:
			continue
		var dist := absi(node.pos_x - origin.x) + absi(node.pos_y - origin.y)
		if dist < best_dist:
			best_dist = dist
			best = node
	return best

func _find_nearest_stockpile(stockpiles: Array, origin: Vector2i) -> StructureState:
	var best: StructureState = null
	var best_dist := 999999
	for stockpile in stockpiles:
		var dist := absi(stockpile.pos_x - origin.x) + absi(stockpile.pos_y - origin.y)
		if dist < best_dist:
			best_dist = dist
			best = stockpile
	return best

func _spawn_road_path(state: SimState, start: Vector2i, end: Vector2i, remaining: int) -> int:
	var path := _build_manhattan_path(start, end)
	for pos in path:
		if remaining <= 0:
			break
		if not state.world.is_valid(pos.x, pos.y):
			continue
		if state.world.has_road(pos.x, pos.y):
			continue
		if _is_tile_occupied(state, pos.x, pos.y):
			continue
		if _has_active_project_at(state, pos.x, pos.y):
			continue
		state.communal_projects.start_project(id, "road", pos.x, pos.y, 0, state.tick, state)
		remaining -= 1
	return remaining

func _build_manhattan_path(start: Vector2i, end: Vector2i) -> Array:
	var path := []
	var x := start.x
	var y := start.y
	while x != end.x:
		path.append(Vector2i(x, y))
		x += 1 if end.x > x else -1
	while y != end.y:
		path.append(Vector2i(x, y))
		y += 1 if end.y > y else -1
	path.append(Vector2i(x, y))
	return path

func _has_active_project_at(state: SimState, x: int, y: int) -> bool:
	for project in state.communal_projects.projects:
		if not project.is_active():
			continue
		if project.pos_x == x and project.pos_y == y:
			return true
	return false

func to_dict() -> Dictionary:
	return {
		"id": id,
		"name": name,
		"members": members.duplicate(),
		"stockpile_ids": stockpile_ids.duplicate(),
		"treasury": treasury,
		"town_center_x": town_center.x,
		"town_center_y": town_center.y,
		"zoning_radii": zoning_radii.duplicate(true)
	}

static func from_dict(d: Dictionary) -> Organization:
	var org := Organization.new()
	org.id = int(d.get("id", 0))
	org.name = d.get("name", "")
	org.members = []
	for member_id in d.get("members", []):
		org.members.append(int(member_id))
	org.stockpile_ids = []
	for structure_id in d.get("stockpile_ids", []):
		org.stockpile_ids.append(int(structure_id))
	org.treasury = int(d.get("treasury", 0))
	org.town_center = Vector2i(int(d.get("town_center_x", 0)), int(d.get("town_center_y", 0)))
	org.zoning_radii = d.get("zoning_radii", org.zoning_radii).duplicate(true)
	return org
