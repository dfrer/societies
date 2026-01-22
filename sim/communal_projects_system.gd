## CommunalProjectsSystem - manages faction cooperative construction projects
class_name CommunalProjectsSystem
extends RefCounted

var next_project_id: int = 1
var projects: Array = []  # Array of CommunalProject

var stats: Dictionary = {
	"projects_started": 0,
	"projects_completed": 0,
	"projects_abandoned": 0,
	"resources_contributed": 0
}

## Project type definitions with their resource requirements
const PROJECT_TYPES := {
	"workshop": {"Planks": 15, "MetalIngot": 3},
	"farm": {"Planks": 10, "Berries": 5},
	"road": {"Planks": 8},
	"wall": {"Planks": 12, "MetalIngot": 2},
	"town_hall": {"Planks": 30, "MetalIngot": 10}
}

func _init() -> void:
	pass

## Start a new communal project
func start_project(faction_id: int, project_type: String, pos_x: int, pos_y: int,
				   initiator_id: int, current_tick: int, state: SimState) -> CommunalProject:
	if not PROJECT_TYPES.has(project_type):
		return null
	
	# Check if there's already a project at this location
	for project in projects:
		if project.pos_x == pos_x and project.pos_y == pos_y and project.is_active():
			return null
	
	var project := CommunalProject.new()
	var requirements := get_project_requirements(project_type)
	if requirements.is_empty():
		return null
	project.init_project(next_project_id, project_type, faction_id, pos_x, pos_y,
						 initiator_id, requirements, current_tick)
	next_project_id += 1
	projects.append(project)
	stats["projects_started"] += 1
	
	state.log_event("project_started", {
		"project_id": project.id,
		"type": project_type,
		"faction_id": faction_id,
		"initiator_id": initiator_id,
		"pos": [pos_x, pos_y]
	})
	
	return project

## Read-only access to project requirements for a given type
func get_project_requirements(project_type: String) -> Dictionary:
	if not PROJECT_TYPES.has(project_type):
		return {}
	var data: Dictionary = PROJECT_TYPES[project_type]
	return data.duplicate(true)

## Contribute resources to a project
func contribute_to_project(project_id: int, agent: Agent, item: String, qty: int,
						   state: SimState) -> int:
	var project := get_project(project_id)
	if project == null:
		return 0
	
	# Check agent has the items
	var available := agent.get_available_item(item)
	var to_contribute := mini(qty, available)
	if to_contribute <= 0:
		return 0
	
	# Contribute and consume from agent
	var consumed := project.contribute(agent.id, item, to_contribute)
	if consumed > 0:
		agent.remove_item(item, consumed)
		stats["resources_contributed"] += consumed
		
		state.log_event("project_contribution", {
			"project_id": project.id,
			"agent_id": agent.id,
			"item": item,
			"qty": consumed
		})
	
	return consumed

## Reserve resources for a project to avoid double-spending
func reserve_project_resources(project_id: int, item: String, qty: int) -> int:
	var project := get_project(project_id)
	if project == null:
		return 0
	return project.reserve_resource(item, qty)

## Release reserved resources for a project
func release_project_reservation(project_id: int, item: String, qty: int) -> int:
	var project := get_project(project_id)
	if project == null:
		return 0
	return project.release_reserved_resource(item, qty)

## Process building projects each tick
func process_building(world: World, state: SimState, current_tick: int, tuning: Dictionary) -> void:
	for project in projects:
		if project.status != CommunalProject.STATUS_BUILDING:
			continue

		if project.build_required <= 0:
			project.build_required = _get_build_ticks_for_project(project, tuning)
		project.build_progress += 1
		if project.build_progress >= project.build_required:
			_complete_project(project, world, state, current_tick)

