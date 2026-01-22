class_name MapView
extends Control

## Map view that renders the world grid with pan and zoom support.
## Uses custom _draw() for efficient rendering with overlay support.

signal tile_clicked(tile: Vector2i)
signal tile_hovered(tile: Vector2i)
signal agent_clicked(agent_id: int)

## Tile size in pixels (configurable)
@export var tile_size: int = 10

## Zoom limits
@export var min_zoom: float = 0.25
@export var max_zoom: float = 4.0
@export var zoom_step: float = 0.1

## Base colors
@export var color_background: Color = Color(0.1, 0.1, 0.12)
@export var color_grid_line: Color = Color(0.3, 0.3, 0.35)
@export var color_selection: Color = Color(1.0, 0.8, 0.2, 0.8)
@export var color_hover: Color = Color(1.0, 1.0, 1.0, 0.3)
@export var color_workshop: Color = Color(0.7, 0.5, 0.2)
@export var color_market: Color = Color(0.9, 0.7, 0.1)
@export var color_market_blocked: Color = Color(0.9, 0.2, 0.2)
@export var color_faction_border: Color = Color(1.0, 1.0, 1.0, 0.6)
@export var color_project_collecting: Color = Color(0.95, 0.6, 0.2, 0.7)
@export var color_project_building: Color = Color(0.2, 0.8, 0.4, 0.7)
@export var color_task_default: Color = Color(0.6, 0.8, 0.9, 0.85)
@export var color_stockpile_reserved: Color = Color(0.9, 0.2, 0.2, 0.7)

## World dimensions
var world_width: int = 96
var world_height: int = 96

## Camera state
var camera_offset: Vector2 = Vector2.ZERO
var zoom_level: float = 1.0

## Pan state
var _is_panning: bool = false
var _pan_start: Vector2 = Vector2.ZERO

## Selection model
var selection: SelectionModel = SelectionModel.new()

## Hover redraw throttling
var _hover_redraw_timer: Timer = null
var _hover_redraw_pending: bool = false
var _hover_redraw_interval: float = 0.075

## Overlay settings
var overlay_settings: OverlaySettings = OverlaySettings.new()

## Cached snapshot data for rendering
var _snapshot: Dictionary = {}
var _world_data: Dictionary = {}
var _agents_data: Array = []

## Performance optimization - track last data hashes to prevent unnecessary redraws
var _last_world_hash: int = 0
var _last_agents_hash: int = 0
var _last_snapshot_hash: int = 0
var _last_static_world_hash: int = 0
var _resource_nodes: Array = []
var _workshops: Array = []
var _claims: Dictionary = {}
var _pollution_data: Dictionary = {}
var _avg_pollution: float = 0.0
var _projects_data: Array = []
var _tasks_data: Array = []
var _stockpiles_data: Array = []
var _claims_texture: ImageTexture = null
var _pollution_texture: ImageTexture = null
var _claims_dirty: bool = true
var _pollution_dirty: bool = true
var _last_projects_hash: int = 0
var _last_tasks_hash: int = 0
var _last_stockpiles_hash: int = 0

## Selected agent target position for line drawing (-1,-1 means no target)
var _agent_target_pos: Vector2i = Vector2i(-1, -1)

## Font for drawing letters
var _font: Font = null


func _ready() -> void:
	# Enable input processing
	mouse_filter = Control.MOUSE_FILTER_STOP
	clip_contents = true

	# Get default font
	_font = ThemeDB.fallback_font

	# Connect selection signals
	selection.tile_selected.connect(_on_tile_selected)
	selection.tile_hovered.connect(_on_tile_hovered)

	# Connect overlay settings signal
	overlay_settings.settings_changed.connect(_on_overlay_settings_changed)

	# Initialize hover redraw throttling timer
	_hover_redraw_timer = Timer.new()
	_hover_redraw_timer.one_shot = true
	_hover_redraw_timer.wait_time = _hover_redraw_interval
	_hover_redraw_timer.timeout.connect(_on_hover_redraw_timeout)
	add_child(_hover_redraw_timer)

	# Center the view initially
	call_deferred("_center_view")


