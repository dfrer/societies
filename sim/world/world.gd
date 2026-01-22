## World - container for all world/environment state
## Provides clean API for tile queries, resource nodes, pollution, and ownership
class_name World
extends RefCounted

## World dimensions
var width: int = 96
var height: int = 96

## Pollution per tile (flat array, index = y * width + x)
var pollution: Array[float] = []

## Tile ownership (flat array, same indexing)
## 0 = unclaimed, positive = agent ID, >= 1000000 = faction owner ID
var tiles: Array[int] = []

## Road network (flat array, same indexing)
## 0 = no road, 1 = dirt road, 2 = paved road
var roads: Array[int] = []

## Zoning tags per tile (flat array, same indexing)
var zone_tags: Array[String] = []

## Resource nodes
var resource_nodes: Array[ResourceNode] = []
var _nodes_by_id: Dictionary = {}  # id -> ResourceNode
var next_node_id: int = 1

## Workshops
var workshops: Array[Workshop] = []
var _workshops_by_id: Dictionary = {}  # id -> Workshop

## Pollution cache for performance
var _cached_avg_pollution: float = 0.0
var _pollution_cache_tick: int = -1

## Faction owner ID offset (faction_id + this = owner_id)
const FACTION_OWNER_OFFSET := 1000000
const ORGANIZATION_OWNER_OFFSET := 2000000

# ============================================
# INITIALIZATION
# ============================================

func _init() -> void:
	pass

## Initialize world with dimensions
func init_world(w: int, h: int) -> void:
	width = w
	height = h
	pollution.resize(w * h)
	pollution.fill(0.0)
	tiles.resize(w * h)
	tiles.fill(0)
	roads.resize(w * h)
	roads.fill(0)
	zone_tags.resize(w * h)
	zone_tags.fill("")

# ============================================
# TILE QUERY
# ============================================

## Check if coordinates are within bounds
func is_valid(x: int, y: int) -> bool:
	return x >= 0 and x < width and y >= 0 and y < height

## Get tile index from coordinates
func _tile_index(x: int, y: int) -> int:
	return y * width + x

## Get tile data at position
func get_tile(x: int, y: int) -> Dictionary:
	if not is_valid(x, y):
		return {}
	var idx := _tile_index(x, y)
	return {
		"x": x,
		"y": y,
		"pollution": pollution[idx],
		"owner_id": tiles[idx]
	}

# ============================================
# RESOURCE NODE QUERY/MUTATE
# ============================================

## Add a resource node
func add_resource_node(node: ResourceNode) -> void:
	if node.id == 0:
		node.id = next_node_id
		next_node_id += 1
	elif node.id >= next_node_id:
		next_node_id = node.id + 1
	resource_nodes.append(node)
	_nodes_by_id[node.id] = node

## Get node by ID
func get_node_by_id(node_id: int) -> ResourceNode:
	return _nodes_by_id.get(node_id, null)

## Get node at position (first found)
func get_node_at(x: int, y: int) -> ResourceNode:
	for node in resource_nodes:
		if node.pos_x == x and node.pos_y == y:
			return node
	return null

## Get total stock of a resource type
func get_total_stock(type: String) -> int:
	var total := 0
	for node in resource_nodes:
		if node.type == type:
			total += node.stock
	return total

# ============================================
# POLLUTION QUERY/MUTATE
# ============================================

## Get pollution at position
func get_pollution(x: int, y: int) -> float:
	if not is_valid(x, y):
		return 0.0
	return pollution[_tile_index(x, y)]

## Set pollution at position
func set_pollution(x: int, y: int, value: float) -> void:
	if is_valid(x, y):
		pollution[_tile_index(x, y)] = clampf(value, 0.0, 1.0)
		_pollution_cache_tick = -1  # Invalidate cache

## Add pollution at position (clamped 0-1)
func add_pollution(x: int, y: int, delta: float) -> void:
	if is_valid(x, y):
		var idx := _tile_index(x, y)
		pollution[idx] = clampf(pollution[idx] + delta, 0.0, 1.0)
		_pollution_cache_tick = -1  # Invalidate cache

