## MapViewController - Handles map-related UI logic and rendering coordination
## Separates map management from the main visualizer class

class_name MapViewController
extends RefCounted

## Signals for map events
signal map_data_updated(world_data: Dictionary)
signal agents_updated(agents: Array)
signal snapshot_updated(snapshot: Dictionary)
signal overlay_changed()

## Map view reference
var _map_view: MapView

## Sim runner reference for data access
var _sim_runner: SimRunnerNode

## Cached data to prevent unnecessary updates
var _last_agents_hash: int = 0
var _last_snapshot_hash: int = 0
var _world_data_initialized: bool = false


func _init(map_view: MapView, sim_runner: SimRunnerNode) -> void:
	_map_view = map_view
	_sim_runner = sim_runner


## Update world map with current simulation data
func update_map_data() -> void:
	if _sim_runner.sim == null:
		return

	var world: RefCounted = _sim_runner.sim.state.world
	var state: SimState = _sim_runner.sim.state
	if not _world_data_initialized:
		var world_data := _build_full_world_data(world)
		_map_view.update_world_data(world_data)
		_world_data_initialized = true
		world.consume_dirty_claim_tiles()
		world.consume_dirty_pollution_tiles()
		_map_view.update_projects_data(_build_projects_data(state))
		_map_view.update_tasks_data(_build_tasks_data(state))
		_map_view.update_stockpiles_data(_build_stockpiles_data(state))
		map_data_updated.emit(world_data)
		return

	var dirty_claims: Array = world.consume_dirty_claim_tiles()
	var dirty_pollution: Array = world.consume_dirty_pollution_tiles()

	var claims_delta: Dictionary = {}
	for tile in dirty_claims:
		var owner_id: int = world.get_claim_owner(tile.x, tile.y)
		claims_delta["%d,%d" % [tile.x, tile.y]] = owner_id

	var pollution_delta: Dictionary = {}
	for tile in dirty_pollution:
		var p: float = world.get_pollution(tile.x, tile.y)
		pollution_delta["%d,%d" % [tile.x, tile.y]] = p

	if not claims_delta.is_empty() or not pollution_delta.is_empty():
		_map_view.update_world_tiles(claims_delta, pollution_delta)

	var resource_nodes := _build_resource_nodes_data(world)
	var workshops := _build_workshops_data(world)
	_map_view.update_static_world_data(resource_nodes, workshops)
	_map_view.update_projects_data(_build_projects_data(state))
	_map_view.update_tasks_data(_build_tasks_data(state))
	_map_view.update_stockpiles_data(_build_stockpiles_data(state))


## Update agents display
func update_agents() -> void:
	if _sim_runner.sim == null:
		return

	# Build agents data
	var agents_data: Array = []
	for agent in _sim_runner.sim.state.agents:
		agents_data.append({
			"id": agent.id,
			"pos_x": agent.pos_x,
			"pos_y": agent.pos_y,
			"alive": agent.is_alive(),
			"is_player": agent.is_player,
			"faction_id": agent.faction_id
		})

	# Check if data changed
	var current_hash = agents_data.hash()
	if current_hash == _last_agents_hash:
		return

	_map_view.update_agents(agents_data)
	_last_agents_hash = current_hash
	agents_updated.emit(agents_data)


## Update market position in snapshot
func update_snapshot() -> void:
	if _sim_runner.sim == null:
		return

	var snapshot: Dictionary = {}
	var market_x: int = _sim_runner.sim.state.tuning.get("market_pos_x", 48)
	var market_y: int = _sim_runner.sim.state.tuning.get("market_pos_y", 48)
	snapshot["market_x"] = market_x
	snapshot["market_y"] = market_y

	var current_hash = snapshot.hash()
	if current_hash == _last_snapshot_hash:
		return

	_map_view.update_from_snapshot(snapshot)
	_last_snapshot_hash = current_hash
	snapshot_updated.emit(snapshot)


## Update world size from simulation
func update_world_size() -> void:
	if _sim_runner.sim == null:
		return

	var world_w: int = _sim_runner.sim.state.tuning.get("world_w", 96)
	var world_h: int = _sim_runner.sim.state.tuning.get("world_h", 96)
	_map_view.set_world_size(world_w, world_h)


## Force immediate update (for initialization)
func force_update_all() -> void:
	update_world_size()
	update_map_data()
	update_agents()
	update_snapshot()


## Handle overlay changes
func on_overlay_changed() -> void:
	overlay_changed.emit()
	_map_view.queue_redraw()


## Set agent target position
func set_agent_target(target_pos: Vector2) -> void:
	_map_view.set_agent_target(target_pos)


## Clear agent target
func clear_agent_target() -> void:
	_map_view.clear_agent_target()


## Get current zoom level
func get_zoom_level() -> float:
	return _map_view.zoom_level


## Set overlay settings
func set_overlay_settings(settings: OverlaySettings) -> void:
	_map_view.set_overlay_settings(settings)


## Get map view selection
func get_selection() -> SelectionModel:
	return _map_view.selection