func _center_view() -> void:
	var view_size := size
	var world_pixel_size := Vector2(world_width * tile_size, world_height * tile_size) * zoom_level
	camera_offset = (view_size - world_pixel_size) / 2.0
	queue_redraw()


func set_world_size(width: int, height: int) -> void:
	world_width = width
	world_height = height
	selection.set_world_size(width, height)
	_claims_dirty = true
	_pollution_dirty = true
	_center_view()


func update_from_snapshot(snapshot: Dictionary) -> void:
	var new_hash = snapshot.hash()
	if new_hash == _last_snapshot_hash:
		return
	
	_snapshot = snapshot
	_avg_pollution = snapshot.get("avg_pollution", 0.0)
	_pollution_dirty = true
	_last_snapshot_hash = new_hash
	queue_redraw()


func update_world_data(world_data: Dictionary) -> void:
	var new_hash = world_data.hash()
	if new_hash == _last_world_hash:
		return
	
	_world_data = world_data
	_claims = world_data.get("claims", {})
	_resource_nodes = world_data.get("resource_nodes", [])
	_workshops = world_data.get("workshops", [])
	_pollution_data = world_data.get("pollution", [])
	_claims_dirty = true
	_pollution_dirty = true
	_last_world_hash = new_hash
	queue_redraw()


func update_static_world_data(resource_nodes: Array, workshops: Array) -> void:
	var static_hash := resource_nodes.hash() ^ workshops.hash()
	if static_hash == _last_static_world_hash:
		return

	_resource_nodes = resource_nodes
	_workshops = workshops
	_last_static_world_hash = static_hash
	queue_redraw()


func update_world_tiles(claims_delta: Dictionary, pollution_delta: Dictionary) -> void:
	if claims_delta.is_empty() and pollution_delta.is_empty():
		return

	for key in claims_delta.keys():
		var owner_id: int = claims_delta[key]
		if owner_id > 0:
			_claims[key] = owner_id
		else:
			_claims.erase(key)

	for key in pollution_delta.keys():
		var pollution: float = float(pollution_delta[key])
		if pollution > 0.0:
			_pollution_data[key] = pollution
		else:
			_pollution_data.erase(key)

	queue_redraw()


func update_agents(agents: Array) -> void:
	var new_hash = agents.hash()
	if new_hash == _last_agents_hash:
		return
	
	_agents_data = agents
	_last_agents_hash = new_hash
	queue_redraw()


func update_projects_data(projects: Array) -> void:
	var new_hash = projects.hash()
	if new_hash == _last_projects_hash:
		return
	_projects_data = projects
	_last_projects_hash = new_hash
	queue_redraw()


func update_tasks_data(tasks: Array) -> void:
	var new_hash = tasks.hash()
	if new_hash == _last_tasks_hash:
		return
	_tasks_data = tasks
	_last_tasks_hash = new_hash
	queue_redraw()


func update_stockpiles_data(stockpiles: Array) -> void:
	var new_hash = stockpiles.hash()
	if new_hash == _last_stockpiles_hash:
		return
	_stockpiles_data = stockpiles
	_last_stockpiles_hash = new_hash
	queue_redraw()


func set_overlay_settings(settings: OverlaySettings) -> void:
	if overlay_settings != null:
		if overlay_settings.settings_changed.is_connected(_on_overlay_settings_changed):
			overlay_settings.settings_changed.disconnect(_on_overlay_settings_changed)

	overlay_settings = settings
	overlay_settings.settings_changed.connect(_on_overlay_settings_changed)
	queue_redraw()


func _on_overlay_settings_changed() -> void:
	_claims_dirty = true
	_pollution_dirty = true
	queue_redraw()


