class_name OverlayToolbar
extends PanelContainer

## Toolbar for toggling map overlays.

signal overlay_changed()

@onready var claims_check: CheckBox = $MarginContainer/VBoxContainer/ClaimsCheck
@onready var resources_check: CheckBox = $MarginContainer/VBoxContainer/ResourcesCheck
@onready var pollution_check: CheckBox = $MarginContainer/VBoxContainer/PollutionCheck
@onready var faction_borders_check: CheckBox = $MarginContainer/VBoxContainer/FactionBordersCheck
@onready var market_check: CheckBox = $MarginContainer/VBoxContainer/MarketCheck
@onready var agents_check: CheckBox = $MarginContainer/VBoxContainer/AgentsCheck
@onready var grid_check: CheckBox = $MarginContainer/VBoxContainer/GridCheck
@onready var resource_mode_option: OptionButton = $MarginContainer/VBoxContainer/ResourceModeOption

## The overlay settings object to sync with
var settings: OverlaySettings = null


func _ready() -> void:
	# Connect toggle signals
	claims_check.toggled.connect(_on_claims_toggled)
	resources_check.toggled.connect(_on_resources_toggled)
	pollution_check.toggled.connect(_on_pollution_toggled)
	faction_borders_check.toggled.connect(_on_faction_borders_toggled)
	market_check.toggled.connect(_on_market_toggled)
	agents_check.toggled.connect(_on_agents_toggled)
	grid_check.toggled.connect(_on_grid_toggled)
	resource_mode_option.item_selected.connect(_on_resource_mode_selected)

	# Setup resource mode dropdown
	resource_mode_option.clear()
	resource_mode_option.add_item("Dots", OverlaySettings.ResourceDisplayMode.DOTS)
	resource_mode_option.add_item("Letters", OverlaySettings.ResourceDisplayMode.LETTERS)
	resource_mode_option.add_item("Scaled", OverlaySettings.ResourceDisplayMode.SCALED_DOTS)


func set_settings(new_settings: OverlaySettings) -> void:
	settings = new_settings
	_sync_from_settings()


func _sync_from_settings() -> void:
	if settings == null:
		return

	# Block signals while syncing
	claims_check.set_pressed_no_signal(settings.show_claims)
	resources_check.set_pressed_no_signal(settings.show_resources)
	pollution_check.set_pressed_no_signal(settings.show_pollution)
	faction_borders_check.set_pressed_no_signal(settings.show_faction_borders)
	market_check.set_pressed_no_signal(settings.show_market)
	agents_check.set_pressed_no_signal(settings.show_agents)
	grid_check.set_pressed_no_signal(settings.show_grid)

	# Sync resource mode
	for i in range(resource_mode_option.item_count):
		if resource_mode_option.get_item_id(i) == settings.resource_display_mode:
			resource_mode_option.select(i)
			break


func _on_claims_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_claims(pressed)
	overlay_changed.emit()


func _on_resources_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_resources(pressed)
	overlay_changed.emit()


func _on_pollution_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_pollution(pressed)
	overlay_changed.emit()


func _on_faction_borders_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_faction_borders(pressed)
	overlay_changed.emit()


func _on_market_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_market(pressed)
	overlay_changed.emit()


func _on_agents_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_agents(pressed)
	overlay_changed.emit()


func _on_grid_toggled(pressed: bool) -> void:
	if settings:
		settings.set_show_grid(pressed)
	overlay_changed.emit()


func _on_resource_mode_selected(index: int) -> void:
	if settings:
		var mode: int = resource_mode_option.get_item_id(index)
		settings.set_resource_display_mode(mode)
	overlay_changed.emit()
