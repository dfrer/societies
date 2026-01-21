class_name SelectionModel
extends RefCounted

## Manages selection state for the map view.
## Tracks selected tile, hovered tile, selected agent, and emits signals on changes.

signal tile_selected(tile: Vector2i)
signal tile_hovered(tile: Vector2i)
signal selection_cleared()
signal agent_selected(agent_id: int)

## Currently selected tile (-1, -1 means no selection)
var selected_tile: Vector2i = Vector2i(-1, -1)

## Currently hovered tile (-1, -1 means no hover)
var hover_tile: Vector2i = Vector2i(-1, -1)

## Currently selected agent ID (-1 means no agent selection)
var selected_agent_id: int = -1

## World dimensions for bounds checking
var world_width: int = 96
var world_height: int = 96


func set_world_size(width: int, height: int) -> void:
	world_width = width
	world_height = height


func is_valid_tile(tile: Vector2i) -> bool:
	return tile.x >= 0 and tile.x < world_width and tile.y >= 0 and tile.y < world_height


func select_tile(tile: Vector2i) -> void:
	if not is_valid_tile(tile):
		return
	if selected_tile != tile:
		selected_tile = tile
		tile_selected.emit(tile)


func clear_selection() -> void:
	if selected_tile != Vector2i(-1, -1):
		selected_tile = Vector2i(-1, -1)
		selection_cleared.emit()


func set_hover(tile: Vector2i) -> void:
	if hover_tile != tile:
		hover_tile = tile
		if is_valid_tile(tile):
			tile_hovered.emit(tile)


func clear_hover() -> void:
	if hover_tile != Vector2i(-1, -1):
		hover_tile = Vector2i(-1, -1)


func has_selection() -> bool:
	return selected_tile != Vector2i(-1, -1)


func has_hover() -> bool:
	return is_valid_tile(hover_tile)


func select_agent(agent_id: int) -> void:
	if selected_agent_id != agent_id:
		selected_agent_id = agent_id
		agent_selected.emit(agent_id)


func clear_agent_selection() -> void:
	if selected_agent_id != -1:
		selected_agent_id = -1


func has_agent_selection() -> bool:
	return selected_agent_id != -1