func _draw() -> void:
	# Background
	draw_rect(Rect2(Vector2.ZERO, size), color_background)

	var effective_tile_size: float = tile_size * zoom_level

	# Calculate visible tile range for culling
	var start_tile := world_to_tile(-camera_offset / zoom_level)
	var end_tile := world_to_tile((-camera_offset + size) / zoom_level)
	start_tile = start_tile.clamp(Vector2i.ZERO, Vector2i(world_width - 1, world_height - 1))
	end_tile = end_tile.clamp(Vector2i.ZERO, Vector2i(world_width - 1, world_height - 1))

	# Draw base tiles with claims coloring (cached)
	_ensure_claims_cache()
	if _claims_texture != null:
		var world_rect := Rect2(
			camera_offset,
			Vector2(world_width * tile_size, world_height * tile_size) * zoom_level
		)
		draw_texture_rect(_claims_texture, world_rect, false)

	# Draw pollution overlay
	if overlay_settings.show_pollution:
		_ensure_pollution_cache()
		if _pollution_texture != null:
			var world_rect := Rect2(
				camera_offset,
				Vector2(world_width * tile_size, world_height * tile_size) * zoom_level
			)
			draw_texture_rect(_pollution_texture, world_rect, false)

	# Draw resource nodes
	if overlay_settings.show_resources:
		_draw_resources(start_tile, end_tile, effective_tile_size)

	# Draw workshops
	_draw_workshops(start_tile, end_tile, effective_tile_size)

	# Draw market
	if overlay_settings.show_market:
		_draw_market(start_tile, end_tile, effective_tile_size)

	# Draw projects
	if overlay_settings.show_projects:
		_draw_projects(start_tile, end_tile, effective_tile_size)

	# Draw tasks
	if overlay_settings.show_tasks:
		_draw_tasks(start_tile, end_tile, effective_tile_size)

	# Draw stockpile reservation overlay
	if overlay_settings.show_stockpile_reservations:
		_draw_stockpile_reservations(start_tile, end_tile, effective_tile_size)

	# Draw faction borders
	if overlay_settings.show_faction_borders:
		_draw_faction_borders(start_tile, end_tile, effective_tile_size)

	# Draw agents
	if overlay_settings.show_agents:
		_draw_agents(start_tile, end_tile, effective_tile_size)

	# Draw grid lines
	if overlay_settings.show_grid and zoom_level >= 0.5:
		_draw_grid(start_tile, end_tile, effective_tile_size)

	# Draw selection and hover
	_draw_selection(start_tile, end_tile, effective_tile_size)


func _ensure_claims_cache() -> void:
	if not _claims_dirty and _claims_texture != null:
		return
	if world_width <= 0 or world_height <= 0 or tile_size <= 0:
		return

	var image := Image.create(world_width * tile_size, world_height * tile_size, false, Image.FORMAT_RGBA8)
	for y in range(world_height):
		for x in range(world_width):
			var tile_key := "%d,%d" % [x, y]
			var base_color: Color

			if overlay_settings.show_claims:
				var owner_id: int = 0
				if _claims.has(tile_key):
					owner_id = _claims[tile_key]
				base_color = overlay_settings.get_owner_color(owner_id)
			else:
				base_color = overlay_settings.COLOR_UNCLAIMED

			image.fill_rect(Rect2i(x * tile_size, y * tile_size, tile_size, tile_size), base_color)

	if _claims_texture == null \
			or _claims_texture.get_width() != image.get_width() \
			or _claims_texture.get_height() != image.get_height():
		_claims_texture = ImageTexture.create_from_image(image)
	else:
		_claims_texture.update(image)
	_claims_dirty = false


