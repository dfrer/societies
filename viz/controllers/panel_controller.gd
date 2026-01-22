## PanelController - Manages data panels and their updates
## Coordinates updates between market, factions, contracts, and timeline panels

class_name PanelController
extends RefCounted

## Signals for panel events
signal panels_updated()
signal visibility_changed(panel_name: String, visible: bool)

## Panel references
var _data_tabs: TabContainer
var _market_panel: MarketPanel
var _factions_panel: FactionsPanel
var _contracts_panel: PanelContainer
var _timeline_panel: Control  # Changed from EventTimelinePanel to avoid dependency
var _metrics_panel: PanelContainer

## Sim runner reference for data access
var _sim_runner: SimRunnerNode

## Update throttling
var _last_update_tick: int = -1
var _update_interval: int = 1  # Update every tick for now, can be adjusted


func _init(
	data_tabs: TabContainer,
	market_panel: MarketPanel,
	factions_panel: FactionsPanel,
	contracts_panel: PanelContainer,
	timeline_panel: Control,  # Changed from EventTimelinePanel
	metrics_panel: PanelContainer,
	sim_runner: SimRunnerNode
) -> void:
	_data_tabs = data_tabs
	_market_panel = market_panel
	_factions_panel = factions_panel
	_contracts_panel = contracts_panel
	_timeline_panel = timeline_panel
	_metrics_panel = metrics_panel
	_sim_runner = sim_runner


## Update all visible panels with current simulation state
func update_panels() -> void:
	if _sim_runner.sim == null:
		return

	var current_tick = _sim_runner.sim.state.tick
	
	# Throttle updates based on interval
	if current_tick - _last_update_tick < _update_interval:
		return
	
	var sim_state: SimState = _sim_runner.sim.state
	
	# Update only the active tab to reduce unnecessary work
	var active_tab := _data_tabs.get_child(_data_tabs.current_tab)
	if active_tab and active_tab.has_method("update_sim_state"):
		active_tab.update_sim_state(sim_state)
	
	# Update metrics panel if visible
	if _metrics_panel.visible:
		_metrics_panel.update_sim_state(sim_state)
	
	_last_update_tick = current_tick
	panels_updated.emit()


## Force immediate update of all panels (for initialization)
func force_update_all() -> void:
	if _sim_runner.sim == null:
		return

	var sim_state: SimState = _sim_runner.sim.state
	
	# Update all data tabs
	for child in _data_tabs.get_children():
		if child.has_method("update_sim_state"):
			child.update_sim_state(sim_state)
	
	# Update metrics panel
	_metrics_panel.update_sim_state(sim_state)
	
	_last_update_tick = sim_state.tick
	panels_updated.emit()


## Update specific panel
func update_panel(panel_name: String) -> void:
	if _sim_runner.sim == null:
		return

	var sim_state: SimState = _sim_runner.sim.state
	
	match panel_name:
		"market":
			if _market_panel.has_method("update_sim_state"):
				_market_panel.update_sim_state(sim_state)
		"factions":
			if _factions_panel.has_method("update_sim_state"):
				_factions_panel.update_sim_state(sim_state)
		"contracts":
			if _contracts_panel.has_method("update_sim_state"):
				_contracts_panel.update_sim_state(sim_state)
		"timeline":
			if _timeline_panel and _timeline_panel.has_method("update_sim_state"):
				_timeline_panel.update_sim_state(sim_state)
		"metrics":
			if _metrics_panel.visible and _metrics_panel.has_method("update_sim_state"):
				_metrics_panel.update_sim_state(sim_state)


## Handle tab changes so panels refresh even when simulation is paused.
func on_tab_changed(tab_index: int) -> void:
	var panel_name := _panel_name_for_tab(tab_index)
	if panel_name == "":
		return
	update_panel(panel_name)


func _panel_name_for_tab(tab_index: int) -> String:
	if tab_index < 0 or tab_index >= _data_tabs.get_child_count():
		return ""

	var tab := _data_tabs.get_child(tab_index)
	if tab == _market_panel:
		return "market"
	if tab == _factions_panel:
		return "factions"
	if tab == _contracts_panel:
		return "contracts"
	if tab == _timeline_panel:
		return "timeline"
	return ""


## Set panel visibility
func set_panel_visible(panel_name: String, visible: bool) -> void:
	match panel_name:
		"metrics":
			_metrics_panel.visible = visible
		_:
			push_warning("Unknown panel: " + panel_name)
	
	visibility_changed.emit(panel_name, visible)


## Get current visible tab
func get_current_tab() -> int:
	return _data_tabs.current_tab


## Set current tab
func set_current_tab(tab_index: int) -> void:
	if tab_index >= 0 and tab_index < _data_tabs.get_child_count():
		_data_tabs.current_tab = tab_index


## Get panel by name
func get_panel(panel_name: String) -> Variant:
	match panel_name:
		"market":
			return _market_panel
		"factions":
			return _factions_panel
		"contracts":
			return _contracts_panel
		"timeline":
			return _timeline_panel as Control
		"metrics":
			return _metrics_panel
		_:
			return null


## Set update interval for throttling
func set_update_interval(ticks: int) -> void:
	_update_interval = max(1, ticks)


## Get update statistics
func get_update_stats() -> Dictionary:
	return {
		"last_update_tick": _last_update_tick,
		"update_interval": _update_interval,
		"current_tick": _sim_runner.sim.state.tick if _sim_runner.sim else -1
	}
