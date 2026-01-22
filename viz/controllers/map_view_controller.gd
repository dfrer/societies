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
	if not _world_data_initialized:
		var world_data := _build_full_world_data(world)
		_map_view.update_world_data(world_data)
		_world_data_initialized = true
		world.consume_dirty_claim_tiles()
		world.consume_dirty_pollution_tiles()
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