func _ensure_pollution_cache() -> void:
	if not _pollution_dirty and _pollution_texture != null:
		return
	if world_width <= 0 or world_height <= 0 or tile_size <= 0:
		return

	var image := Image.create(world_width * tile_size, world_height * tile_size, false, Image.FORMAT_RGBA8)
	image.fill(Color(0.0, 0.0, 0.0, 0.0))

	if _pollution_data.size() > 0:
		for key in _pollution_data.keys():
			var parts: PackedStringArray = str(key).split(",")
			if parts.size() != 2:
				continue
			var x := int(parts[0])
			var y := int(parts[1])
			if x < 0 or x >= world_width or y < 0 or y >= world_height:
				continue
			var pollution: float = _pollution_data[key]
			if pollution <= 0.01:
				continue
			var pollution_color := overlay_settings.get_pollution_color(pollution)
			image.fill_rect(Rect2i(x * tile_size, y * tile_size, tile_size, tile_size), pollution_color)
	elif _avg_pollution > 0.01:
		var tint := overlay_settings.get_global_pollution_tint(_avg_pollution)
		image.fill(tint)

	if _pollution_texture == null \
			or _pollution_texture.get_width() != image.get_width() \
			or _pollution_texture.get_height() != image.get_height():
		_pollution_texture = ImageTexture.create_from_image(image)
	else:
		_pollution_texture.update(image)
	_pollution_dirty = false