## Build a full world payload for initial map view setup
func _build_full_world_data(world: RefCounted) -> Dictionary:
	var world_data: Dictionary = {}

	var claims: Dictionary = {}
	for y in range(world.height):
		for x in range(world.width):
			var owner_id: int = world.get_claim_owner(x, y)
			if owner_id > 0:
				claims["%d,%d" % [x, y]] = owner_id
	world_data["claims"] = claims

	var resource_nodes := _build_resource_nodes_data(world)
	world_data["resource_nodes"] = resource_nodes

	var workshops := _build_workshops_data(world)
	world_data["workshops"] = workshops

	var pollution: Dictionary = {}
	for y in range(world.height):
		for x in range(world.width):
			var p: float = world.get_pollution(x, y)
			if p > 0.0:
				pollution["%d,%d" % [x, y]] = p
	world_data["pollution"] = pollution

	return world_data


func _build_resource_nodes_data(world: RefCounted) -> Array:
	var resource_nodes: Array = []
	for node in world.resource_nodes:
		resource_nodes.append({
			"pos_x": node.pos_x,
			"pos_y": node.pos_y,
			"type": node.type,
			"stock": node.stock,
			"max_stock": node.max_stock
		})
	return resource_nodes


func _build_workshops_data(world: RefCounted) -> Array:
	var workshops: Array = []
	for workshop in world.workshops:
		workshops.append({
			"pos_x": workshop.pos_x,
			"pos_y": workshop.pos_y,
			"is_built": workshop.is_built
		})
	return workshops


func _build_projects_data(state: SimState) -> Array:
	var projects_data: Array = []
	for project in state.communal_projects.projects:
		if not project.is_active():
			continue
		var progress_ratio := 0.0
		if project.status == CommunalProject.STATUS_BUILDING:
			var required := maxi(1, project.build_required)
			progress_ratio = float(mini(project.build_progress, required)) / float(required)
		elif project.status == CommunalProject.STATUS_COLLECTING:
			var required_total := 0
			var contributed_total := 0
			for item in project.required_resources:
				required_total += int(project.required_resources[item])
				contributed_total += int(project.contributed.get(item, 0))
			if required_total > 0:
				progress_ratio = float(contributed_total) / float(required_total)
		projects_data.append({
			"pos_x": project.pos_x,
			"pos_y": project.pos_y,
			"status": project.status,
			"progress_ratio": progress_ratio
		})
	return projects_data


func _build_tasks_data(state: SimState) -> Array:
	var tasks_data: Array = []
	for activity in state.job_board.activities:
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status != JobBoard.STATUS_AVAILABLE and status != JobBoard.STATUS_CLAIMED:
			continue
		var activity_type: String = activity.get("type", "")
		var data: Dictionary = activity.get("data", {})
		var pos := _get_activity_position(state, activity_type, data)
		if pos == Vector2i(-1, -1):
			continue
		tasks_data.append({
			"pos_x": pos.x,
			"pos_y": pos.y,
			"type": activity_type,
			"status": status
		})
	return tasks_data


func _get_activity_position(state: SimState, activity_type: String, data: Dictionary) -> Vector2i:
	match activity_type:
		JobBoard.ACTIVITY_GATHER_NODE:
			var node_id: int = int(data.get("node_id", -1))
			var node := state.world.get_node_by_id(node_id)
			if node != null:
				return Vector2i(node.pos_x, node.pos_y)
		JobBoard.ACTIVITY_HAUL:
			var destination_type: String = data.get("destination_type", "stockpile")
			if destination_type == "project":
				var project_id: int = int(data.get("destination_id", -1))
				var project := state.communal_projects.get_project(project_id)
				if project != null:
					return Vector2i(project.pos_x, project.pos_y)
			else:
				var destination_id: int = int(data.get("destination_id", -1))
				var destination := state.structures.get_structure(destination_id)
				if destination != null:
					return Vector2i(destination.pos_x, destination.pos_y)
		JobBoard.ACTIVITY_DELIVER_TO_PROJECT, JobBoard.ACTIVITY_BUILD_SITE:
			var project_id: int = int(data.get("project_id", -1))
			var project := state.communal_projects.get_project(project_id)
			if project != null:
				return Vector2i(project.pos_x, project.pos_y)
		JobBoard.ACTIVITY_CRAFT_AT_STATION:
			var station_id: int = int(data.get("station_id", -1))
			var workshop := state.world.get_workshop_by_id(station_id)
			if workshop != null:
				return Vector2i(workshop.pos_x, workshop.pos_y)
		JobBoard.ACTIVITY_FARM_TASK:
			var plot_id: int = int(data.get("plot_id", -1))
			if plot_id >= 0:
				var x := plot_id % state.world.width
				var y := plot_id / state.world.width
				return Vector2i(x, y)
	return Vector2i(-1, -1)


func _build_stockpiles_data(state: SimState) -> Array:
	var stockpiles_data: Array = []
	for structure in state.structures.structures:
		if structure.structure_type != StructureState.TYPE_STOCKPILE:
			continue
		var reserved_total := 0
		for item in structure.reserved_items:
			reserved_total += int(structure.reserved_items[item])
		stockpiles_data.append({
			"pos_x": structure.pos_x,
			"pos_y": structure.pos_y,
			"reserved_total": reserved_total
		})
	return stockpiles_data