## Get average pollution (cached per tick)
func get_average_pollution(tick: int = -1) -> float:
	if tick >= 0 and tick == _pollution_cache_tick:
		return _cached_avg_pollution
	
	if pollution.is_empty():
		return 0.0
	
	var total := 0.0
	for p in pollution:
		total += p
	_cached_avg_pollution = total / pollution.size()
	_pollution_cache_tick = tick
	return _cached_avg_pollution

# ============================================
# OWNERSHIP QUERY/MUTATE
# ============================================

## Get claim owner at position (0 = unclaimed)
func get_claim_owner(x: int, y: int) -> int:
	if not is_valid(x, y):
		return 0
	return tiles[_tile_index(x, y)]

## Set claim owner at position
func set_claim_owner(x: int, y: int, owner_id: int) -> void:
	if is_valid(x, y):
		tiles[_tile_index(x, y)] = owner_id

## Check if tile is claimed
func is_claimed(x: int, y: int) -> bool:
	return get_claim_owner(x, y) > 0

## Get zoning tag at position
func get_zone_tag(x: int, y: int) -> String:
	if not is_valid(x, y):
		return ""
	return zone_tags[_tile_index(x, y)]

## Set zoning tag at position
func set_zone_tag(x: int, y: int, tag: String) -> void:
	if is_valid(x, y):
		zone_tags[_tile_index(x, y)] = tag

## Get road status at position
func get_road(x: int, y: int) -> int:
	if not is_valid(x, y):
		return 0
	return roads[_tile_index(x, y)]

## Set road status at position
func set_road(x: int, y: int, value: int) -> void:
	if is_valid(x, y):
		roads[_tile_index(x, y)] = value

## Check if tile has a road
func has_road(x: int, y: int) -> bool:
	return get_road(x, y) > 0

## Get total claimed tiles count
func get_claimed_tiles_count() -> int:
	var count := 0
	for owner in tiles:
		if owner > 0:
			count += 1
	return count

## Get claims count for a specific owner
func get_owner_claims_count(owner_id: int) -> int:
	var count := 0
	for owner in tiles:
		if owner == owner_id:
			count += 1
	return count

## Get claims grouped by owner
func get_claims_by_owner() -> Dictionary:
	var claims := {}
	for i in range(tiles.size()):
		var owner := tiles[i]
		if owner > 0:
			if not claims.has(owner):
				claims[owner] = []
			var x := i % width
			var y := i / width
			claims[owner].append(Vector2i(x, y))
	return claims

# ============================================
# WORKSHOP QUERY/MUTATE
# ============================================

## Add a workshop
func add_workshop(workshop: Workshop) -> void:
	workshops.append(workshop)
	_workshops_by_id[workshop.id] = workshop

## Get workshop by ID
func get_workshop_by_id(workshop_id: int) -> Workshop:
	return _workshops_by_id.get(workshop_id, null)

## Get workshop count
func get_workshop_count() -> int:
	return workshops.size()

## Get ready workshop count
func get_ready_workshop_count() -> int:
	var count := 0
	for ws in workshops:
		if ws.is_ready():
			count += 1
	return count

## Check if workshop exists at position
func has_workshop_at(x: int, y: int) -> bool:
	for ws in workshops:
		if ws.pos_x == x and ws.pos_y == y:
			return true
	return false

## Get workshop at position (first found)
func get_workshop_at(x: int, y: int) -> Workshop:
	for ws in workshops:
		if ws.pos_x == x and ws.pos_y == y:
			return ws
	return null

## Find closest workshop to position
func find_closest_workshop(x: int, y: int) -> Workshop:
	var closest: Workshop = null
	var closest_dist := 999999
	for ws in workshops:
		if ws.is_ready():
			var dist := absi(ws.pos_x - x) + absi(ws.pos_y - y)
			if dist < closest_dist:
				closest_dist = dist
				closest = ws
	return closest

# ============================================
# FACTION OWNER HELPERS
# ============================================

