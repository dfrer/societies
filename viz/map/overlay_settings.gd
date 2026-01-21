class_name OverlaySettings
extends RefCounted

## Manages overlay toggle states and provides color utilities.

signal settings_changed()

## Overlay toggles
var show_claims: bool = true
var show_resources: bool = true
var show_pollution: bool = false
var show_faction_borders: bool = false
var show_market: bool = true
var show_agents: bool = true
var show_grid: bool = true

## Resource display mode
enum ResourceDisplayMode { DOTS, LETTERS, SCALED_DOTS }
var resource_display_mode: int = ResourceDisplayMode.DOTS

## Cache for owner colors
var _owner_color_cache: Dictionary = {}

## Predefined faction colors (for first 10 factions)
const FACTION_COLORS: Array = [
	Color(0.2, 0.6, 0.9),   # Blue
	Color(0.9, 0.3, 0.3),   # Red
	Color(0.3, 0.8, 0.3),   # Green
	Color(0.9, 0.7, 0.2),   # Gold
	Color(0.7, 0.3, 0.9),   # Purple
	Color(0.3, 0.8, 0.8),   # Cyan
	Color(0.9, 0.5, 0.2),   # Orange
	Color(0.8, 0.4, 0.6),   # Pink
	Color(0.5, 0.7, 0.3),   # Lime
	Color(0.6, 0.4, 0.2),   # Brown
]

## Predefined agent colors (for individual agents)
const AGENT_COLORS: Array = [
	Color(0.4, 0.6, 0.8),
	Color(0.8, 0.5, 0.4),
	Color(0.5, 0.7, 0.5),
	Color(0.7, 0.6, 0.4),
	Color(0.6, 0.4, 0.7),
	Color(0.4, 0.7, 0.7),
	Color(0.7, 0.5, 0.3),
	Color(0.6, 0.5, 0.6),
]

## Unclaimed tile color
const COLOR_UNCLAIMED: Color = Color(0.25, 0.25, 0.28)

## Faction owner ID offset (from World class)
const FACTION_OWNER_OFFSET: int = 1000001


func set_show_claims(value: bool) -> void:
	if show_claims != value:
		show_claims = value
		settings_changed.emit()


func set_show_resources(value: bool) -> void:
	if show_resources != value:
		show_resources = value
		settings_changed.emit()


func set_show_pollution(value: bool) -> void:
	if show_pollution != value:
		show_pollution = value
		settings_changed.emit()


func set_show_faction_borders(value: bool) -> void:
	if show_faction_borders != value:
		show_faction_borders = value
		settings_changed.emit()


func set_show_market(value: bool) -> void:
	if show_market != value:
		show_market = value
		settings_changed.emit()


func set_show_agents(value: bool) -> void:
	if show_agents != value:
		show_agents = value
		settings_changed.emit()


func set_show_grid(value: bool) -> void:
	if show_grid != value:
		show_grid = value
		settings_changed.emit()


func set_resource_display_mode(mode: int) -> void:
	if resource_display_mode != mode:
		resource_display_mode = mode
		settings_changed.emit()


## Get a deterministic color for an owner ID
func get_owner_color(owner_id: int) -> Color:
	if owner_id <= 0:
		return COLOR_UNCLAIMED

	# Check cache first
	if _owner_color_cache.has(owner_id):
		return _owner_color_cache[owner_id]

	var color: Color

	# Check if this is a faction owner
	if owner_id >= FACTION_OWNER_OFFSET:
		var faction_id: int = owner_id - FACTION_OWNER_OFFSET
		var idx: int = faction_id % FACTION_COLORS.size()
		color = FACTION_COLORS[idx]
	else:
		# Individual agent owner
		var idx: int = owner_id % AGENT_COLORS.size()
		color = AGENT_COLORS[idx]
		# Add slight variation based on full ID
		var hue_shift: float = (owner_id * 0.037) - floor(owner_id * 0.037)
		color = color.lightened(hue_shift * 0.2 - 0.1)

	_owner_color_cache[owner_id] = color
	return color


## Get color for a resource type
func get_resource_color(resource_type: String) -> Color:
	match resource_type:
		"berry":
			return Color(0.9, 0.2, 0.3)
		"tree":
			return Color(0.2, 0.7, 0.2)
		"ore":
			return Color(0.5, 0.5, 0.65)
		_:
			return Color(0.5, 0.5, 0.5)


## Get letter glyph for a resource type
func get_resource_letter(resource_type: String) -> String:
	match resource_type:
		"berry":
			return "B"
		"tree":
			return "T"
		"ore":
			return "O"
		_:
			return "?"


## Get pollution color (red tint based on pollution level 0-1)
func get_pollution_color(pollution: float) -> Color:
	var clamped: float = clampf(pollution, 0.0, 1.0)
	# From transparent to red overlay
	return Color(0.8, 0.1, 0.1, clamped * 0.5)


## Get pollution tint for the whole map
func get_global_pollution_tint(avg_pollution: float) -> Color:
	var clamped: float = clampf(avg_pollution, 0.0, 1.0)
	return Color(0.6, 0.2, 0.1, clamped * 0.3)


## Check if owner is a faction
func is_faction_owner(owner_id: int) -> bool:
	return owner_id >= FACTION_OWNER_OFFSET


## Get faction ID from owner ID
func get_faction_id(owner_id: int) -> int:
	if owner_id >= FACTION_OWNER_OFFSET:
		return owner_id - FACTION_OWNER_OFFSET
	return -1


## Clear the color cache (call if owner assignments change dramatically)
func clear_cache() -> void:
	_owner_color_cache.clear()