## Complete a project and apply its effects
func _complete_project(project: CommunalProject, world: World, state: SimState, current_tick: int) -> void:
	match project.project_type:
		"workshop":
			var workshop := Workshop.new()
			workshop.pos_x = project.pos_x
			workshop.pos_y = project.pos_y
			workshop.built_by = project.initiator_id
			workshop.build_start_tick = project.started_tick
			workshop.build_ticks_remaining = 0  # Already built
			world.add_workshop(workshop)
		
		"farm":
			# Create a berry node at the location
			var node := ResourceNode.new()
			node.type = "berry"
			node.pos_x = project.pos_x
			node.pos_y = project.pos_y
			node.stock = 10
			node.max_stock = 15
			world.add_resource_node(node)
		
		"road":
			world.set_road(project.pos_x, project.pos_y, 1)
		
		"wall":
			# Walls could provide defense (not implemented yet)
			pass
			
		"town_hall":
			# Mark the faction home position with the town hall
			var faction = state.factions_system.get_faction(project.faction_id, state.factions)
			if faction:
				faction.home_pos = Vector2i(project.pos_x, project.pos_y)
	
	project.complete(current_tick)
	stats["projects_completed"] += 1
	
	state.log_event("project_completed", {
		"project_id": project.id,
		"type": project.project_type,
		"faction_id": project.faction_id,
		"contributors": project.contributors,
		"pos": [project.pos_x, project.pos_y]
	})

func _get_build_ticks_for_project(project: CommunalProject, tuning: Dictionary) -> int:
	var default_ticks: int = int(tuning.get("project_build_ticks_default", 8))
	if default_ticks <= 0:
		default_ticks = 1
	match project.project_type:
		"workshop":
			return maxi(1, int(tuning.get("project_build_ticks_workshop", default_ticks)))
		"farm":
			return maxi(1, int(tuning.get("project_build_ticks_farm", default_ticks)))
		"road":
			return maxi(1, int(tuning.get("project_build_ticks_road", default_ticks)))
		"wall":
			return maxi(1, int(tuning.get("project_build_ticks_wall", default_ticks)))
		"town_hall":
			return maxi(1, int(tuning.get("project_build_ticks_town_hall", default_ticks)))
	return default_ticks

## Get a project by ID
func get_project(project_id: int) -> CommunalProject:
	for project in projects:
		if project.id == project_id:
			return project
	return null

## Get active projects for a faction
func get_faction_projects(faction_id: int) -> Array:
	var result := []
	for project in projects:
		if project.faction_id == faction_id and project.is_active():
			result.append(project)
	return result

## Get projects near a position
func get_projects_near(pos_x: int, pos_y: int, radius: int) -> Array:
	var result := []
	for project in projects:
		var dist := absi(project.pos_x - pos_x) + absi(project.pos_y - pos_y)
		if dist <= radius and project.is_active():
			result.append(project)
	return result

## Abandon stale projects (no contributions for too long)
func abandon_stale_projects(current_tick: int, max_stale_ticks: int, state: SimState) -> void:
	for project in projects:
		if not project.is_active():
			continue
		if current_tick - project.started_tick > max_stale_ticks:
			# Check if any progress was made recently
			# For simplicity, just abandon if timeout exceeded
			project.abandon()
			stats["projects_abandoned"] += 1
			
			state.log_event("project_abandoned", {
				"project_id": project.id,
				"type": project.project_type,
				"faction_id": project.faction_id
			})

## Serialize to dictionary
func to_dict() -> Dictionary:
	var projects_data := []
	for project in projects:
		projects_data.append(project.to_dict())
	
	return {
		"next_project_id": next_project_id,
		"projects": projects_data,
		"stats": stats.duplicate()
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> CommunalProjectsSystem:
	var system := CommunalProjectsSystem.new()
	system.next_project_id = int(d.get("next_project_id", 1))
	
	system.projects = []
	for project_data in d.get("projects", []):
		system.projects.append(CommunalProject.from_dict(project_data))
	
	var stats_data: Dictionary = d.get("stats", {
		"projects_started": 0,
		"projects_completed": 0,
		"projects_abandoned": 0,
		"resources_contributed": 0
	})
	system.stats = {}
	for key in stats_data:
		system.stats[key] = int(stats_data[key])
	
	return system