## Check if owner_id represents a faction (not individual agent)
static func is_faction_owner(owner_id: int) -> bool:
	return owner_id >= FACTION_OWNER_OFFSET

## Extract faction_id from faction owner_id
static func faction_id_from_owner(owner_id: int) -> int:
	return owner_id - FACTION_OWNER_OFFSET

## Create faction owner_id from faction_id
static func faction_owner_id(faction_id: int) -> int:
	return faction_id + FACTION_OWNER_OFFSET

## Alias for backward compatibility
static func owner_id_for_faction(faction_id: int) -> int:
	return faction_owner_id(faction_id)

## Check if owner_id represents an organization
static func is_organization_owner(owner_id: int) -> bool:
	return owner_id >= ORGANIZATION_OWNER_OFFSET

## Extract organization_id from owner_id
static func organization_id_from_owner(owner_id: int) -> int:
	return owner_id - ORGANIZATION_OWNER_OFFSET

## Create organization owner_id from organization_id
static func organization_owner_id(organization_id: int) -> int:
	return organization_id + ORGANIZATION_OWNER_OFFSET

# ============================================
# SERIALIZATION
# ============================================

## Serialize to dictionary
func to_dict() -> Dictionary:
	var nodes_data := []
	for node in resource_nodes:
		nodes_data.append(node.to_dict())
	
	var workshops_data := []
	for ws in workshops:
		workshops_data.append(ws.to_dict())
	
	# Convert pollution to regular Array for JSON
	var pollution_data := []
	for p in pollution:
		pollution_data.append(snappedf(p, 0.00000001))
	
	return {
		"width": width,
		"height": height,
		"pollution": pollution_data,
		"tiles": tiles.duplicate(),
		"roads": roads.duplicate(),
		"zone_tags": zone_tags.duplicate(),
		"resource_nodes": nodes_data,
		"workshops": workshops_data,
		"next_node_id": next_node_id
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> World:
	var world := World.new()
	world.width = int(d.get("width", 96))
	world.height = int(d.get("height", 96))
	
	# Load pollution
	var pollution_data: Array = d.get("pollution", [])
	world.pollution.resize(pollution_data.size())
	for i in range(pollution_data.size()):
		world.pollution[i] = float(pollution_data[i])
	
	# Load tiles
	var tiles_data: Array = d.get("tiles", [])
	world.tiles.resize(tiles_data.size())
	for i in range(tiles_data.size()):
		world.tiles[i] = int(tiles_data[i])
	
	# Ensure arrays are properly sized if empty
	if world.pollution.is_empty():
		world.pollution.resize(world.width * world.height)
		world.pollution.fill(0.0)
	if world.tiles.is_empty():
		world.tiles.resize(world.width * world.height)
		world.tiles.fill(0)
	
	# Load roads
	var roads_data: Array = d.get("roads", [])
	world.roads.resize(roads_data.size())
	for i in range(roads_data.size()):
		world.roads[i] = int(roads_data[i])

	if world.roads.is_empty():
		world.roads.resize(world.width * world.height)
		world.roads.fill(0)

	# Load zone tags
	var zone_data: Array = d.get("zone_tags", [])
	world.zone_tags.resize(zone_data.size())
	for i in range(zone_data.size()):
		world.zone_tags[i] = str(zone_data[i])

	if world.zone_tags.is_empty():
		world.zone_tags.resize(world.width * world.height)
		world.zone_tags.fill("")
	
	# Load resource nodes
	world.resource_nodes = []
	world._nodes_by_id = {}
	for node_data in d.get("resource_nodes", []):
		var node := ResourceNode.from_dict(node_data)
		world.resource_nodes.append(node)
		world._nodes_by_id[node.id] = node
	
	# Load workshops
	world.workshops = []
	world._workshops_by_id = {}
	for ws_data in d.get("workshops", []):
		var ws := Workshop.from_dict(ws_data)
		world.workshops.append(ws)
		world._workshops_by_id[ws.id] = ws
	
	world.next_node_id = int(d.get("next_node_id", 1))
	
	return world