func _draw_tiles(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	for y in range(start_tile.y, end_tile.y + 1):
		for x in range(start_tile.x, end_tile.x + 1):
			var tile_pos := tile_to_screen(Vector2i(x, y))
			var tile_rect := Rect2(tile_pos, Vector2(effective_tile_size, effective_tile_size))

			var tile_key := "%d,%d" % [x, y]
			var base_color: Color

			if overlay_settings.show_claims:
				var owner_id: int = 0
				if _claims.has(tile_key):
					owner_id = _claims[tile_key]
				base_color = overlay_settings.get_owner_color(owner_id)
			else:
				base_color = overlay_settings.COLOR_UNCLAIMED

			draw_rect(tile_rect, base_color)


func _draw_pollution_overlay(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	# If we have per-tile pollution data, use it; otherwise use global average
	if _pollution_data.size() > 0:
		# Per-tile pollution (sparse dictionary format: "x,y" -> float)
		for y in range(start_tile.y, end_tile.y + 1):
			for x in range(start_tile.x, end_tile.x + 1):
				var key := "%d,%d" % [x, y]
				if _pollution_data.has(key):
					var pollution: float = _pollution_data[key]
					if pollution > 0.01:
						var tile_pos := tile_to_screen(Vector2i(x, y))
						var tile_rect := Rect2(tile_pos, Vector2(effective_tile_size, effective_tile_size))
						var pollution_color := overlay_settings.get_pollution_color(pollution)
						draw_rect(tile_rect, pollution_color)
	else:
		# Global pollution tint over entire visible area
		if _avg_pollution > 0.01:
			var tint := overlay_settings.get_global_pollution_tint(_avg_pollution)
			var visible_start := tile_to_screen(start_tile)
			var visible_end := tile_to_screen(end_tile + Vector2i.ONE)
			var visible_rect := Rect2(visible_start, visible_end - visible_start)
			draw_rect(visible_rect, tint)


func _draw_resources(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	var font_size: int = maxi(8, int(effective_tile_size * 0.6))

	for node in _resource_nodes:
		var nx: int = node.get("pos_x", 0)
		var ny: int = node.get("pos_y", 0)
		if nx < start_tile.x or nx > end_tile.x or ny < start_tile.y or ny > end_tile.y:
			continue

		var node_pos := tile_to_screen(Vector2i(nx, ny))
		var node_type: String = node.get("type", "")
		var node_color := overlay_settings.get_resource_color(node_type)
		var stock: int = node.get("stock", 0)
		var max_stock: int = node.get("max_stock", 1)
		var stock_ratio: float = float(stock) / float(max_stock) if max_stock > 0 else 0.0

		match overlay_settings.resource_display_mode:
			OverlaySettings.ResourceDisplayMode.DOTS:
				var inset: float = effective_tile_size * 0.25
				var inner_rect := Rect2(
					node_pos + Vector2(inset, inset),
					Vector2(effective_tile_size - inset * 2, effective_tile_size - inset * 2)
				)
				draw_rect(inner_rect, node_color)

			OverlaySettings.ResourceDisplayMode.LETTERS:
				var letter := overlay_settings.get_resource_letter(node_type)
				var center := node_pos + Vector2(effective_tile_size / 2, effective_tile_size / 2)
				# Adjust for text centering
				var text_offset := Vector2(-font_size * 0.3, font_size * 0.35)
				draw_string(_font, center + text_offset, letter, HORIZONTAL_ALIGNMENT_CENTER, -1, font_size, node_color)

			OverlaySettings.ResourceDisplayMode.SCALED_DOTS:
				var base_inset: float = effective_tile_size * 0.35
				var scale_factor: float = 0.5 + stock_ratio * 0.5
				var actual_inset: float = base_inset + (1.0 - scale_factor) * effective_tile_size * 0.15
				var inner_rect := Rect2(
					node_pos + Vector2(actual_inset, actual_inset),
					Vector2(effective_tile_size - actual_inset * 2, effective_tile_size - actual_inset * 2)
				)
				# Color intensity based on stock
				var adjusted_color := node_color.lerp(Color(0.2, 0.2, 0.2), 1.0 - stock_ratio)
				draw_rect(inner_rect, adjusted_color)


func _draw_workshops(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	for workshop in _workshops:
		var wx: int = workshop.get("pos_x", 0)
		var wy: int = workshop.get("pos_y", 0)
		if wx < start_tile.x or wx > end_tile.x or wy < start_tile.y or wy > end_tile.y:
			continue

		var ws_pos := tile_to_screen(Vector2i(wx, wy))
		var inset: float = effective_tile_size * 0.15
		var ws_rect := Rect2(
			ws_pos + Vector2(inset, inset),
			Vector2(effective_tile_size - inset * 2, effective_tile_size - inset * 2)
		)
		draw_rect(ws_rect, color_workshop)


func _draw_market(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	var market_x: int = _snapshot.get("market_x", 48)
	var market_y: int = _snapshot.get("market_y", 48)

	if market_x < start_tile.x or market_x > end_tile.x or market_y < start_tile.y or market_y > end_tile.y:
		return

	var market_pos := tile_to_screen(Vector2i(market_x, market_y))
	var market_rect := Rect2(market_pos, Vector2(effective_tile_size, effective_tile_size))

	# Check if selected agent would be blocked (if we have that info)
	var is_blocked: bool = _snapshot.get("market_blocked_for_selected", false)
	var outline_color := color_market_blocked if is_blocked else color_market

	# Draw double outline for market
	draw_rect(market_rect, outline_color, false, 2.0 * zoom_level)
	var inner_rect := Rect2(
		market_pos + Vector2(3 * zoom_level, 3 * zoom_level),
		Vector2(effective_tile_size - 6 * zoom_level, effective_tile_size - 6 * zoom_level)
	)
	draw_rect(inner_rect, outline_color, false, 1.0 * zoom_level)


func _draw_projects(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	for project in _projects_data:
		var px: int = project.get("pos_x", 0)
		var py: int = project.get("pos_y", 0)
		if px < start_tile.x or px > end_tile.x or py < start_tile.y or py > end_tile.y:
			continue

		var progress: float = float(project.get("progress_ratio", 0.0))
		var status: String = project.get("status", "")
		var base_color := color_project_collecting if status == CommunalProject.STATUS_COLLECTING else color_project_building
		var alpha: float = clampf(0.2 + progress * 0.7, 0.2, 0.9)
		var display_color := base_color
		display_color.a = alpha
		var tile_pos := tile_to_screen(Vector2i(px, py))
		var inset: float = effective_tile_size * 0.1
		var rect := Rect2(
			tile_pos + Vector2(inset, inset),
			Vector2(effective_tile_size - inset * 2, effective_tile_size - inset * 2)
		)
		draw_rect(rect, display_color)


func _draw_tasks(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	var radius: float = maxf(2.0, effective_tile_size * 0.15)
	for task in _tasks_data:
		var tx: int = task.get("pos_x", 0)
		var ty: int = task.get("pos_y", 0)
		if tx < start_tile.x or tx > end_tile.x or ty < start_tile.y or ty > end_tile.y:
			continue
		var status: String = task.get("status", "")
		var task_type: String = task.get("type", "")
		var base_color := _get_task_color(task_type)
		var display_color := base_color.lightened(0.2) if status == JobBoard.STATUS_CLAIMED else base_color
		var tile_pos := tile_to_screen(Vector2i(tx, ty))
		var center := tile_pos + Vector2(effective_tile_size / 2, effective_tile_size / 2)
		draw_circle(center, radius, display_color)


func _draw_stockpile_reservations(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	for stockpile in _stockpiles_data:
		var sx: int = stockpile.get("pos_x", 0)
		var sy: int = stockpile.get("pos_y", 0)
		if sx < start_tile.x or sx > end_tile.x or sy < start_tile.y or sy > end_tile.y:
			continue
		var reserved_total: int = stockpile.get("reserved_total", 0)
		if reserved_total <= 0:
			continue
		var tile_pos := tile_to_screen(Vector2i(sx, sy))
		var inset: float = effective_tile_size * 0.05
		var rect := Rect2(
			tile_pos + Vector2(inset, inset),
			Vector2(effective_tile_size - inset * 2, effective_tile_size - inset * 2)
		)
		draw_rect(rect, color_stockpile_reserved, false, 2.0 * zoom_level)


func _get_task_color(task_type: String) -> Color:
	match task_type:
		JobBoard.ACTIVITY_GATHER_NODE:
			return Color(0.7, 0.4, 0.9, 0.85)
		JobBoard.ACTIVITY_HAUL:
			return Color(0.3, 0.7, 0.9, 0.85)
		JobBoard.ACTIVITY_DELIVER_TO_PROJECT:
			return Color(0.9, 0.6, 0.2, 0.85)
		JobBoard.ACTIVITY_BUILD_SITE:
			return Color(0.2, 0.8, 0.4, 0.85)
		JobBoard.ACTIVITY_CRAFT_AT_STATION:
			return Color(0.2, 0.8, 0.8, 0.85)
		JobBoard.ACTIVITY_FARM_TASK:
			return Color(0.4, 0.9, 0.4, 0.85)
		_:
			return color_task_default


func _draw_faction_borders(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	# Draw borders around faction-owned tiles
	var border_width: float = maxf(1.0, 2.0 * zoom_level)

	for y in range(start_tile.y, end_tile.y + 1):
		for x in range(start_tile.x, end_tile.x + 1):
			var tile_key := "%d,%d" % [x, y]
			if not _claims.has(tile_key):
				continue

			var owner_id: int = _claims[tile_key]
			if not overlay_settings.is_faction_owner(owner_id):
				continue

			var tile_pos := tile_to_screen(Vector2i(x, y))
			var faction_color := overlay_settings.get_owner_color(owner_id)
			var border_color := faction_color.lightened(0.3)
			border_color.a = 0.8

			# Check each edge - draw border if adjacent tile is different owner
			# Top edge
			if y == 0 or _get_owner_at(x, y - 1) != owner_id:
				draw_line(tile_pos, tile_pos + Vector2(effective_tile_size, 0), border_color, border_width)

			# Bottom edge
			if y == world_height - 1 or _get_owner_at(x, y + 1) != owner_id:
				var bottom := tile_pos + Vector2(0, effective_tile_size)
				draw_line(bottom, bottom + Vector2(effective_tile_size, 0), border_color, border_width)

			# Left edge
			if x == 0 or _get_owner_at(x - 1, y) != owner_id:
				draw_line(tile_pos, tile_pos + Vector2(0, effective_tile_size), border_color, border_width)

			# Right edge
			if x == world_width - 1 or _get_owner_at(x + 1, y) != owner_id:
				var right := tile_pos + Vector2(effective_tile_size, 0)
				draw_line(right, right + Vector2(0, effective_tile_size), border_color, border_width)


func _get_owner_at(x: int, y: int) -> int:
	var key := "%d,%d" % [x, y]
	if _claims.has(key):
		return _claims[key]
	return 0


func _draw_agents(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	var selected_agent_center: Vector2 = Vector2(-1, -1)
	
	for agent in _agents_data:
		if not agent.get("alive", true):
			continue

		var ax: int = agent.get("pos_x", 0)
		var ay: int = agent.get("pos_y", 0)
		if ax < start_tile.x or ax > end_tile.x or ay < start_tile.y or ay > end_tile.y:
			continue

		var agent_pos := tile_to_screen(Vector2i(ax, ay))
		var center := agent_pos + Vector2(effective_tile_size / 2, effective_tile_size / 2)
		var radius: float = effective_tile_size * 0.3

		# Color based on faction
		var faction_id: int = agent.get("faction_id", 0)
		var agent_color: Color
		if faction_id > 0:
			var faction_owner_id: int = OverlaySettings.FACTION_OWNER_OFFSET + faction_id
			agent_color = overlay_settings.get_owner_color(faction_owner_id)
		else:
			agent_color = Color(0.3, 0.5, 0.8)  # Default agent color

		# Player agent gets special indicator
		if agent.get("is_player", false):
			draw_circle(center, radius * 1.3, Color.WHITE)

		# Selected agent gets highlight ring
		var agent_id: int = agent.get("id", -1)
		if selection.has_agent_selection() and selection.selected_agent_id == agent_id:
			draw_arc(center, radius * 1.5, 0, TAU, 32, color_selection, 2.0 * zoom_level)
			selected_agent_center = center

		draw_circle(center, radius, agent_color)
	
	# Draw line from selected agent to target position
	if selected_agent_center != Vector2(-1, -1) and _agent_target_pos != Vector2i(-1, -1):
		var target_screen := tile_to_screen(_agent_target_pos)
		var target_center := target_screen + Vector2(effective_tile_size / 2, effective_tile_size / 2)
		var line_color := Color(1.0, 0.8, 0.2, 0.5)
		draw_line(selected_agent_center, target_center, line_color, 1.5 * zoom_level)


func _draw_grid(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	var line_alpha: float = clampf((zoom_level - 0.5) / 0.5, 0.0, 1.0) * 0.4
	var grid_color := Color(color_grid_line, line_alpha)

	# Vertical lines
	for x in range(start_tile.x, end_tile.x + 2):
		var line_x: float = camera_offset.x + x * effective_tile_size
		draw_line(Vector2(line_x, 0), Vector2(line_x, size.y), grid_color, 1.0)

	# Horizontal lines
	for y in range(start_tile.y, end_tile.y + 2):
		var line_y: float = camera_offset.y + y * effective_tile_size
		draw_line(Vector2(0, line_y), Vector2(size.x, line_y), grid_color, 1.0)


func _draw_selection(start_tile: Vector2i, end_tile: Vector2i, effective_tile_size: float) -> void:
	# Draw selection highlight
	if selection.has_selection():
		var sel_tile := selection.selected_tile
		if sel_tile.x >= start_tile.x and sel_tile.x <= end_tile.x and sel_tile.y >= start_tile.y and sel_tile.y <= end_tile.y:
			var sel_pos := tile_to_screen(sel_tile)
			var sel_rect := Rect2(sel_pos, Vector2(effective_tile_size, effective_tile_size))
			draw_rect(sel_rect, color_selection, false, 2.0 * zoom_level)

	# Draw hover highlight
	if selection.has_hover():
		var htile := selection.hover_tile
		if htile.x >= start_tile.x and htile.x <= end_tile.x and htile.y >= start_tile.y and htile.y <= end_tile.y:
			var hover_pos := tile_to_screen(htile)
			var hover_rect := Rect2(hover_pos, Vector2(effective_tile_size, effective_tile_size))
			draw_rect(hover_rect, color_hover)


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		_handle_mouse_button(event)
	elif event is InputEventMouseMotion:
		_handle_mouse_motion(event)


func _handle_mouse_button(event: InputEventMouseButton) -> void:
	match event.button_index:
		MOUSE_BUTTON_LEFT:
			if event.pressed:
				var tile := screen_to_tile(event.position)
				if selection.is_valid_tile(tile):
					# Check for agent at tile first (priority)
					var agent_id := get_agent_at_tile(tile)
					if agent_id != -1:
						selection.select_agent(agent_id)
						selection.select_tile(tile)
						tile_clicked.emit(tile)
						agent_clicked.emit(agent_id)
					else:
						selection.clear_agent_selection()
						selection.select_tile(tile)
						tile_clicked.emit(tile)
					queue_redraw()

		MOUSE_BUTTON_MIDDLE:
			if event.pressed:
				_is_panning = true
				_pan_start = event.position
			else:
				_is_panning = false

		MOUSE_BUTTON_WHEEL_UP:
			if event.pressed:
				_zoom_at(event.position, zoom_step)

		MOUSE_BUTTON_WHEEL_DOWN:
			if event.pressed:
				_zoom_at(event.position, -zoom_step)


func _handle_mouse_motion(event: InputEventMouseMotion) -> void:
	if _is_panning:
		camera_offset += event.relative
		queue_redraw()
	else:
		var tile := screen_to_tile(event.position)
		selection.set_hover(tile)
		tile_hovered.emit(tile)
		_request_hover_redraw()


func _request_hover_redraw() -> void:
	if _hover_redraw_timer.is_stopped():
		queue_redraw()
		_hover_redraw_timer.start()
	else:
		_hover_redraw_pending = true


func _on_hover_redraw_timeout() -> void:
	if _hover_redraw_pending:
		_hover_redraw_pending = false
		queue_redraw()
		_hover_redraw_timer.start()


func _zoom_at(screen_pos: Vector2, delta: float) -> void:
	var old_zoom := zoom_level
	zoom_level = clampf(zoom_level + delta, min_zoom, max_zoom)

	if zoom_level != old_zoom:
		# Zoom toward mouse position
		var world_pos := (screen_pos - camera_offset) / old_zoom
		camera_offset = screen_pos - world_pos * zoom_level
		queue_redraw()


## Convert screen position to tile coordinate
func screen_to_tile(screen_pos: Vector2) -> Vector2i:
	var world_pos := (screen_pos - camera_offset) / zoom_level
	var tile_x := int(floor(world_pos.x / tile_size))
	var tile_y := int(floor(world_pos.y / tile_size))
	return Vector2i(tile_x, tile_y)


## Convert tile coordinate to screen position (top-left corner)
func tile_to_screen(tile: Vector2i) -> Vector2:
	var world_pos := Vector2(tile.x * tile_size, tile.y * tile_size)
	return camera_offset + world_pos * zoom_level


## Convert world pixel position to tile coordinate
func world_to_tile(world_pos: Vector2) -> Vector2i:
	var tile_x := int(floor(world_pos.x / tile_size))
	var tile_y := int(floor(world_pos.y / tile_size))
	return Vector2i(tile_x, tile_y)


func _on_tile_selected(tile: Vector2i) -> void:
	queue_redraw()


func _on_tile_hovered(tile: Vector2i) -> void:
	queue_redraw()


## Reset view to center
func reset_view() -> void:
	zoom_level = 1.0
	_center_view()


## Get agent ID at a tile position (-1 if no agent)
func get_agent_at_tile(tile: Vector2i) -> int:
	for agent in _agents_data:
		if not agent.get("alive", true):
			continue
		var ax: int = agent.get("pos_x", 0)
		var ay: int = agent.get("pos_y", 0)
		if ax == tile.x and ay == tile.y:
			return agent.get("id", -1)
	return -1


## Set agent target position for line drawing
func set_agent_target(target_pos: Vector2i) -> void:
	_agent_target_pos = target_pos
	queue_redraw()


## Clear agent target position
func clear_agent_target() -> void:
	_agent_target_pos = Vector2i(-1, -1)
	queue_redraw()
